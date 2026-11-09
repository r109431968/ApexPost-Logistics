using ApexPost.Application.Specifications;
using ApexPost.Domain.Entities;

namespace ApexPost.Application.Abstractions;

/// <summary>
/// Generic repository. The constraint means only types that have an Id can be stored. The application layer sees only this
/// interface; the implementation (EF Core and SQL Server) lives in the Infrastructure project and can be swapped without
/// touching the business rules.
/// </summary>
public interface IRepository<T, in TId>
    where T : Entity<TId>
    where TId : notnull
{
    Task<T?> GetByIdAsync(TId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken);

    Task AddAsync(T entity, CancellationToken cancellationToken);

    Task UpdateAsync(T entity, CancellationToken cancellationToken);

    Task DeleteAsync(TId id, CancellationToken cancellationToken);
}
