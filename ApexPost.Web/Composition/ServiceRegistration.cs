using ApexPost.Application.Reports;
using ApexPost.Application.Services;
using ApexPost.Application.Settings;
using ApexPost.Application.Validation;
using ApexPost.Domain.Rules;
using ApexPost.Infrastructure;
using ApexPost.Infrastructure.Caching;
using ApexPost.Infrastructure.Notifications;
using ApexPost.Infrastructure.Persistence.Sql;
using ApexPost.Infrastructure.Persistence.Sql.Procedures;
using ApexPost.Infrastructure.Persistence.Sql.Reporting;
using ApexPost.Infrastructure.Persistence.Sql.Repositories;
using ApexPost.Infrastructure.Persistence.Sql.Stores;
using ApexPost.Infrastructure.Seeding;
using ApexPost.Infrastructure.Shipping;
using ApexPost.Infrastructure.Verification;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Web.Composition;

/// <summary>
/// Registers the domain, application and infrastructure services.
/// Lifetimes: the DbContext, the repositories, the unit of work and the use cases are scoped (one per request, and
/// they all share the same DbContext). Catalogue caches, events, the notification sink and the audit store are
/// singletons: they hold caches or locks, or they open their own short-lived DbContext. Settings are read lazily
/// from IConfiguration, so tests can override configuration before the first request.
/// </summary>
public static class ServiceRegistration
{
    public const string ConnectionName = "ApexPostDb";

    public static IServiceCollection AddApexPost(this IServiceCollection services)
    {
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

        // SQL Server. The connection string comes from user-secrets (Development) or the environment, never from source control.
        services.AddDbContextFactory<ApexPostDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionName)
                ?? throw new InvalidOperationException(
                    $"The connection string '{ConnectionName}' is missing. Set it with dotnet user-secrets (see README).");

            options.UseSqlServer(connectionString);
        });
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<ApexPostDbContext>>().CreateDbContext());

        // One DbContext per request is shared by every repository and the unit of work.
        // The generic interfaces point at the same scoped instance as the specific ones.
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IRepository<Branch, BranchCode>>(sp => sp.GetRequiredService<IBranchRepository>());
        services.AddScoped<IStaffRepository, StaffRepository>();
        services.AddScoped<IRepository<Staff, Guid>>(sp => sp.GetRequiredService<IStaffRepository>());
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IRepository<Product, Sku>>(sp => sp.GetRequiredService<IProductRepository>());
        services.AddScoped<IZoneRepository, ZoneRepository>();
        services.AddScoped<IRepository<ShippingZone, string>>(sp => sp.GetRequiredService<IZoneRepository>());
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IRepository<StockItem, string>>(sp => sp.GetRequiredService<IStockRepository>());
        services.AddScoped<ICounterTransactionRepository, CounterTransactionRepository>();
        services.AddScoped<IRepository<CounterTransaction, Guid>>(sp => sp.GetRequiredService<ICounterTransactionRepository>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IProductPriceWriter, ProductPriceWriter>();

        services.AddScoped<ICredentialStore, SqlCredentialStore>();
        services.AddSingleton<IAuditStore, SqlAuditStore>();

        services.AddSingleton<IProductCatalog>(sp => new ProductCatalog(
            new ScopedReadRepository<Product, Sku>(sp.GetRequiredService<IServiceScopeFactory>()),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<CatalogCacheSettings>()));
        services.AddSingleton<IZoneCatalog>(sp => new ZoneCatalog(
            new ScopedReadRepository<ShippingZone, string>(sp.GetRequiredService<IServiceScopeFactory>()),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<CatalogCacheSettings>()));

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
        // The web application uses the stored-procedure versions of the catalogue and report services.
        services.AddScoped<ICatalogueService, SqlCatalogueService>();
        services.AddScoped<IPriceRevisionService, PriceRevisionService>();
        services.AddScoped<ITransactionQueries, TransactionQueries>();
        services.AddScoped<IReportService, SqlReportService>();

        services.AddTransient<DemoDataSeeder>();
        services.AddTransient<CredentialSeeder>();

        return services;
    }

    /// <summary>Connects the observer (event subscriber) once at start-up.</summary>
    public static void AttachEventSubscribers(this IServiceProvider services) =>
        services.GetRequiredService<NotificationSubscriber>().Attach();

    /// <summary>
    /// Applies pending migrations when Database:ApplyMigrationsOnStartup is true (Development and tests).
    /// It runs the migrations, it never calls EnsureCreated. Production deployments use the idempotent script.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
            return;

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApexPostDbContext>();
        await db.Database.MigrateAsync(app.Lifetime.ApplicationStopping);
    }

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
}