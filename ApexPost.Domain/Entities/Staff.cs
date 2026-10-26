using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Domain.Entities;

public sealed class Staff : Entity<Guid>
{
    public string Name
    {
        get;
        private set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value.Trim();
        }
    }

    public string Email
    {
        get;
        private set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            var email = value.Trim();
            if (!email.Contains('@') || !email.Contains('.'))
                throw new ArgumentException("Email is not valid.", nameof(value));
            field = email.ToLowerInvariant();
        }
    }

    public StaffRole Role { get; private set; }
    public BranchCode HomeBranch { get; private set; }
    public bool IsActive { get; private set; }

    public Staff(Guid id, string name, string email, StaffRole role, BranchCode homeBranch)
        : base(id)
    {
        Name = name;
        Email = email;
        Role = role;
        HomeBranch = homeBranch;
        IsActive = true;
    }

    public void UpdateDetails(string name, string email, StaffRole role, BranchCode homeBranch)
    {
        Name = name;
        Email = email;
        Role = role;
        HomeBranch = homeBranch;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
