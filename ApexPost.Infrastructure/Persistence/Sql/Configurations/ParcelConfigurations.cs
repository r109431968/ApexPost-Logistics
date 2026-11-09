using ApexPost.Infrastructure.Persistence.Sql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence.Sql.Configurations;

public sealed class ParcelConfiguration : IEntityTypeConfiguration<ParcelEntity>
{
    public void Configure(EntityTypeBuilder<ParcelEntity> builder)
    {
        builder.ToTable("Parcels", table =>
        {
            table.HasCheckConstraint("CK_Parcels_Weight", "[WeightKg] >= 0.1 AND [WeightKg] <= 70");
            table.HasCheckConstraint("CK_Parcels_Status", "[Status] BETWEEN 0 AND 5");
        });

        builder.HasKey(x => x.TrackingNumber);
        builder.Property(x => x.TrackingNumber).HasMaxLength(12).IsUnicode(false).ValueGeneratedNever();
        builder.Property(x => x.WeightKg).HasPrecision(5, 2);
        builder.Property(x => x.RecipientName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RecipientPhone).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.RecipientAddress).HasMaxLength(200).IsRequired();
        builder.Property(x => x.RecipientCity).HasMaxLength(80).IsRequired();

        // rowversion: two people changing the same parcel at once cannot silently overwrite each other (answered with 409).
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        // The list endpoint filters by status and city.
        builder.HasIndex(x => new { x.Status, x.RecipientCity });
    }
}

public sealed class ParcelScanConfiguration : IEntityTypeConfiguration<ParcelScanEntity>
{
    public void Configure(EntityTypeBuilder<ParcelScanEntity> builder)
    {
        builder.ToTable("ParcelScans", table => table.HasCheckConstraint("CK_ParcelScans_ScanType", "[ScanType] BETWEEN 0 AND 3"));

        // The scan id made on the courier's phone ("scan::{guid}") is the key. A second insert of the same id is impossible,
        // which is what makes the bulk upload idempotent even if two uploads race each other.
        builder.HasKey(x => x.ScanId);
        builder.Property(x => x.ScanId).HasMaxLength(50).IsUnicode(false).ValueGeneratedNever();
        builder.Property(x => x.TrackingNumber).HasMaxLength(12).IsUnicode(false).IsRequired();
        builder.Property(x => x.CourierId).HasMaxLength(40).IsUnicode(false).IsRequired();
        builder.Property(x => x.Location).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.RecipientSignatureName).HasMaxLength(100);

        builder.HasOne(x => x.Parcel).WithMany(x => x.Scans)
            .HasForeignKey(x => x.TrackingNumber).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.TrackingNumber, x.ScannedAtUtc });
    }
}
