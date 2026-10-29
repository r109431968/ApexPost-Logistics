using ApexPost.Application.DTOs;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Abstractions;

public interface IShippingPricer
{
    Money Price(ShipmentType type, ServiceLevel level, Weight weight, ShippingZone zone);
}

public interface ITaxCalculator
{
    Money TaxFor(ILineItem line);

    Func<ILineItem, Money> AsRule();
}

public interface ICheckoutService
{
    Task<CounterTransaction> OpenAsync(
        BranchCode branchCode, Guid counterId, Guid clerkId, CancellationToken cancellationToken);

    Task<CounterTransaction> AddRetailLineAsync(
        Guid transactionId, Sku sku, int quantity, CancellationToken cancellationToken);

    Task<CounterTransaction> AddShippingLineAsync(
        Guid transactionId, ShipmentType type, Weight weight, string zoneCode, ServiceLevel level,
        CancellationToken cancellationToken);

    Task<CounterTransaction> AddMoneyOrderAsync(
        Guid transactionId, MoneyOrderRequest request, CancellationToken cancellationToken);

    Task<CounterTransaction> AttachVerificationAsync(
        Guid transactionId, DocumentType documentType, string documentNumber, CancellationToken cancellationToken);

    Task<CounterTransaction> PayAsync(
        Guid transactionId, PaymentMethod method, CancellationToken cancellationToken);
}

public interface IReversalService
{
    Task<CounterTransaction> VoidAsync(
        Guid transactionId, Guid managerId, string reason, CancellationToken cancellationToken);

    Task<CounterTransaction> RefundAsync(
        Guid transactionId, Guid managerId, string reason, CancellationToken cancellationToken);
}

public interface ILoginService
{
    /// <summary>Returns the staff member, or null for any failure (unknown email, wrong password, inactive).</summary>
    Task<Staff?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken);
}

public interface IStaffService
{
    Task<IReadOnlyList<Staff>> ListAsync(CancellationToken cancellationToken);

    Task<Staff?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Staff> CreateAsync(StaffRequest request, string password, CancellationToken cancellationToken);

    Task<Staff> UpdateAsync(Guid id, StaffRequest request, CancellationToken cancellationToken);

    Task SetActiveAsync(Guid id, bool active, Guid actorId, CancellationToken cancellationToken);
}

public interface IBranchService
{
    Task<IReadOnlyList<Branch>> ListAsync(CancellationToken cancellationToken);

    Task<Branch?> GetAsync(BranchCode code, CancellationToken cancellationToken);

    Task SetOpenAsync(BranchCode code, bool open, CancellationToken cancellationToken);
}

public interface ICatalogueService
{
    Task<IReadOnlyList<CatalogueRow>> ListAsync(
        BranchCode branch, ProductCategory? category, string? search, CancellationToken cancellationToken);

    Task<StockItem> AdjustStockAsync(
        Guid actorId, Sku sku, int delta, string reason, CancellationToken cancellationToken);
}

public interface ITransactionQueries
{
    Task<CounterTransaction?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<CounterTransaction>> RecentAsync(
        BranchCode branch, int count, CancellationToken cancellationToken);
}
