using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services;

public sealed class BranchService(IRepository<Branch, BranchCode> branches) : IBranchService
{
    public async Task<IReadOnlyList<Branch>> ListAsync(CancellationToken cancellationToken) =>
        (await branches.ListAsync(cancellationToken)).OrderBy(branch => branch.Code.Value, StringComparer.Ordinal).ToList();

    public Task<Branch?> GetAsync(BranchCode code, CancellationToken cancellationToken) =>
        branches.GetByIdAsync(code, cancellationToken);

    public async Task SetOpenAsync(BranchCode code, bool open, CancellationToken cancellationToken)
    {
        var branch = await branches.GetByIdAsync(code, cancellationToken)
            ?? throw new NotFoundException(nameof(Branch), code);

        if (open)
            branch.Reopen();
        else
            branch.Close();

        await branches.UpdateAsync(branch, cancellationToken);
    }
}
