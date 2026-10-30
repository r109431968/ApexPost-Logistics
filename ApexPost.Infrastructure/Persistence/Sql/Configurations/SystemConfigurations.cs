using ApexPost.Infrastructure.Persistence.Sql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ApexPost.Infrastructure.Persistence.Sql.Configurations;

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntryEntity>
{
    public void Configure(EntityTypeBuilder<AuditEntryEntity> builder)
    {
        builder.ToTable("AuditEntries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        // No foreign keys on purpose: the audit trail must survive even if the referenced row ever changes.
        builder.Property(x => x.Role).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.BranchCode).HasMaxLength(5).IsUnicode(false);
        builder.Property(x => x.Controller).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Operation).HasMaxLength(100);
        builder.Property(x => x.CorrelationId).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(x => x.RouteValuesJson).IsRequired();
        builder.Property(x => x.DetailsJson).IsRequired();
    }
}
