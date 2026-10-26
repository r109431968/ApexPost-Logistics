using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

public sealed class Branch : Entity<BranchCode>
{
    private readonly List<Counter> _counters = [];

    public BranchCode Code => Id;
    public string Name { get; private set; }
    public string Region { get; private set; }
    public TimeOnly OpensAt { get; private set; }
    public TimeOnly ClosesAt { get; private set; }
    public bool IsActive { get; private set; }
    public IReadOnlyList<Counter> Counters => _counters;

    public Branch(BranchCode code, string name, string region, TimeOnly opensAt, TimeOnly closesAt)
        : base(code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        if (closesAt <= opensAt)
            throw new ArgumentException("Closing time must be after opening time.", nameof(closesAt));

        Name = name.Trim();
        Region = region.Trim();
        OpensAt = opensAt;
        ClosesAt = closesAt;
        IsActive = true;
    }

    public Counter AddCounter(string label) => AddCounter(Guid.NewGuid(), label);

    /// <summary>Used when a saved branch is loaded, so counters keep their original IDs.</summary>
    public Counter AddCounter(Guid id, string label)
    {
        var counter = new Counter(id, Id, label);
        _counters.Add(counter);
        return counter;
    }

    public void Close() => IsActive = false;
    public void Reopen() => IsActive = true;
}
