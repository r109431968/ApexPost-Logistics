using ApexPost.Application.Abstractions;
using ApexPost.Application.Services;
using ApexPost.Application.Settings;
using ApexPost.Application.Validation;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApexPost.UnitTests.Support;

/// <summary>One small branch with a clerk, a manager, two products and a zone, wired to the real services.</summary>
public sealed class TestWorld
{
    public static readonly Sku Pen = new("STA-0001");
    public static readonly Sku Box = new("PKG-0001");

    public required Branch Branch { get; init; }
    public required Counter Counter { get; init; }
    public required Staff Clerk { get; init; }
    public required Staff Manager { get; init; }

    public required InMemoryRepository<Branch, BranchCode> Branches { get; init; }
    public required InMemoryRepository<Staff, Guid> StaffRepo { get; init; }
    public required InMemoryRepository<StockItem, string> Stock { get; init; }
    public required InMemoryRepository<CounterTransaction, Guid> Transactions { get; init; }

    public required CounterEvents Events { get; init; }
    public required CheckoutService Checkout { get; init; }
    public required ReversalService Reversal { get; init; }

    public MoneyOrderSettings MoneyOrders { get; } = new();
    public StockSettings StockRules { get; } = new();

    public async Task<int> StockOfAsync(Sku sku) =>
        (await Stock.GetByIdAsync(StockItem.KeyFor(Branch.Code, sku), CancellationToken.None))?.Quantity ?? 0;

    public static async Task<TestWorld> CreateAsync(int penStock = 10, bool branchActive = true)
    {
        var code = new BranchCode("BR001");
        var branch = new Branch(code, "Central", "Central", new TimeOnly(8, 0), new TimeOnly(18, 0));
        var counter = branch.AddCounter("Counter 1");
        if (!branchActive)
            branch.Close();

        var clerk = new Staff(Guid.NewGuid(), "Casey Clerk", "casey@apexpost.test", StaffRole.CounterClerk, code);
        var manager = new Staff(Guid.NewGuid(), "Morgan Manager", "morgan@apexpost.test", StaffRole.BranchManager, code);

        var branches = new InMemoryRepository<Branch, BranchCode>();
        var staff = new InMemoryRepository<Staff, Guid>();
        var productRepo = new InMemoryRepository<Product, Sku>();
        var stock = new InMemoryRepository<StockItem, string>();
        var zoneRepo = new InMemoryRepository<ShippingZone, string>();
        var transactions = new InMemoryRepository<CounterTransaction, Guid>();

        await branches.AddAsync(branch, CancellationToken.None);
        await staff.AddAsync(clerk, CancellationToken.None);
        await staff.AddAsync(manager, CancellationToken.None);

        await productRepo.AddAsync(
            new Product(Pen, "Gel pen", ProductCategory.Stationery, new Money(2.50m), TaxCategory.Standard),
            CancellationToken.None);
        await productRepo.AddAsync(
            new Product(Box, "Small box", ProductCategory.Packaging, new Money(1.20m), TaxCategory.Reduced),
            CancellationToken.None);
        await zoneRepo.AddAsync(
            new ShippingZone("Z1", "Local", new Money(5m), new Money(1m)), CancellationToken.None);

        await stock.AddAsync(new StockItem(code, Pen, penStock), CancellationToken.None);
        await stock.AddAsync(new StockItem(code, Box, 50), CancellationToken.None);

        var clock = new FixedClock(new DateTimeOffset(2026, 10, 20, 10, 0, 0, TimeSpan.Zero));
        var events = new CounterEvents(NullLogger<CounterEvents>.Instance);
        var shipping = new ShippingSettings();
        var moneyOrders = new MoneyOrderSettings();
        var stockSettings = new StockSettings();

        var checkout = new CheckoutService(
            branches, staff, new RepositoryProductCatalog(productRepo), stock, new RepositoryZoneCatalog(zoneRepo),
            transactions, new ShippingPricer(shipping), new TaxCalculator(new TaxSettings()),
            shipping.CreateWeightLimitRule(), moneyOrders, stockSettings, events, new AttributeModelValidator(), clock);

        var reversal = new ReversalService(staff, stock, transactions, events, clock);

        return new TestWorld
        {
            Branch = branch,
            Counter = counter,
            Clerk = clerk,
            Manager = manager,
            Branches = branches,
            StaffRepo = staff,
            Stock = stock,
            Transactions = transactions,
            Events = events,
            Checkout = checkout,
            Reversal = reversal
        };
    }
}
