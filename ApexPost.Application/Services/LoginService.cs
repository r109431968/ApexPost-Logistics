using ApexPost.Application.Abstractions;
using ApexPost.Domain.Entities;

namespace ApexPost.Application.Services;

public sealed class LoginService(
    IStaffRepository staff,
    ICredentialStore credentials,
    IPasswordHasher hasher) : ILoginService
{
    public async Task<Staff?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(password);

        var member = await staff.GetByEmailAsync(email, cancellationToken);
        var storedHash = member is null ? null : await credentials.GetHashAsync(member.Id, cancellationToken);

        // The same amount of hashing work happens for unknown accounts, so response time
        // does not reveal whether an email address exists.
        var passwordMatches = hasher.Verify(password, storedHash ?? hasher.DummyHash);

        return member is not null && storedHash is not null && passwordMatches && member.IsActive
            ? member
            : null;
    }
}