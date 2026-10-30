using ApexPost.Infrastructure.Persistence.Sql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence.Sql.Configurations;

public sealed class CounterTransactionConfiguration : IEntityTypeConfiguration<CounterTransactionEntity>
{
    public void Configure(EntityTypeBuilder<CounterTransactionEntity> builder)
    {
        builder.ToTable("CounterTransactions", table =>
        {
            table.HasCheckConstraint("CK_CounterTransactions_Status", "[Status] IN (1, 2, 3, 4)");
            table.HasCheckConstraint("CK_CounterTransactions_Tax", "[TaxTotal] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ReversalReason).HasMaxLength(200);
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.HasOne<BranchEntity>().WithMany().HasForeignKey(x => x.BranchCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CounterEntity>().WithMany().HasForeignKey(x => x.CounterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StaffEntity>().WithMany().HasForeignKey(x => x.ClerkId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TransactionLineConfiguration : IEntityTypeConfiguration<TransactionLineEntity>
{
    public void Configure(EntityTypeBuilder<TransactionLineEntity> builder)
    {
        builder.ToTable("TransactionLines", table =>
        {
            table.HasCheckConstraint("CK_TransactionLines_Type", "[LineType] IN ('Retail', 'Shipping', 'MoneyOrder')");
            table.HasCheckConstraint("CK_TransactionLines_Quantity", "[Quantity] > 0");
            table.HasCheckConstraint("CK_TransactionLines_Price", "[UnitPrice] >= 0");
            table.HasCheckConstraint("CK_TransactionLines_Tax", "[TaxCategory] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_TransactionLines_Number", "[LineNumber] > 0");
            table.HasCheckConstraint(
                "CK_TransactionLines_RetailFields",
                "[LineType] <> 'Retail' OR ([Sku] IS NOT NULL AND [Description] IS NOT NULL)");
        });
        builder.HasKey(x => new { x.TransactionId, x.LineNumber });
        builder.Property(x => x.LineType).HasMaxLength(10).IsUnicode(false).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(200);

        builder.HasOne(x => x.Transaction).WithMany(x => x.Lines)
            .HasForeignKey(x => x.TransactionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProductEntity>().WithMany().HasForeignKey(x => x.Sku).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ShipmentConfiguration : IEntityTypeConfiguration<ShipmentEntity>
{
    public void Configure(EntityTypeBuilder<ShipmentEntity> builder)
    {
        builder.ToTable("Shipments", table =>
        {
            table.HasCheckConstraint("CK_Shipments_Weight", "[WeightGrams] > 0");
            table.HasCheckConstraint("CK_Shipments_Type", "[ShipmentType] IN (1, 2)");
            table.HasCheckConstraint("CK_Shipments_Service", "[ServiceLevel] IN (1, 2, 3)");
        });
        builder.HasKey(x => new { x.TransactionId, x.LineNumber });
        builder.Property(x => x.Weight).HasColumnName("WeightGrams");
        builder.Property(x => x.DestinationZone).HasMaxLength(10).IsUnicode(false).IsRequired();

        builder.HasOne(x => x.Line).WithOne(x => x.Shipment)
            .HasForeignKey<ShipmentEntity>(x => new { x.TransactionId, x.LineNumber }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ShippingZoneEntity>().WithMany().HasForeignKey(x => x.DestinationZone).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MoneyOrderConfiguration : IEntityTypeConfiguration<MoneyOrderEntity>
{
    public void Configure(EntityTypeBuilder<MoneyOrderEntity> builder)
    {
        builder.ToTable("MoneyOrders", table =>
        {
            table.HasCheckConstraint("CK_MoneyOrders_Amount", "[Amount] > 0");
            table.HasCheckConstraint("CK_MoneyOrders_Fee", "[Fee] >= 0");
        });
        builder.HasKey(x => new { x.TransactionId, x.LineNumber });
        builder.Property(x => x.Sender).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Recipient).HasMaxLength(80).IsRequired();

        builder.HasOne(x => x.Line).WithOne(x => x.MoneyOrder)
            .HasForeignKey<MoneyOrderEntity>(x => new { x.TransactionId, x.LineNumber }).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PaymentConfiguration : IEntityTypeConfiguration<PaymentEntity>
{
    public void Configure(EntityTypeBuilder<PaymentEntity> builder)
    {
        builder.ToTable("Payments", table =>
        {
            table.HasCheckConstraint("CK_Payments_Method", "[Method] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_Payments_Amount", "[Amount] >= 0");
        });
        builder.HasKey(x => x.TransactionId);
        builder.Property(x => x.TransactionId).ValueGeneratedNever();
        builder.HasOne(x => x.Transaction).WithOne(x => x.Payment)
            .HasForeignKey<PaymentEntity>(x => x.TransactionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class IdentityVerificationConfiguration : IEntityTypeConfiguration<IdentityVerificationEntity>
{
    public void Configure(EntityTypeBuilder<IdentityVerificationEntity> builder)
    {
        builder.ToTable("IdentityVerifications", table =>
        {
            // The column is only 8 characters wide and must look like ****1234, so a full document number cannot fit.
            table.HasCheckConstraint("CK_IdentityVerifications_Masked", "[MaskedNumber] LIKE '****____' AND LEN([MaskedNumber]) = 8");
            table.HasCheckConstraint("CK_IdentityVerifications_Result", "[Result] IN (1, 2, 3)");
            table.HasCheckConstraint("CK_IdentityVerifications_Document", "[DocumentType] IN (1, 2, 3)");
        });
        builder.HasKey(x => x.TransactionId);
        builder.Property(x => x.TransactionId).ValueGeneratedNever();
        builder.Property(x => x.MaskedNumber).HasMaxLength(8).IsUnicode(false).IsRequired();
        builder.HasOne(x => x.Transaction).WithOne(x => x.Verification)
            .HasForeignKey<IdentityVerificationEntity>(x => x.TransactionId).OnDelete(DeleteBehavior.Cascade);
    }
}