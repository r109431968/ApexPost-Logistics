using ApexPost.Application.Reports;
using ApexPost.Application.Services;
using ApexPost.Application.Settings;
using ApexPost.Application.Validation;
using ApexPost.Domain.Rules;
using ApexPost.Infrastructure;
using ApexPost.Infrastructure.Auditing;
using ApexPost.Infrastructure.Caching;
using ApexPost.Infrastructure.Notifications;
using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.Infrastructure.Persistence.Json.Mapping;
using ApexPost.Infrastructure.Persistence.Json.Records;
using ApexPost.Infrastructure.Security;
using ApexPost.Infrastructure.Seeding;
using ApexPost.Infrastructure.Shipping;
using ApexPost.Infrastructure.Verification;

namespace ApexPost.Web.Composition;

/// <summary>
/// Registers the domain, application and infrastructure services.
/// Lifetimes: repositories, catalogues, events, the audit store and the notification sink are singletons
/// (they hold caches or locks and are thread safe). Use cases are scoped. Settings are read lazily from
/// IConfiguration, so tests can override configuration before the first request.
/// </summary>
public static class ServiceRegistration
{
    public static IServiceCollection AddApexPost(this IServiceCollection services)
    {
        services.AddSettings<JsonStorageSettings>(JsonStorageSettings.SectionName);
        services.AddSettings<ShippingSettings>(ShippingSettings.SectionName);
        services.AddSettings<TaxSettings>(TaxSettings.SectionName);
        services.AddSettings<MoneyOrderSettings>(MoneyOrderSettings.SectionName);
        services.AddSettings<StockSettings>(StockSettings.SectionName);
        services.AddSettings<PasswordSettings>(PasswordSettings.SectionName);
        services.AddSettings<CourierSettings>(CourierSettings.SectionName);
        services.AddSettings<CatalogCacheSettings>(CatalogCacheSettings.SectionName);
        services.AddSettings<SeedSettings>(SeedSettings.SectionName);

        services.AddSingleton(sp => sp.GetRequiredService<ShippingSettings>().CreateWeightLimitRule());
        services.AddSingleton<IClock, SystemClock>();

        services.AddSingleton<IRepository<Branch, BranchCode>>(sp =>
            new JsonRepository<Branch, BranchCode, BranchRecord>(DataFile(sp, "branches.json"), new BranchMapper()));
        services.AddSingleton<IRepository<Staff, Guid>>(sp =>
            new JsonRepository<Staff, Guid, StaffRecord>(DataFile(sp, "staff.json"), new StaffMapper()));
        services.AddSingleton<IRepository<Product, Sku>>(sp =>
            new JsonRepository<Product, Sku, ProductRecord>(DataFile(sp, "products.json"), new ProductMapper()));
        services.AddSingleton<IRepository<StockItem, string>>(sp =>
            new JsonRepository<StockItem, string, StockRecord>(DataFile(sp, "stock.json"), new StockMapper()));
        services.AddSingleton<IRepository<ShippingZone, string>>(sp =>
            new JsonRepository<ShippingZone, string, ZoneRecord>(DataFile(sp, "zones.json"), new ZoneMapper()));
        services.AddSingleton<IRepository<CounterTransaction, Guid>>(sp =>
            new JsonRepository<CounterTransaction, Guid, TransactionRecord>(
                DataFile(sp, "transactions.json"), new TransactionMapper()));

        services.AddSingleton<ICredentialStore>(sp => new JsonCredentialStore(DataFile(sp, "credentials.json")));
        services.AddSingleton<IAuditStore>(sp => new JsonLinesAuditStore(DataFile(sp, "audit.jsonl")));

        services.AddSingleton<IProductCatalog, ProductCatalog>();
        services.AddSingleton<IZoneCatalog, ZoneCatalog>();
        services.AddSingleton<INotificationSink, InMemoryNotificationSink>();
        services.AddSingleton<ICourierTrackingService, SimulatedCourierTrackingService>();
        services.AddSingleton<IIdentityVerificationService>(_ => new SimulatedIdentityVerificationService());

        services.AddSingleton<CounterEvents>();
        services.AddSingleton<NotificationSubscriber>();
        services.AddSingleton<IModelValidator, AttributeModelValidator>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IShippingPricer, ShippingPricer>();
        services.AddSingleton<ITaxCalculator, TaxCalculator>();

        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<IReversalService, ReversalService>();
        services.AddScoped<ILoginService, LoginService>();
        services.AddScoped<IStaffService, StaffService>();
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<ICatalogueService, CatalogueService>();
        services.AddScoped<ITransactionQueries, TransactionQueries>();
        services.AddScoped<IReportService, ReportService>();

        services.AddTransient<DemoDataSeeder>();
        services.AddTransient<CredentialSeeder>();

        return services;
    }

    /// <summary>Connects the observer (event subscriber) once at start-up.</summary>
    public static void AttachEventSubscribers(this IServiceProvider services) =>
        services.GetRequiredService<NotificationSubscriber>().Attach();

    public static async Task SeedDemoDataAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var cancellationToken = app.Lifetime.ApplicationStopping;

        await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedIfEmptyAsync(cancellationToken);
        await scope.ServiceProvider.GetRequiredService<CredentialSeeder>().SeedAsync(cancellationToken);
    }

    private static IServiceCollection AddSettings<T>(this IServiceCollection services, string section)
        where T : class, new() =>
        services.AddSingleton(sp => sp.GetRequiredService<IConfiguration>().GetSection(section).Get<T>() ?? new T());

    private static string DataFile(IServiceProvider services, string fileName)
    {
        var storage = services.GetRequiredService<JsonStorageSettings>();
        var environment = services.GetRequiredService<IHostEnvironment>();

        var directory = Path.IsPathRooted(storage.DataDirectory)
            ? storage.DataDirectory
            : Path.Combine(environment.ContentRootPath, storage.DataDirectory);

        return Path.Combine(directory, fileName);
    }
}
