using System.Text.Json;
using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Infrastructure.Persistence.Sql.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence.Sql.Stores;

/// <summary>
/// Append-only audit trail in the AuditEntries table. It uses its OWN DbContext from the factory, so an audit
/// write is a separate commit: it can never be rolled back with a failed sale, and a failing audit write can
/// never undo a sale that already succeeded.
/// </summary>
public sealed class SqlAuditStore(IDbContextFactory<ApexPostDbContext> factory) : IAuditStore
{
    public async Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        db.AuditEntries.Add(new AuditEntryEntity
        {
            Id = entry.Id,
            TimestampUtc = entry.Timestamp.UtcDateTime,
            StaffId = entry.StaffId,
            Role = entry.Role,
            BranchCode = entry.BranchCode,
            Controller = entry.Controller,
            Action = entry.Action,
            Operation = entry.Operation,
            RouteValuesJson = JsonSerializer.Serialize(entry.RouteValues),
            DetailsJson = JsonSerializer.Serialize(entry.Details),
            StatusCode = entry.StatusCode,
            DurationMs = entry.DurationMs,
            CorrelationId = entry.CorrelationId
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Returns up to <paramref name="count"/> entries, newest first.</summary>
    public async Task<IReadOnlyList<AuditEntry>> ReadRecentAsync(int count, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var rows = await db.AuditEntries.AsNoTracking()
            .OrderByDescending(row => row.TimestampUtc)
            .Take(count)
            .ToListAsync(cancellationToken);

        return rows.Select(ToEntry).ToList();
    }

    private static AuditEntry ToEntry(AuditEntryEntity row) => new()
    {
        Id = row.Id,
        Timestamp = new DateTimeOffset(DateTime.SpecifyKind(row.TimestampUtc, DateTimeKind.Utc)),
        StaffId = row.StaffId,
        Role = row.Role,
        BranchCode = row.BranchCode,
        Controller = row.Controller,
        Action = row.Action,
        Operation = row.Operation,
        RouteValues = JsonSerializer.Deserialize<Dictionary<string, string>>(row.RouteValuesJson) ?? new Dictionary<string, string>(),
        Details = JsonSerializer.Deserialize<Dictionary<string, string>>(row.DetailsJson) ?? new Dictionary<string, string>(),
        StatusCode = row.StatusCode,
        DurationMs = row.DurationMs,
        CorrelationId = row.CorrelationId
    };
}

/// <summary>
/// Password hashes live in the Staff table (PasswordHash column). Only hashes are stored, never passwords.
/// Setting a hash stages the change in the shared DbContext, so creating a staff member and their hash
/// are saved by the same unit of work.
/// </summary>
public sealed class SqlCredentialStore(ApexPostDbContext db) : ICredentialStore
{
    public Task<string?> GetHashAsync(Guid staffId, CancellationToken cancellationToken) =>
        db.Staff.AsNoTracking()
            .Where(member => member.Id == staffId)
            .Select(member => member.PasswordHash)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task SetHashAsync(Guid staffId, string hash, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        var member = db.Staff.Local.FirstOrDefault(row => row.Id == staffId)
            ?? await db.Staff.FirstOrDefaultAsync(row => row.Id == staffId, cancellationToken)
            ?? throw new InvalidOperationException($"Staff member '{staffId}' does not exist.");

        member.PasswordHash = hash;
    }
}