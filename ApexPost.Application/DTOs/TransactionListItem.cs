using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.DTOs;

/// <summary>One row of the transaction list screen. Read model only: it has no behaviour and cannot be saved.</summary>
public sealed record TransactionListItem(
    Guid Id, DateTimeOffset OpenedAt, TransactionStatus Status, int LineCount, Money Total);
