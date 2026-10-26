using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

public sealed class Counter : Entity<Guid>
{
    public BranchCode BranchCode { get; }
    public string Label { get; }

    public Counter(Guid id, BranchCode branchCode, string label) : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        BranchCode = branchCode;
        Label = label.Trim();
    }
}
