using System.Net;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Reports;
using ApexPost.Application.Settings;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using ApexPost.IntegrationTests.Support;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace ApexPost.IntegrationTests.Persistence;

/// <summary>
/// Tests that need the real SQL Server: seeding, transactions, concurrency, query counts, stored procedures and set-based updates.
/// Every test class that uses ApexFactory gets its own disposable database, so these tests never touch ApexPostDb.
/// </summary>
public class SqlPersistenceTests(ApexFactory factory, ITestOutputHelper output) : IClassFixture<ApexFactory>
{
    private static readonly BranchCode Br001 = new("BR001");

    private async Task<Sku> AnyStockedSkuAsync()
    {
        await using var db = factory.CreateDbContext();
        return await db.BranchStock.AsNoTracking()
            .Where(row => row.BranchCode == Br001 && row.Quantity > 0)
            .OrderBy(row => row.Sku)
            .Select(row => row.Sku)
            .FirstAsync();
    }

    // ---- Seed volume -------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_seeder_creates_the_required_volume_of_data()
    {
        using var big = new ApexFactory(new Dictionary<string, string?> { ["Seed:TransactionCount"] = "240" });
        await using var db = big.CreateDbContext();

        Assert.True(await db.Branches.CountAsync() >= 5);
        Assert.True(await db.Staff.CountAsync() >= 20);
        Assert.True(await db.Products.CountAsync() >= 30);
        Assert.True(await db.ShippingZones.CountAsync() >= 10);
        Assert.True(await db.CounterTransactions.CountAsync() >= 200);
    }

    // ---- One commit, all or nothing ----------------------------------------------------------------------------

    [Fact]
    public async Task A_failed_commit_rolls_back_everything_that_was_staged_with_it()
    {
        var sku = await AnyStockedSkuAsync();
        var key = StockItem.KeyFor(Br001, sku);

        int before;
        await using (var db = factory.CreateDbContext())
            before = await db.BranchStock.Where(row => row.BranchCode == Br001 && row.Sku == sku).Select(row => row.Quantity).SingleAsync();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var stock = scope.ServiceProvider.GetRequiredService<IStockRepository>();
            var staff = scope.ServiceProvider.GetRequiredService<IStaffRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // Staged together: a valid stock change and a staff member whose e-mail already exists (unique index).
            var item = (await stock.GetByIdAsync(key, default))!;
            item.Add(7);
            await stock.UpdateAsync(item, default);
            await staff.AddAsync(
                new Staff(Guid.NewGuid(), "Duplicate", ApexFactory.AdminEmail, StaffRole.CounterClerk, Br001), default);

            await Assert.ThrowsAsync<DuplicateValueException>(() => uow.SaveChangesAsync(default));
        }

        await using var check = factory.CreateDbContext();
        var after = await check.BranchStock.Where(row => row.BranchCode == Br001 && row.Sku == sku).Select(row => row.Quantity).SingleAsync();
        Assert.Equal(before, after);
    }

    // ---- Optimistic concurrency --------------------------------------------------------------------------------

    [Fact]
    public async Task Two_people_changing_the_same_stock_row_cause_a_concurrency_conflict_for_the_second()
    {
        var sku = await AnyStockedSkuAsync();
        var key = StockItem.KeyFor(Br001, sku);

        await using var scopeA = factory.Services.CreateAsyncScope();
        await using var scopeB = factory.Services.CreateAsyncScope();
        var stockA = scopeA.ServiceProvider.GetRequiredService<IStockRepository>();
        var stockB = scopeB.ServiceProvider.GetRequiredService<IStockRepository>();

        // Both read the same row (and the same rowversion) before either one saves.
        var itemA = (await stockA.GetByIdAsync(key, default))!;
        var itemB = (await stockB.GetByIdAsync(key, default))!;

        itemA.Add(1);
        await stockA.UpdateAsync(itemA, default);
        await scopeA.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default);

        itemB.Add(2);
        await stockB.UpdateAsync(itemB, default);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => scopeB.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default));
    }

    [Theory]
    [InlineData(typeof(ConcurrencyConflictException), 409)]
    [InlineData(typeof(DuplicateValueException), 409)]
    [InlineData(typeof(NotFoundException), 404)]
    public void Exceptions_are_mapped_to_the_documented_http_status(Type exceptionType, int expectedStatus)
    {
        Exception exception = exceptionType == typeof(NotFoundException)
            ? new NotFoundException("Product", "XXX-0000")
            : exceptionType == typeof(DuplicateValueException)
                ? new DuplicateValueException(new InvalidOperationException("duplicate"))
                : new ConcurrencyConflictException();

        Assert.True(ProblemMapping.TryMap(exception, out var status, out _, out _));
        Assert.Equal(expectedStatus, status);
    }

    [Fact]
    public void A_business_rule_violation_is_mapped_to_400()
    {
        Assert.True(ProblemMapping.TryMap(new ApexPost.Domain.Exceptions.DomainException("rule"), out var status, out _, out _));
        Assert.Equal(400, status);
    }

    // ---- No N+1 ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_number_of_sql_commands_does_not_depend_on_the_number_of_rows_returned()
    {
        var capture = new CommandLogCapture();
        using var logged = factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging =>
        {
            logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);
            logging.AddProvider(capture);
        }));

        async Task<(int Rows, int Commands)> RecentAsync(int count)
        {
            await using var scope = logged.Services.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<ICounterTransactionRepository>();

            capture.Clear();
            var rows = await repository.RecentForBranchAsync(Br001, count, default);
            // Touch everything the page shows (lines, payment) so lazy loading mistakes would show up here as extra commands.
            _ = rows.Sum(row => row.Lines.Count);
            return (rows.Count, capture.Commands.Count);
        }

        var small = await RecentAsync(3);
        var large = await RecentAsync(30);

        output.WriteLine($"3 requested: {small.Rows} rows, {small.Commands} command(s)");
        output.WriteLine($"30 requested: {large.Rows} rows, {large.Commands} command(s)");

        Assert.True(large.Rows > small.Rows, "The second call must return more rows, or the comparison proves nothing.");
        Assert.Equal(small.Commands, large.Commands);
        Assert.InRange(large.Commands, 1, 3);
    }

    [Fact]
    public async Task Generated_sql_for_the_main_queries_is_printed_for_the_performance_document()
    {
        await using var db = factory.CreateDbContext();

        var recent = db.CounterTransactions.AsNoTracking()
            .Where(t => t.BranchCode == Br001)
            .OrderByDescending(t => t.OpenedAtUtc)
            .Take(20)
            .ToQueryString();

        var stock = db.BranchStock.AsNoTracking()
            .Where(row => row.BranchCode == Br001 && row.Quantity <= 5)
            .OrderBy(row => row.Quantity)
            .ToQueryString();

        var withLines = db.CounterTransactions.AsNoTracking()
            .Include(t => t.Lines)
            .Where(t => t.BranchCode == Br001)
            .AsSplitQuery()
            .ToQueryString();

        output.WriteLine("-- RECENT TRANSACTIONS\n" + recent);
        output.WriteLine("\n-- LOW STOCK\n" + stock);
        output.WriteLine("\n-- TRANSACTIONS WITH LINES (split query)\n" + withLines);

        Assert.Contains("TOP(@", recent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE", stock, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", withLines, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Stored procedures -------------------------------------------------------------------------------------

    [Fact]
    public async Task Daily_sales_from_the_stored_procedure_match_the_in_memory_calculation()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var fromSql = sp.GetRequiredService<IReportService>();
        var fromDomain = new ReportService(
            sp.GetRequiredService<IRepository<CounterTransaction, Guid>>(),
            sp.GetRequiredService<IRepository<StockItem, string>>(),
            sp.GetRequiredService<IRepository<Product, Sku>>(),
            new StockSettings(),
            new MoneyOrderSettings(),
            sp.GetRequiredService<IClock>());

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var offset = 0; offset < 14; offset++)
        {
            var day = today.AddDays(-offset);
            var expected = await fromDomain.DailySalesAsync(Br001, day, default);
            var actual = await fromSql.DailySalesAsync(Br001, day, default);

            Assert.Equal(expected.TransactionCount, actual.TransactionCount);
            Assert.Equal(expected.RetailTotal.Amount, actual.RetailTotal.Amount);
            Assert.Equal(expected.ServicesTotal.Amount, actual.ServicesTotal.Amount);
        }
    }

    [Fact]
    public async Task Low_stock_from_the_stored_procedure_matches_the_in_memory_list()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var fromSql = sp.GetRequiredService<IReportService>();
        var fromDomain = new ReportService(
            sp.GetRequiredService<IRepository<CounterTransaction, Guid>>(),
            sp.GetRequiredService<IRepository<StockItem, string>>(),
            sp.GetRequiredService<IRepository<Product, Sku>>(),
            new StockSettings(),
            new MoneyOrderSettings(),
            sp.GetRequiredService<IClock>());

        var expected = await fromDomain.LowStockAsync(Br001, 40, default);
        var actual = await fromSql.LowStockAsync(Br001, 40, default);

        Assert.NotEmpty(actual);
        Assert.Equal(
            expected.Select(i => (i.Sku.Value, i.Quantity)).OrderBy(i => i.Value).ToList(),
            actual.Select(i => (i.Sku.Value, i.Quantity)).OrderBy(i => i.Value).ToList());
    }

    // ---- Set-based reports: SQL Server gives the same answer as the in-memory reference -------------------------

    private static ReportService InMemoryReports(IServiceProvider sp) => new(
        sp.GetRequiredService<IRepository<CounterTransaction, Guid>>(),
        sp.GetRequiredService<IRepository<StockItem, string>>(),
        sp.GetRequiredService<IRepository<Product, Sku>>(),
        new StockSettings(),
        new MoneyOrderSettings(),
        sp.GetRequiredService<IClock>());

    [Fact]
    public async Task Shipping_volume_from_sql_matches_the_in_memory_calculation()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var fromSql = sp.GetRequiredService<IReportService>();
        var fromDomain = InMemoryReports(sp);

        var to = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = to.AddDays(-90);

        foreach (var branch in new BranchCode?[] { null, Br001 })
        {
            var expected = await fromDomain.ShippingVolumeAsync(from, to, branch, default);
            var actual = await fromSql.ShippingVolumeAsync(from, to, branch, default);

            if (branch is null)
                Assert.NotEmpty(actual);

            Assert.Equal(
                expected.Select(row => (row.ZoneCode, row.ServiceLevel, row.ShipmentCount, row.TotalWeightKg, row.Revenue.Amount)).ToList(),
                actual.Select(row => (row.ZoneCode, row.ServiceLevel, row.ShipmentCount, row.TotalWeightKg, row.Revenue.Amount)).ToList());
        }
    }

    [Fact]
    public async Task The_money_order_audit_from_sql_matches_the_in_memory_calculation()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var fromSql = sp.GetRequiredService<IReportService>();
        var fromDomain = InMemoryReports(sp);

        foreach (var branch in new BranchCode?[] { null, Br001 })
        {
            var expected = await fromDomain.MoneyOrderAuditAsync(branch, default);
            var actual = await fromSql.MoneyOrderAuditAsync(branch, default);

            if (branch is null)
                Assert.NotEmpty(actual);

            Assert.Equal(
                expected.Select(Totals).ToList(),
                actual.Select(Totals).ToList());

            Assert.Equal(
                expected.SelectMany(summary => summary.Rows).Select(RowKey).Order().ToList(),
                actual.SelectMany(summary => summary.Rows).Select(RowKey).Order().ToList());
        }

        static (string, int, decimal, int, int, int) Totals(BranchMoneyOrderSummary summary) => (
            summary.BranchCode.Value, summary.PaidMoneyOrderCount, summary.PaidTotalAmount.Amount,
            summary.VerificationsPassed, summary.VerificationsFailed, summary.Rows.Count);

        static string RowKey(MoneyOrderAuditRow row) =>
            $"{row.TransactionId}|{row.At.UtcDateTime:O}|{row.Status}|{row.Amount.Amount}|{row.Fee.Amount}|{row.DocumentType}|{row.MaskedDocumentNumber}|{row.VerificationResult}";
    }

    [Fact]
    public async Task Dashboard_alerts_from_sql_match_the_in_memory_calculation()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var fromSql = sp.GetRequiredService<IReportService>();
        var fromDomain = InMemoryReports(sp);

        var expected = await fromDomain.DashboardAsync(Br001, default);
        var actual = await fromSql.DashboardAsync(Br001, default);

        Assert.Equal(expected.Day, actual.Day);
        Assert.Equal(
            expected.Alerts.Select(alert => $"{alert.Kind}|{alert.Message}|{alert.TransactionId}").Order().ToList(),
            actual.Alerts.Select(alert => $"{alert.Kind}|{alert.Message}|{alert.TransactionId}").Order().ToList());
    }

    [Theory]
    [InlineData("EXEC dbo.usp_DailyBranchSales @Day = NULL", 50020)]
    [InlineData("EXEC dbo.usp_DailyBranchSales @Day = '2026-10-31', @BranchCode = 'XX'", 50021)]
    [InlineData("EXEC dbo.usp_LowStockReport @Threshold = -1", 50022)]
    [InlineData("EXEC dbo.usp_LowStockReport @CategoryId = 999999", 50023)]
    public async Task The_report_procedures_reject_bad_parameters_with_a_clear_error(string call, int expectedError)
    {
        await using var db = factory.CreateDbContext();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = call;

        var error = await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync());

        Assert.Equal(expectedError, error.Number);
    }

    [Fact]
    public async Task A_stock_adjustment_runs_the_stored_procedure_and_writes_a_history_row()
    {
        var sku = await AnyStockedSkuAsync();
        var manager = await factory.SignedInAsync(ApexFactory.ManagerEmail);

        int before;
        long historyBefore;
        await using (var db = factory.CreateDbContext())
        {
            before = await db.BranchStock.Where(r => r.BranchCode == Br001 && r.Sku == sku).Select(r => r.Quantity).SingleAsync();
            historyBefore = await db.StockAdjustments.LongCountAsync();
        }

        var response = await manager.PostWithTokenAsync("/Catalogue", "/Catalogue/Adjust", new Dictionary<string, string>
        {
            ["Sku"] = sku.Value, ["Delta"] = "3", ["Reason"] = "delivery received"
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var check = factory.CreateDbContext();
        Assert.Equal(before + 3, await check.BranchStock.Where(r => r.BranchCode == Br001 && r.Sku == sku).Select(r => r.Quantity).SingleAsync());
        Assert.Equal(historyBefore + 1, await check.StockAdjustments.LongCountAsync());

        var last = await check.StockAdjustments.AsNoTracking().OrderByDescending(r => r.Id).FirstAsync();
        Assert.Equal(3, last.Delta);
        Assert.Equal(before + 3, last.QuantityAfter);
        Assert.Equal("delivery received", last.Reason);
    }

    [Fact]
    public async Task A_stock_adjustment_below_zero_is_rejected_with_409_and_changes_nothing()
    {
        var sku = await AnyStockedSkuAsync();
        var manager = await factory.SignedInAsync(ApexFactory.ManagerEmail);

        int before;
        long historyBefore;
        await using (var db = factory.CreateDbContext())
        {
            before = await db.BranchStock.Where(r => r.BranchCode == Br001 && r.Sku == sku).Select(r => r.Quantity).SingleAsync();
            historyBefore = await db.StockAdjustments.LongCountAsync();
        }

        // The form allows -1000 at most, which is more than the seeded stock of any item.
        var request = new HttpRequestMessage(HttpMethod.Post, "/Catalogue/Adjust")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Sku"] = sku.Value,
                ["Delta"] = (-(before + 1)).ToString(),
                ["Reason"] = "too much",
                ["__RequestVerificationToken"] = await manager.AntiforgeryTokenAsync("/Catalogue")
            })
        };
        request.Headers.Accept.ParseAdd("application/json");

        var response = await manager.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var check = factory.CreateDbContext();
        Assert.Equal(before, await check.BranchStock.Where(r => r.BranchCode == Br001 && r.Sku == sku).Select(r => r.Quantity).SingleAsync());
        Assert.Equal(historyBefore, await check.StockAdjustments.LongCountAsync());
    }

    // ---- Set-based update --------------------------------------------------------------------------------------

    [Fact]
    public async Task An_administrator_can_raise_a_category_by_ten_percent_in_one_transaction_and_the_cache_is_refreshed()
    {
        Dictionary<string, decimal> before;
        await using (var db = factory.CreateDbContext())
        {
            before = (await db.Products.AsNoTracking().Where(p => p.CategoryId == ProductCategory.Stationery).ToListAsync())
                .ToDictionary(p => p.Sku.Value, p => p.UnitPrice.Amount);
            Assert.NotEmpty(before);
        }

        var otherBefore = await PricesOutsideAsync(ProductCategory.Stationery);
        var admin = await factory.SignedInAsync(ApexFactory.AdminEmail);

        var response = await admin.PostWithTokenAsync("/Catalogue", "/Catalogue/RevisePrices", new Dictionary<string, string>
        {
            ["Category"] = "Stationery", ["Percent"] = "10"
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var check = factory.CreateDbContext();
        var after = (await check.Products.AsNoTracking().Where(p => p.CategoryId == ProductCategory.Stationery).ToListAsync())
            .ToDictionary(p => p.Sku.Value, p => p.UnitPrice.Amount);

        foreach (var (sku, oldPrice) in before)
            Assert.Equal(new Money(oldPrice * 1.10m).Amount, after[sku]);

        // Other categories are untouched.
        Assert.Equal(otherBefore, await PricesOutsideAsync(ProductCategory.Stationery));

        // The singleton catalogue cache must show the new price too.
        var catalog = factory.Services.GetRequiredService<IProductCatalog>();
        var firstSku = before.Keys.OrderBy(k => k).First();
        Assert.Equal(after[firstSku], (await catalog.GetAsync(new Sku(firstSku), default))!.UnitPrice.Amount);
    }

    [Fact]
    public async Task A_manager_cannot_revise_prices()
    {
        var manager = await factory.SignedInAsync(ApexFactory.ManagerEmail);

        var response = await manager.PostWithTokenAsync("/Catalogue", "/Catalogue/RevisePrices", new Dictionary<string, string>
        {
            ["Category"] = "Stationery", ["Percent"] = "10"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<List<(string Sku, decimal Price)>> PricesOutsideAsync(ProductCategory category)
    {
        await using var db = factory.CreateDbContext();
        return (await db.Products.AsNoTracking().Where(p => p.CategoryId != category).ToListAsync())
            .OrderBy(p => p.Sku.Value)
            .Select(p => (p.Sku.Value, p.UnitPrice.Amount))
            .ToList();
    }

    // ---- Over-posting ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Extra_form_fields_such_as_id_password_hash_and_is_active_are_ignored_when_creating_staff()
    {
        var admin = await factory.SignedInAsync(ApexFactory.AdminEmail);
        var smuggledId = Guid.NewGuid();

        var response = await admin.PostWithTokenAsync("/Staff/Create", "/Staff/Create", new Dictionary<string, string>
        {
            ["Name"] = "Over Poster",
            ["Email"] = "over.poster@apexpost.test",
            ["Role"] = "CounterClerk",
            ["HomeBranch"] = "BR001",
            ["Password"] = "Test-Passw0rd-2026!",
            // None of these are fields of the view model, so the model binder must not touch them.
            ["Id"] = smuggledId.ToString(),
            ["IsActive"] = "false",
            ["PasswordHash"] = "evil-hash",
            ["RoleId"] = "4"
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var db = factory.CreateDbContext();
        var created = await db.Staff.AsNoTracking().SingleAsync(s => s.Email == "over.poster@apexpost.test");
        Assert.NotEqual(smuggledId, created.Id);
        Assert.True(created.IsActive);
        Assert.NotEqual("evil-hash", created.PasswordHash);
        Assert.Equal(StaffRole.CounterClerk, created.RoleId);
    }
}
