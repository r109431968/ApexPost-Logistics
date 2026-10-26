using ApexPost.Application.Abstractions;
using ApexPost.Application.Settings;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Rules;
using ApexPost.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ApexPost.Infrastructure.Seeding;

/// <summary>
/// Fills empty data files with demo data. Everything goes through the real domain methods,
/// so the seed obeys the same business rules as the live application.
/// </summary>
public sealed class DemoDataSeeder(
    SeedSettings settings,
    IRepository<Branch, BranchCode> branchRepo,
    IRepository<Staff, Guid> staffRepo,
    IRepository<Product, Sku> productRepo,
    IRepository<StockItem, string> stockRepo,
    IRepository<ShippingZone, string> zoneRepo,
    IRepository<CounterTransaction, Guid> transactionRepo,
    IShippingPricer pricer,
    ITaxCalculator tax,
    WeightLimitRule weightRule,
    MoneyOrderSettings moneyOrderSettings,
    IClock clock,
    ILogger<DemoDataSeeder> logger)
{
    private const int RandomSeed = 20261025;
    private const int TransactionCount = 48;
    private const int OpenBasketIndex = 47;
    private const int FailedVerificationIndex = 13;

    private const TaxCategory Std = TaxCategory.Standard;
    private const TaxCategory Red = TaxCategory.Reduced;

    private static readonly (string Code, string Name, string Region, int OpenHour, int CloseHour, bool Active)[] BranchData =
    [
        ("BR001", "Central Counter", "Central", 8, 18, true),
        ("BR002", "Harbour Point", "South", 9, 17, true),
        ("BR003", "Airport Terminal", "East", 6, 22, true),
        ("BR004", "Northgate", "North", 9, 17, true),
        ("BR005", "Westfield", "West", 10, 19, true),
        ("BR006", "Old Quay", "South", 9, 17, false)
    ];

    private static readonly string[] FirstNames =
    [
        "Asha", "Ravi", "Meera", "Arjun", "Priya", "Kabir", "Neha", "Rohan", "Isha", "Vikram",
        "Anita", "Sanjay", "Divya", "Karan", "Pooja", "Imran", "Sara", "Daniel", "Emily", "Omar",
        "Grace", "Liam", "Chloe", "Noah", "Zara"
    ];

    private static readonly string[] LastNames =
    [
        "Rao", "Sharma", "Iyer", "Mehta", "Nair", "Kapoor", "Singh", "Das", "Khan", "Patel",
        "Gupta", "Reddy", "Joshi", "Bose", "Menon", "Verma", "Shah", "Clarke", "Evans", "Hughes",
        "Morgan", "Bennett", "Foster", "Hayes", "Reid"
    ];

    private static readonly string[] Senders =
        ["Alan Pryce", "Rita Moore", "Samir Ali", "Helen Cole", "Tom Barker", "Nadia Khan", "Paul Weir", "Lucy Shaw"];

    private static readonly string[] Recipients =
        ["Maria Lopez", "John Doyle", "Aisha Noor", "Peter Hale", "Sofia Rossi", "Ken Ito", "Bella Grant", "Omar Said"];

    private static readonly string[] VoidReasons =
        ["Customer changed their mind", "Wrong item scanned", "Payment problem at the counter", "Duplicate basket"];

    private static readonly string[] RefundReasons =
        ["Item returned damaged", "Customer cancelled the shipment", "Charged twice by mistake"];

    private static readonly (string Sku, string Name, ProductCategory Category, decimal Price, TaxCategory Tax)[] ProductData =
    [
        ("PKG-0001", "Small Cardboard Box", ProductCategory.Packaging, 1.20m, Std),
        ("PKG-0002", "Medium Cardboard Box", ProductCategory.Packaging, 1.80m, Std),
        ("PKG-0003", "Large Cardboard Box", ProductCategory.Packaging, 2.60m, Std),
        ("PKG-0004", "Extra Large Cardboard Box", ProductCategory.Packaging, 3.90m, Std),
        ("PKG-0005", "Bubble Wrap 5m", ProductCategory.Packaging, 4.50m, Std),
        ("PKG-0006", "Packing Tape 48mm", ProductCategory.Packaging, 2.25m, Std),
        ("PKG-0007", "Padded Envelope A4", ProductCategory.Packaging, 0.95m, Std),
        ("PKG-0008", "Padded Envelope A5", ProductCategory.Packaging, 0.75m, Std),
        ("PKG-0009", "Fragile Stickers 50 Pack", ProductCategory.Packaging, 1.50m, Std),
        ("PKG-0010", "Packing Peanuts Bag", ProductCategory.Packaging, 3.40m, Std),
        ("PKG-0011", "Tube Mailer", ProductCategory.Packaging, 2.10m, Std),
        ("PKG-0012", "Document Wallet", ProductCategory.Packaging, 0.60m, Std),
        ("STA-0001", "Ballpoint Pens 10 Pack", ProductCategory.Stationery, 2.40m, Std),
        ("STA-0002", "A4 Notepad", ProductCategory.Stationery, 1.95m, Red),
        ("STA-0003", "DL Envelopes 50 Pack", ProductCategory.Stationery, 2.80m, Red),
        ("STA-0004", "Greeting Card", ProductCategory.Stationery, 1.50m, Std),
        ("STA-0005", "Permanent Marker", ProductCategory.Stationery, 1.20m, Std),
        ("STA-0006", "Sticky Notes", ProductCategory.Stationery, 1.35m, Std),
        ("STA-0007", "Highlighters 4 Pack", ProductCategory.Stationery, 2.20m, Std),
        ("STA-0008", "Scissors", ProductCategory.Stationery, 3.50m, Std),
        ("STA-0009", "Glue Stick", ProductCategory.Stationery, 1.10m, Std),
        ("STA-0010", "Pencil Case", ProductCategory.Stationery, 4.25m, Std),
        ("OFF-0001", "A4 Paper Ream", ProductCategory.OfficeSupplies, 5.50m, Red),
        ("OFF-0002", "Stapler", ProductCategory.OfficeSupplies, 6.90m, Std),
        ("OFF-0003", "Staples Box", ProductCategory.OfficeSupplies, 1.15m, Std),
        ("OFF-0004", "Paper Clips 100 Pack", ProductCategory.OfficeSupplies, 0.95m, Std),
        ("OFF-0005", "Ring Binder", ProductCategory.OfficeSupplies, 2.75m, Std),
        ("OFF-0006", "Lever Arch File", ProductCategory.OfficeSupplies, 3.60m, Std),
        ("OFF-0007", "Desk Calculator", ProductCategory.OfficeSupplies, 8.50m, Std),
        ("OFF-0008", "Tape Dispenser", ProductCategory.OfficeSupplies, 3.20m, Std),
        ("OFF-0009", "Printer Ink Black", ProductCategory.OfficeSupplies, 19.90m, Std),
        ("OFF-0010", "USB Flash Drive 16GB", ProductCategory.OfficeSupplies, 7.40m, Std)
    ];

    private static readonly (string Code, string Name, decimal BaseRate, decimal PerKgRate)[] ZoneData =
    [
        ("Z01", "Local", 3.00m, 1.00m),
        ("Z02", "National", 4.00m, 1.20m),
        ("Z03", "Europe West", 8.50m, 2.40m),
        ("Z04", "Europe East", 9.50m, 2.80m),
        ("Z05", "North America", 14.00m, 4.50m),
        ("Z06", "South America", 16.00m, 5.20m),
        ("Z07", "Middle East", 13.00m, 4.20m),
        ("Z08", "Africa", 15.00m, 4.80m),
        ("Z09", "Asia Pacific", 15.50m, 4.90m),
        ("Z10", "Oceania", 17.00m, 5.50m)
    ];

    /// <summary>Returns true when data was written, false when seeding was skipped.</summary>
    public async Task<bool> SeedIfEmptyAsync(CancellationToken cancellationToken)
    {
        if (!settings.Enabled)
        {
            logger.LogInformation("Demo data seeding is switched off.");
            return false;
        }

        if ((await branchRepo.ListAsync(cancellationToken)).Count > 0)
        {
            logger.LogInformation("Existing data found, so the demo data seed was skipped.");
            return false;
        }

        var now = clock.UtcNow;

        var branchList = BuildBranches();
        var staffList = BuildStaff();
        var productList = BuildProducts();
        var zoneList = BuildZones();
        var stock = BuildStock(branchList, productList);
        var transactionList = BuildTransactions(
            now, new Random(RandomSeed), branchList, staffList, productList, zoneList, stock);
        ApplyLowStockDemo(branchList, productList, stock);

        await SaveAsync(productRepo, productList, cancellationToken);
        await SaveAsync(zoneRepo, zoneList, cancellationToken);
        await SaveAsync(staffRepo, staffList, cancellationToken);
        await SaveAsync(stockRepo, stock.Values, cancellationToken);
        await SaveAsync(transactionRepo, transactionList, cancellationToken);

        // Branches go last: if the seed is interrupted, the next start sees no branches and runs it again.
        await SaveAsync(branchRepo, branchList, cancellationToken);

        logger.LogInformation(
            "Seeded {Branches} branches, {Staff} staff, {Products} products, {Zones} zones, {Stock} stock rows and {Transactions} transactions.",
            branchList.Count, staffList.Count, productList.Count, zoneList.Count, stock.Count, transactionList.Count);

        return true;
    }

    private static List<Branch> BuildBranches()
    {
        var result = new List<Branch>();

        for (var index = 0; index < BranchData.Length; index++)
        {
            var data = BranchData[index];
            var branch = new Branch(
                new BranchCode(data.Code), data.Name, data.Region, new TimeOnly(data.OpenHour, 0), new TimeOnly(data.CloseHour, 0));

            for (var counter = 1; counter <= 3; counter++)
                branch.AddCounter(Guid.Parse($"10000000-0000-0000-0000-{index * 10 + counter:D12}"), $"Counter {counter}");

            if (!data.Active)
                branch.Close();

            result.Add(branch);
        }

        return result;
    }

    private static List<Staff> BuildStaff()
    {
        var staff = new List<Staff>();
        var nameIndex = 0;
        var idNumber = 1;

        Staff Add(StaffRole role, string emailName, string branchCode)
        {
            var name = $"{FirstNames[nameIndex % FirstNames.Length]} {LastNames[(nameIndex * 7 + 3) % LastNames.Length]}";
            nameIndex++;

            var member = new Staff(
                Guid.Parse($"20000000-0000-0000-0000-{idNumber++:D12}"),
                name,
                $"{emailName}@apexpost.test",
                role,
                new BranchCode(branchCode));

            staff.Add(member);
            return member;
        }

        foreach (var branch in BranchData.Where(data => data.Active))
        {
            var suffix = branch.Code.ToLowerInvariant();
            Add(StaffRole.BranchManager, $"manager.{suffix}", branch.Code);
            Add(StaffRole.CounterClerk, $"clerk1.{suffix}", branch.Code);
            Add(StaffRole.CounterClerk, $"clerk2.{suffix}", branch.Code);
            Add(StaffRole.CounterClerk, $"clerk3.{suffix}", branch.Code);
        }

        Add(StaffRole.Auditor, "auditor1", "BR001");
        Add(StaffRole.Auditor, "auditor2", "BR003");
        Add(StaffRole.Administrator, "admin1", "BR001");
        Add(StaffRole.Administrator, "admin2", "BR002");

        // Shows the "inactive staff cannot process transactions" rule.
        Add(StaffRole.CounterClerk, "clerk4.br002", "BR002").Deactivate();

        // The closed branch still has a manager.
        Add(StaffRole.BranchManager, "manager.br006", "BR006");

        return staff;
    }

    private static List<Product> BuildProducts() => ProductData
        .Select(data => new Product(new Sku(data.Sku), data.Name, data.Category, new Money(data.Price), data.Tax))
        .ToList();

    private static List<ShippingZone> BuildZones() => ZoneData
        .Select(data => new ShippingZone(data.Code, data.Name, new Money(data.BaseRate), new Money(data.PerKgRate)))
        .ToList();

    private static Dictionary<string, StockItem> BuildStock(
        IReadOnlyList<Branch> branchList, IReadOnlyList<Product> productList)
    {
        var result = new Dictionary<string, StockItem>(StringComparer.Ordinal);
        var branchNumber = 0;

        foreach (var branch in branchList.Where(candidate => candidate.IsActive))
        {
            for (var product = 0; product < productList.Count; product++)
            {
                var quantity = 25 + (product * 11 + branchNumber * 17) % 46;
                var item = new StockItem(branch.Code, productList[product].Sku, quantity);
                result[item.Id] = item;
            }

            branchNumber++;
        }

        return result;
    }

    private List<CounterTransaction> BuildTransactions(
        DateTimeOffset now,
        Random random,
        IReadOnlyList<Branch> branchList,
        IReadOnlyList<Staff> staffList,
        IReadOnlyList<Product> productList,
        IReadOnlyList<ShippingZone> zoneList,
        Dictionary<string, StockItem> stock)
    {
        var activeBranches = branchList.Where(branch => branch.IsActive).ToList();
        var threshold = new Money(moneyOrderSettings.IdentityVerificationThreshold);
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var result = new List<CounterTransaction>();

        for (var i = 0; i < TransactionCount; i++)
        {
            var branch = activeBranches[i % activeBranches.Count];
            var clerks = staffList
                .Where(member => member.Role == StaffRole.CounterClerk && member.IsActive && member.HomeBranch == branch.Code)
                .ToList();
            var manager = staffList.First(
                member => member.Role == StaffRole.BranchManager && member.HomeBranch == branch.Code);

            var clerk = clerks[i % clerks.Count];
            var counter = branch.Counters[i % branch.Counters.Count];
            var when = MomentFor(i, today, now);
            var level = (ServiceLevel)(1 + i % 3);

            var transaction = CounterTransaction.Open(branch, counter, clerk, when);

            switch (i)
            {
                case 7:
                    AddRetail(transaction, branch, random, productList, stock, 1);
                    AddMoneyOrder(transaction, 1200m, i);
                    Verify(transaction, i, VerificationResult.Passed, when);
                    break;
                case 22:
                    AddMoneyOrder(transaction, 1500m, i);
                    Verify(transaction, i, VerificationResult.Passed, when);
                    break;
                case 31:
                    AddMoneyOrder(transaction, 650m, i);
                    Verify(transaction, i, VerificationResult.Passed, when);
                    break;
                case FailedVerificationIndex:
                    AddMoneyOrder(transaction, 800m, i);
                    Verify(transaction, i, VerificationResult.Failed, when);
                    break;
                default:
                    switch (i % 6)
                    {
                        case 0:
                            AddRetail(transaction, branch, random, productList, stock, 2 + i % 3);
                            break;
                        case 1:
                            AddShipping(transaction, random, zoneList, ShipmentType.Parcel, level);
                            break;
                        case 2:
                            AddRetail(transaction, branch, random, productList, stock, 2);
                            AddShipping(transaction, random, zoneList, ShipmentType.Parcel, ServiceLevel.Express);
                            break;
                        case 3:
                            AddMoneyOrder(transaction, 120m + i % 5 * 40m, i);
                            break;
                        case 4:
                            AddRetail(transaction, branch, random, productList, stock, 1);
                            AddMoneyOrder(transaction, 350m + i % 4 * 25m, i);
                            Verify(transaction, i, VerificationResult.Passed, when);
                            break;
                        default:
                            AddRetail(transaction, branch, random, productList, stock, 1);
                            AddShipping(transaction, random, zoneList, ShipmentType.Letter, ServiceLevel.Registered);
                            break;
                    }

                    break;
            }

            if (i == FailedVerificationIndex)
            {
                transaction.Void(manager, "Customer could not complete identity verification");
            }
            else if (i != OpenBasketIndex)
            {
                transaction.Pay((PaymentMethod)(1 + i % 3), threshold, tax.AsRule());
                TakeStock(transaction, stock);

                if (i % 11 == 4)
                {
                    transaction.Void(manager, VoidReasons[i % VoidReasons.Length]);
                    Restock(transaction, stock);
                }
                else if (i % 13 == 6)
                {
                    transaction.Refund(manager, RefundReasons[i % RefundReasons.Length]);
                    Restock(transaction, stock);
                }
            }

            result.Add(transaction);
        }

        return result;
    }

    private static DateTimeOffset MomentFor(int i, DateOnly today, DateTimeOffset now)
    {
        var day = today.AddDays(-(i * 3 % 14));
        var moment = new DateTimeOffset(day.Year, day.Month, day.Day, 9 + i % 7, i * 7 % 60, 0, TimeSpan.Zero);
        return moment > now ? moment.AddDays(-1) : moment;
    }

    private static void AddRetail(
        CounterTransaction transaction,
        Branch branch,
        Random random,
        IReadOnlyList<Product> productList,
        Dictionary<string, StockItem> stock,
        int lineCount)
    {
        var added = 0;
        var tried = new HashSet<int>();

        while (added < lineCount && tried.Count < productList.Count)
        {
            var index = random.Next(productList.Count);
            if (!tried.Add(index))
                continue;

            var product = productList[index];
            var available = stock[StockItem.KeyFor(branch.Code, product.Sku)].Quantity;
            var quantity = Math.Min(random.Next(1, 5), available);
            if (quantity <= 0)
                continue;

            transaction.AddLine(new RetailLineItem(product.Sku, product.Name, quantity, product.UnitPrice, product.TaxCategory));
            added++;
        }
    }

    private void AddShipping(
        CounterTransaction transaction,
        Random random,
        IReadOnlyList<ShippingZone> zoneList,
        ShipmentType type,
        ServiceLevel level)
    {
        var zone = zoneList[random.Next(zoneList.Count)];
        var max = weightRule.MaximumFor(type, level);
        var fraction = 0.05m + (decimal)random.NextDouble() * 0.90m;
        var grams = Math.Min(max.Grams, Math.Max(10m, Math.Round(max.Grams * fraction / 10m) * 10m));
        var weight = Weight.FromGrams(grams);

        weightRule.EnsureWithinLimit(type, level, weight);
        var price = pricer.Price(type, level, weight, zone);

        transaction.AddLine(new ShippingLineItem(type, weight, zone.Code, level, price));
    }

    private void AddMoneyOrder(CounterTransaction transaction, decimal amount, int i)
    {
        var fee = Math.Max(moneyOrderSettings.MinimumFee, amount * moneyOrderSettings.FeePercent);

        transaction.AddLine(new MoneyOrderLineItem(
            new Money(amount),
            new Money(fee),
            Senders[i % Senders.Length],
            Recipients[(i * 3 + 1) % Recipients.Length]));
    }

    private static void Verify(CounterTransaction transaction, int i, VerificationResult result, DateTimeOffset when) =>
        transaction.AttachVerification(IdentityVerification.Create(
            (DocumentType)(1 + i % 3), $"DOC{100000 + i * 37}", result, when));

    private static void TakeStock(CounterTransaction transaction, Dictionary<string, StockItem> stock)
    {
        foreach (var group in transaction.Lines.OfType<RetailLineItem>().GroupBy(line => line.Sku))
            stock[StockItem.KeyFor(transaction.BranchCode, group.Key)].Remove(group.Sum(line => line.Quantity));
    }

    private static void Restock(CounterTransaction transaction, Dictionary<string, StockItem> stock)
    {
        foreach (var group in transaction.Lines.OfType<RetailLineItem>().GroupBy(line => line.Sku))
            stock[StockItem.KeyFor(transaction.BranchCode, group.Key)].Add(group.Sum(line => line.Quantity));
    }

    // Replaces a few stock rows with low numbers (one is zero) so the low-stock report has something to show.
    private static void ApplyLowStockDemo(
        IReadOnlyList<Branch> branchList, IReadOnlyList<Product> productList, Dictionary<string, StockItem> stock)
    {
        var activeBranches = branchList.Where(branch => branch.IsActive).ToList();

        for (var branchNumber = 0; branchNumber < activeBranches.Count; branchNumber++)
        {
            for (var k = 0; k < 3; k++)
            {
                var product = productList[(branchNumber * 3 + k * 9 + 2) % productList.Count];
                var quantity = branchNumber == 2 && k == 0 ? 0 : (branchNumber + k * 2) % 5 + 1;

                var item = new StockItem(activeBranches[branchNumber].Code, product.Sku, quantity);
                stock[item.Id] = item;
            }
        }
    }

    private static async Task SaveAsync<T, TId>(
        IRepository<T, TId> repository, IEnumerable<T> items, CancellationToken cancellationToken)
        where T : Entity<TId>
        where TId : notnull
    {
        foreach (var item in items)
        {
            if (await repository.GetByIdAsync(item.Id, cancellationToken) is null)
                await repository.AddAsync(item, cancellationToken);
            else
                await repository.UpdateAsync(item, cancellationToken);
        }
    }
}
