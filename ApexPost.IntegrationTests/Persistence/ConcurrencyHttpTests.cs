using System.Net;
using ApexPost.Infrastructure.Persistence.Sql;
using ApexPost.Infrastructure.Persistence.Sql.Entities;
using ApexPost.IntegrationTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.IntegrationTests.Persistence;

/// <summary>
/// Proves the whole chain for a stock conflict: rowversion in SQL Server, DbUpdateConcurrencyException in EF Core,
/// ConcurrencyConflictException in the unit of work, and an HTTP 409 problem response at the edge.
/// </summary>
public class ConcurrencyHttpTests(ApexFactory factory) : IClassFixture<ApexFactory>
{
    [Fact]
    public async Task A_sale_whose_stock_row_was_changed_by_someone_else_gets_http_409_and_changes_nothing()
    {
        var branch = new ApexPost.Domain.ValueObjects.BranchCode("BR001");
        await using var seedDb = factory.CreateDbContext();
        var stocked = await seedDb.BranchStock.AsNoTracking()
            .Where(row => row.BranchCode == branch && row.Quantity > 5)
            .OrderBy(row => row.Sku)
            .FirstAsync();
        var sku = stocked.Sku.Value;
        var before = stocked.Quantity;

        // From now on the application writes through a context that has the "other clerk" interceptor attached.
        var interceptor = new OtherClerkChangesStockInterceptor(factory.ConnectionString);
        using var web = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<ApexPostDbContext>(options => options.AddInterceptors(interceptor))));

        var client = web.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        Assert.Equal(HttpStatusCode.Redirect, (await client.LoginAsync(ApexFactory.ClerkEmail, ApexFactory.DemoPassword)).StatusCode);

        var id = await client.OpenTransactionAsync();
        var details = $"/Transactions/Details/{id}";
        var add = await client.PostWithTokenAsync(
            details, $"/Transactions/AddItem/{id}", new Dictionary<string, string> { ["Sku"] = sku, ["Quantity"] = "1" });
        Assert.Equal(HttpStatusCode.Redirect, add.StatusCode);

        // The clerk presses Pay. Just before the commit another clerk changes the same stock row (new rowversion).
        interceptor.Arm(sku);
        var pay = new HttpRequestMessage(HttpMethod.Post, $"/Transactions/Pay/{id}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Method"] = "Cash",
                ["__RequestVerificationToken"] = await client.AntiforgeryTokenAsync(details)
            })
        };
        pay.Headers.Accept.ParseAdd("application/json");

        var response = await client.SendAsync(pay);

        Assert.True(interceptor.Fired, "The other clerk's change must have happened, or the test proves nothing.");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Concurrent change", await response.Content.ReadAsStringAsync());

        // Nothing from the failed sale was saved: only the other clerk's +1 is in the table, and the sale is still open.
        await using var check = factory.CreateDbContext();
        var after = await check.BranchStock.AsNoTracking()
            .Where(row => row.BranchCode == stocked.BranchCode && row.Sku == stocked.Sku)
            .Select(row => row.Quantity)
            .SingleAsync();
        Assert.Equal(before + 1, after);
        Assert.Contains("Open", await client.GetStringAsync(details));
    }

    /// <summary>Simulates a second clerk: right before the first SaveChanges that touches stock, it updates that row elsewhere.</summary>
    private sealed class OtherClerkChangesStockInterceptor(string connectionString) : SaveChangesInterceptor
    {
        private string? _sku;

        public bool Fired { get; private set; }

        public void Arm(string sku) => _sku = sku;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var touchesStock = eventData.Context!.ChangeTracker.Entries<BranchStockEntity>()
                .Any(entry => entry.State == EntityState.Modified);

            if (_sku is not null && !Fired && touchesStock)
            {
                Fired = true;

                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = "UPDATE dbo.BranchStock SET Quantity = Quantity + 1 WHERE BranchCode = 'BR001' AND Sku = @sku";
                command.Parameters.Add(new SqlParameter("@sku", System.Data.SqlDbType.VarChar, 8) { Value = _sku });
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            return result;
        }
    }
}
