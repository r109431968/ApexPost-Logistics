using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Specifications;
using ApexPost.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence.Sql.Repositories;

/// <summary>
/// Shared EF Core plumbing for the aggregate repositories. Add, Update and Delete only stage changes in the
/// DbContext. Nothing is written until IUnitOfWork.SaveChangesAsync runs, so one use case is one commit.
/// </summary>
public abstract class EfRepository<TDomain, TId, TEntity>(ApexPostDbContext db) : IRepository<TDomain, TId>
    where TDomain : Entity<TId>
    where TId : notnull
    where TEntity : class
{
    protected ApexPostDbContext Db { get; } = db;

    /// <summary>The query for this aggregate, with everything the mapper needs already included.</summary>
    protected abstract IQueryable<TEntity> Query();

    /// <summary>Finds the tracked row for an id. A row that is already tracked is reused, so a second load costs no query.</summary>
    protected abstract Task<TEntity?> FindAsync(TId id, CancellationToken cancellationToken);

    protected abstract TDomain ToDomain(TEntity entity);

    protected abstract TEntity ToEntity(TDomain domain);

    protected abstract void Apply(TDomain domain, TEntity entity);

    public async Task<TDomain?> GetByIdAsync(TId id, CancellationToken cancellationToken)
    {
        var entity = await FindAsync(id, cancellationToken);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<TDomain>> ListAsync(CancellationToken cancellationToken)
    {
        var entities = await Query().AsNoTracking().ToListAsync(cancellationToken);
        return entities.Select(ToDomain).ToList();
    }

    /// <summary>Filters in memory, so use it only for small reference tables. Large tables have their own query methods.</summary>
    public async Task<IReadOnlyList<TDomain>> ListAsync(
        ISpecification<TDomain> specification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(specification);
        var all = await ListAsync(cancellationToken);
        return all.Where(specification.IsSatisfiedBy).ToList();
    }

    public Task AddAsync(TDomain entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Db.Set<TEntity>().Add(ToEntity(entity));
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(TDomain entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var row = await FindAsync(entity.Id, cancellationToken)
            ?? throw new NotFoundException(typeof(TDomain).Name, entity.Id);

        Apply(entity, row);
    }

    public async Task DeleteAsync(TId id, CancellationToken cancellationToken)
    {
        var row = await FindAsync(id, cancellationToken);
        if (row is not null)
            Db.Set<TEntity>().Remove(row);
    }
}