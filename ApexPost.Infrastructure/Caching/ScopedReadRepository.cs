using ApexPost.Application.Abstractions;
using ApexPost.Application.Specifications;
using ApexPost.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Infrastructure.Caching;

/// <summary>
/// The catalogue caches live for the whole application (singleton), but the repositories live for one request
/// (scoped, because they share the request's DbContext). This class bridges the two: every read opens its own
/// short scope, so a singleton never holds on to a scoped DbContext. It is read-only.
/// </summary>
public sealed class ScopedReadRepository<T, TId>(IServiceScopeFactory scopes) : IRepository<T, TId>
    where T : Entity<TId>
    where TId : notnull
{
    public async Task<T?> GetByIdAsync(TId id, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await Repository(scope).GetByIdAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await Repository(scope).ListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await Repository(scope).ListAsync(specification, cancellationToken);
    }

    public Task AddAsync(T entity, CancellationToken cancellationToken) => throw ReadOnly();

    public Task UpdateAsync(T entity, CancellationToken cancellationToken) => throw ReadOnly();

    public Task DeleteAsync(TId id, CancellationToken cancellationToken) => throw ReadOnly();

    private static IRepository<T, TId> Repository(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IRepository<T, TId>>();

    private static NotSupportedException ReadOnly() =>
        new("This repository is read-only. Use the scoped repository and the unit of work to write.");
}