using ApexPost.Domain.Enums;
using ApexPost.Infrastructure.Persistence.Sql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence.Sql.Configurations;

// Delete behaviour rule used everywhere: branches, staff, products and zones are never deleted because old
// sales refer to them, so those foreign keys are Restrict. Child rows that cannot live alone are Cascade.

public sealed class RoleConfiguration : IEntityTypeConfiguration<RoleEntity>
{
    public void Configure(EntityTypeBuilder<RoleEntity> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasData(Enum.GetValues<StaffRole>().Select(role => new RoleEntity { Id = role, Name = role.ToString() }));
    }
}

public sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategoryEntity>
{
    public void Configure(EntityTypeBuilder<ProductCategoryEntity> builder)
    {
        builder.ToTable("ProductCategories");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasData(Enum.GetValues<ProductCategory>()
            .Select(category => new ProductCategoryEntity { Id = category, Name = category.ToString() }));
    }
}

public sealed class BranchConfiguration : IEntityTypeConfiguration<BranchEntity>
{
    public void Configure(EntityTypeBuilder<BranchEntity> builder)
    {
        builder.ToTable("Branches", table => table.HasCheckConstraint("CK_Branches_Hours", "[ClosesAt] > [OpensAt]"));
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("BranchCode").ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Region).HasMaxLength(60).IsRequired();
    }
}

public sealed class CounterConfiguration : IEntityTypeConfiguration<CounterEntity>
{
    public void Configure(EntityTypeBuilder<CounterEntity> builder)
    {
        builder.ToTable("Counters");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Label).HasMaxLength(50).IsRequired();
        builder.HasOne(x => x.Branch).WithMany(x => x.Counters)
            .HasForeignKey(x => x.BranchCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.BranchCode, x.Label }).IsUnique();
    }
}

public sealed class StaffConfiguration : IEntityTypeConfiguration<StaffEntity>
{
    public void Configure(EntityTypeBuilder<StaffEntity> builder)
    {
        builder.ToTable("Staff");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(254).IsUnicode(false).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(200).IsUnicode(false);
        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasOne<RoleEntity>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BranchEntity>().WithMany().HasForeignKey(x => x.HomeBranchCode).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProductConfiguration : IEntityTypeConfiguration<ProductEntity>
{
    public void Configure(EntityTypeBuilder<ProductEntity> builder)
    {
        builder.ToTable("Products", table =>
        {
            table.HasCheckConstraint("CK_Products_Price", "[UnitPrice] >= 0");
            table.HasCheckConstraint("CK_Products_TaxCategory", "[TaxCategory] IN (1, 2, 3)");
        });
        builder.HasKey(x => x.Sku);
        builder.Property(x => x.Sku).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.HasOne<ProductCategoryEntity>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BranchStockConfiguration : IEntityTypeConfiguration<BranchStockEntity>
{
    public void Configure(EntityTypeBuilder<BranchStockEntity> builder)
    {
        builder.ToTable("BranchStock", table => table.HasCheckConstraint("CK_BranchStock_Quantity", "[Quantity] >= 0"));
        builder.HasKey(x => new { x.BranchCode, x.Sku });
        // rowversion: SQL Server changes it on every update, so a second clerk's stale write is detected.
        builder.Property<byte[]>("RowVersion").IsRowVersion();
        builder.HasOne<BranchEntity>().WithMany().HasForeignKey(x => x.BranchCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProductEntity>().WithMany().HasForeignKey(x => x.Sku).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustmentEntity>
{
    public void Configure(EntityTypeBuilder<StockAdjustmentEntity> builder)
    {
        builder.ToTable("StockAdjustments", table =>
        {
            table.HasCheckConstraint("CK_StockAdjustments_Delta", "[Delta] <> 0");
            table.HasCheckConstraint("CK_StockAdjustments_After", "[QuantityAfter] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ChangedBy).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ChangedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasOne<BranchEntity>().WithMany().HasForeignKey(x => x.BranchCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProductEntity>().WithMany().HasForeignKey(x => x.Sku).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ShippingZoneConfiguration : IEntityTypeConfiguration<ShippingZoneEntity>
{
    public void Configure(EntityTypeBuilder<ShippingZoneEntity> builder)
    {
        builder.ToTable("ShippingZones");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasMaxLength(10).IsUnicode(false).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(60).IsRequired();
    }
}

public sealed class ShippingRateConfiguration : IEntityTypeConfiguration<ShippingRateEntity>
{
    public void Configure(EntityTypeBuilder<ShippingRateEntity> builder)
    {
        builder.ToTable("ShippingRates", table =>
        {
            table.HasCheckConstraint("CK_ShippingRates_Base", "[BaseRate] >= 0");
            table.HasCheckConstraint("CK_ShippingRates_PerKg", "[PerKgRate] >= 0");
        });
        builder.HasKey(x => x.ZoneCode);
        builder.Property(x => x.ZoneCode).HasMaxLength(10).IsUnicode(false).ValueGeneratedNever();
        builder.HasOne(x => x.Zone).WithOne(x => x.Rate)
            .HasForeignKey<ShippingRateEntity>(x => x.ZoneCode).OnDelete(DeleteBehavior.Cascade);
    }
}