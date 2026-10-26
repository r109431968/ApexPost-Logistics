using ApexPost.Domain.Enums;
using ApexPost.Domain.Exceptions;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

public sealed class CounterTransaction : Entity<Guid>
{
    private readonly List<ILineItem> _lines = [];

    public BranchCode BranchCode { get; }
    public Guid CounterId { get; }
    public Guid ClerkId { get; }
    public DateTimeOffset CreatedAt { get; }
    public TransactionStatus Status { get; private set; } = TransactionStatus.Open;
    public PaymentMethod? PaidWith { get; private set; }
    public IdentityVerification? Verification { get; private set; }
    public string? ReversalReason { get; private set; }
    public Money TaxTotal { get; private set; } = Money.Zero;

    public IReadOnlyList<ILineItem> Lines => _lines;
    public Money Subtotal => _lines.Aggregate(Money.Zero, (sum, line) => sum + line.LineTotal);
    public Money Total => Subtotal + TaxTotal;

    private CounterTransaction(Guid id, BranchCode branchCode, Guid counterId, Guid clerkId, DateTimeOffset createdAt)
        : base(id)
    {
        BranchCode = branchCode;
        CounterId = counterId;
        ClerkId = clerkId;
        CreatedAt = createdAt;
    }

    /// <summary>The only way to start a new transaction, so the opening rules cannot be skipped.</summary>
    public static CounterTransaction Open(Branch branch, Counter counter, Staff clerk, DateTimeOffset now)
    {
        if (!branch.IsActive)
            throw new DomainException("A closed branch cannot accept transactions.");
        if (!clerk.IsActive)
            throw new DomainException("Inactive staff cannot process transactions.");
        if (clerk.Role is StaffRole.Auditor or StaffRole.Administrator)
            throw new DomainException("This role cannot process transactions.");
        if (clerk.HomeBranch != branch.Code)
            throw new DomainException("A clerk can only transact at their assigned branch.");
        if (counter.BranchCode != branch.Code)
            throw new DomainException("The counter does not belong to this branch.");

        return new CounterTransaction(Guid.NewGuid(), branch.Code, counter.Id, clerk.Id, now);
    }

    /// <summary>
    /// Rebuilds a saved transaction exactly as it was stored. It deliberately does NOT re-run the business
    /// rules: an old transaction is a historical fact and must not be judged by today's state.
    /// </summary>
    public static CounterTransaction Restore(
        Guid id,
        BranchCode branchCode,
        Guid counterId,
        Guid clerkId,
        DateTimeOffset createdAt,
        TransactionStatus status,
        PaymentMethod? paidWith,
        IdentityVerification? verification,
        string? reversalReason,
        Money taxTotal,
        IEnumerable<ILineItem> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var transaction = new CounterTransaction(id, branchCode, counterId, clerkId, createdAt)
        {
            Status = status,
            PaidWith = paidWith,
            Verification = verification,
            ReversalReason = reversalReason,
            TaxTotal = taxTotal
        };

        transaction._lines.AddRange(lines);
        return transaction;
    }

    public void AddLine(ILineItem line)
    {
        ArgumentNullException.ThrowIfNull(line);
        EnsureStatus(TransactionStatus.Open);
        _lines.Add(line);
    }

    public void AttachVerification(IdentityVerification verification)
    {
        ArgumentNullException.ThrowIfNull(verification);
        EnsureStatus(TransactionStatus.Open);
        Verification = verification;
    }

    /// <param name="taxRule">A delegate, so the tax formula can change without touching this class.</param>
    public void Pay(PaymentMethod method, Money moneyOrderThreshold, Func<ILineItem, Money> taxRule)
    {
        ArgumentNullException.ThrowIfNull(taxRule);
        EnsureStatus(TransactionStatus.Open);
        if (_lines.Count == 0)
            throw new DomainException("A transaction needs at least one line item.");

        var needsVerification = _lines
            .OfType<MoneyOrderLineItem>()
            .Any(order => order.Amount.Amount > moneyOrderThreshold.Amount);

        if (needsVerification && Verification is not { Result: VerificationResult.Passed })
            throw new DomainException("A money order above the threshold needs a successful identity verification.");

        TaxTotal = _lines.Aggregate(Money.Zero, (sum, line) => sum + taxRule(line));
        PaidWith = method;
        Status = TransactionStatus.Paid;
    }

    public void Void(Staff manager, string reason)
    {
        if (Status is not (TransactionStatus.Open or TransactionStatus.Paid))
            throw new DomainException($"A {Status} transaction cannot be voided.");
        EnsureCanReverse(manager, reason);
        Status = TransactionStatus.Voided;
    }

    public void Refund(Staff manager, string reason)
    {
        EnsureStatus(TransactionStatus.Paid);
        EnsureCanReverse(manager, reason);
        Status = TransactionStatus.Refunded;
    }

    private void EnsureCanReverse(Staff manager, string reason)
    {
        if (manager.Role != StaffRole.BranchManager || !manager.IsActive)
            throw new DomainException("Voids and refunds require an active Branch Manager.");
        if (manager.HomeBranch != BranchCode)
            throw new DomainException("A manager can only void or refund at their own branch.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("A reason is required for a void or refund.");

        ReversalReason = reason.Trim();
    }

    private void EnsureStatus(TransactionStatus expected)
    {
        if (Status != expected)
            throw new DomainException($"The transaction is {Status}, expected {expected}.");
    }
}
