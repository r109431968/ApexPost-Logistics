using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Abstractions;

// Specific repositories, one per aggregate. They stage changes only (Add, Update, Delete never save);
// IUnitOfWork.SaveChangesAsync does the commit. No method returns IQueryable.

public interface IBranchRepository : IRepository<Branch, BranchCode>;

public interface IProductRepository : IRepository<Product, Sku>;

public interface IZoneRepository : IRepository<ShippingZone, string>;

public interface IStaffRepository : IRepository<Staff, Guid>
{
    Task<Staff?> GetByEmailAsync(string email, CancellationToken cancellationToken);
}

public interface IStockRepository : IRepository<StockItem, string>
{
    Task<IReadOnlyList<StockItem>> ListForBranchAsync(BranchCode branch, CancellationToken cancellationToken);
}

public interface ICounterTransactionRepository : IRepository<CounterTransaction, Guid>
{
    Task<IReadOnlyList<CounterTransaction>> RecentForBranchAsync(
        BranchCode branch, int count, CancellationToken cancellationToken);
}