using ApexPost.Application.Abstractions;
using ApexPost.Domain.ValueObjects;
using ApexPost.Infrastructure.Persistence.Sql.Converters;
using ApexPost.Infrastructure.Persistence.Sql.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence.Sql;

/// <summary>
/// The EF Core model of ApexPostDb. It is scoped (one per request) and shared by every repository and the unit of work.
/// </summary>
public sealed class ApexPostDbContext(DbContextOptions<ApexPostDbContext> options, IClock clock) : DbContext(options)
{
    private const string CreatedAtUtc = "CreatedAtUtc";
    private const string CreatedBy = "CreatedBy";
    private const string ModifiedAtUtc = "ModifiedAtUtc";

    public DbSet<RoleEntity> Roles => Set<RoleEntity>();
    public DbSet<ProductCategoryEntity> ProductCategories => Set<ProductCategoryEntity>();
    public DbSet<BranchEntity> Branches => Set<BranchEntity>();
    public DbSet<CounterEntity> Counters => Set<CounterEntity>();
    public DbSet<StaffEntity> Staff => Set<StaffEntity>();
    public DbSet<ProductEntity> Products => Set<ProductEntity>();
    public DbSet<BranchStockEntity> BranchStock => Set<BranchStockEntity>();
    public DbSet<StockAdjustmentEntity> StockAdjustments => Set<StockAdjustmentEntity>();
    public DbSet<ShippingZoneEntity> ShippingZones => Set<ShippingZoneEntity>();
    public DbSet<ShippingRateEntity> ShippingRates => Set<ShippingRateEntity>();
    public DbSet<CounterTransactionEntity> CounterTransactions => Set<CounterTransactionEntity>();
    public DbSet<TransactionLineEntity> TransactionLines => Set<TransactionLineEntity>();
    public DbSet<ShipmentEntity> Shipments => Set<ShipmentEntity>();
    public DbSet<MoneyOrderEntity> MoneyOrders => Set<MoneyOrderEntity>();
    public DbSet<PaymentEntity> Payments => Set<PaymentEntity>();
    public DbSet<IdentityVerificationEntity> IdentityVerifications => Set<IdentityVerificationEntity>();
    public DbSet<AuditEntryEntity> AuditEntries => Set<AuditEntryEntity>();
    //public DbSet<SyncInboxEntity> SyncInbox => Set<SyncInboxEntity>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // One rule per value object, applied to every column and foreign key that uses it.
        configurationBuilder.Properties<BranchCode>().HaveConversion<BranchCodeConverter>().HaveColumnType("varchar(5)");
        configurationBuilder.Properties<Sku>().HaveConversion<SkuConverter>().HaveColumnType("varchar(8)");
        configurationBuilder.Properties<Money>().HaveConversion<MoneyConverter>().HaveColumnType("decimal(18,2)");
        configurationBuilder.Properties<Weight>().HaveConversion<WeightConverter>().HaveColumnType("decimal(18,3)");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApexPostDbContext).Assembly);

        // Audit columns are shadow properties: they exist in the table but not on the entity classes.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            if (clrType == typeof(RoleEntity) || clrType == typeof(ProductCategoryEntity))
                continue;

            var builder = modelBuilder.Entity(clrType);
            builder.Property<DateTime>(CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            builder.Property<string>(CreatedBy).HasMaxLength(100).IsRequired(false);
            builder.Property<DateTime?>(ModifiedAtUtc);
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampAuditColumns();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampAuditColumns();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void StampAuditColumns()
    {
        var now = clock.UtcNow.UtcDateTime;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
                continue;
            if (entry.Metadata.FindProperty(CreatedAtUtc) is null)
                continue;

            if (entry.State == EntityState.Added)
                entry.Property(CreatedAtUtc).CurrentValue = now;
            else
                entry.Property(ModifiedAtUtc).CurrentValue = now;
        }
    }
}