using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Validation;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Exceptions;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services;

public sealed class StaffService(
    IRepository<Staff, Guid> staff,
    IRepository<Branch, BranchCode> branches,
    ICredentialStore credentials,
    IPasswordHasher hasher,
    IModelValidator validator) : IStaffService
{
    public const int MinimumPasswordLength = 12;

    public async Task<IReadOnlyList<Staff>> ListAsync(CancellationToken cancellationToken) =>
        (await staff.ListAsync(cancellationToken)).OrderBy(member => member.Name, StringComparer.Ordinal).ToList();

    public Task<Staff?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        staff.GetByIdAsync(id, cancellationToken);

    public async Task<Staff> CreateAsync(StaffRequest request, string password, CancellationToken cancellationToken)
    {
        Validate(request);
        if (password is null || password.Length < MinimumPasswordLength)
            throw new DomainException($"A password must have at least {MinimumPasswordLength} characters.");

        var branch = new BranchCode(request.HomeBranch);
        await EnsureBranchExistsAsync(branch, cancellationToken);
        await EnsureEmailIsFreeAsync(request.Email, null, cancellationToken);

        var member = new Staff(Guid.NewGuid(), request.Name, request.Email, request.Role, branch);
        await staff.AddAsync(member, cancellationToken);
        await credentials.SetHashAsync(member.Id, hasher.Hash(password), cancellationToken);
        return member;
    }

    public async Task<Staff> UpdateAsync(Guid id, StaffRequest request, CancellationToken cancellationToken)
    {
        Validate(request);

        var member = await staff.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Staff), id);

        var branch = new BranchCode(request.HomeBranch);
        await EnsureBranchExistsAsync(branch, cancellationToken);
        await EnsureEmailIsFreeAsync(request.Email, id, cancellationToken);

        member.UpdateDetails(request.Name, request.Email, request.Role, branch);
        await staff.UpdateAsync(member, cancellationToken);
        return member;
    }

    public async Task SetActiveAsync(Guid id, bool active, Guid actorId, CancellationToken cancellationToken)
    {
        var member = await staff.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(Staff), id);

        if (!active && id == actorId)
            throw new DomainException("You cannot deactivate your own account.");

        if (active)
            member.Activate();
        else
            member.Deactivate();

        await staff.UpdateAsync(member, cancellationToken);
    }

    private void Validate(StaffRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var failures = validator.Validate(request).ToList();
        if (!Enum.IsDefined(request.Role))
            failures.Add(new ValidationFailure(nameof(StaffRequest.Role), "Role is not valid."));

        if (failures.Count > 0)
            throw new ValidationFailedException(failures);
    }

    private async Task EnsureBranchExistsAsync(BranchCode code, CancellationToken cancellationToken)
    {
        if (await branches.GetByIdAsync(code, cancellationToken) is null)
            throw new NotFoundException(nameof(Branch), code);
    }

    private async Task EnsureEmailIsFreeAsync(string email, Guid? exceptId, CancellationToken cancellationToken)
    {
        var normalised = email.Trim().ToLowerInvariant();
        var all = await staff.ListAsync(cancellationToken);

        if (all.Any(member => member.Email == normalised && member.Id != exceptId))
            throw new DomainException("Another staff member already uses this email address.");
    }
}
