using ApexPost.Application.Abstractions;
using ApexPost.Application.DTOs;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Settings;
using ApexPost.Application.Validation;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.Events;
using ApexPost.Domain.Exceptions;
using ApexPost.Domain.Rules;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services;

public sealed class CheckoutService(
    IRepository<Branch, BranchCode> branches,
    IRepository<Staff, Guid> staff,
    IProductCatalog products,
    IRepository<StockItem, string> stock,
    IZoneCatalog zones,
    IRepository<CounterTransaction, Guid> transactions,
    IShippingPricer pricer,
    ITaxCalculator tax,
    WeightLimitRule weightRule,
    MoneyOrderSettings moneyOrderSettings,
    StockSettings stockSettings,
    CounterEvents events,
    IModelValidator validator,
    IIdentityVerificationService verificationService,
    IClock clock) : ICheckoutService
{
    public async Task<CounterTransaction> OpenAsync(
        BranchCode branchCode, Guid counterId, Guid clerkId, CancellationToken cancellationToken)
    {
        var branch = await branches.GetByIdAsync(branchCode, cancellationToken)
            ?? throw new NotFoundException(nameof(Branch), branchCode);
        var counter = branch.Counters.FirstOrDefault(c => c.Id == counterId)
            ?? throw new NotFoundException(nameof(Counter), counterId);
        var clerk = await staff.GetByIdAsync(clerkId, cancellationToken)
            ?? throw new NotFoundException(nameof(Staff), clerkId);

        var transaction = CounterTransaction.Open(branch, counter, clerk, clock.UtcNow);
        await transactions.AddAsync(transaction, cancellationToken);
        return transaction;
    }

    public async Task<CounterTransaction> AddRetailLineAsync(
        Guid transactionId, Sku sku, int quantity, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        var transaction = await LoadAsync(transactionId, cancellationToken);
        var product = await products.GetAsync(sku, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), sku);

        var alreadyInBasket = transaction.Lines
            .OfType<RetailLineItem>()
            .Where(line => line.Sku == sku)
            .Sum(line => line.Quantity);

        var item = await stock.GetByIdAsync(StockItem.KeyFor(transaction.BranchCode, sku), cancellationToken);
        var available = item?.Quantity ?? 0;
        var required = alreadyInBasket + quantity;
        if (required > available)
            throw new InsufficientStockException(sku, available, required);

        transaction.AddLine(new RetailLineItem(sku, product.Name, quantity, product.UnitPrice, product.TaxCategory));
        await transactions.UpdateAsync(transaction, cancellationToken);
        return transaction;
    }

    public async Task<CounterTransaction> AddShippingLineAsync(
        Guid transactionId, ShipmentType type, Weight weight, string zoneCode, ServiceLevel level,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zoneCode);

        var transaction = await LoadAsync(transactionId, cancellationToken);
        var zone = await zones.GetAsync(zoneCode, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingZone), zoneCode);

        weightRule.EnsureWithinLimit(type, level, weight);
        var price = pricer.Price(type, level, weight, zone);

        transaction.AddLine(new ShippingLineItem(type, weight, zone.Code, level, price));
        await transactions.UpdateAsync(transaction, cancellationToken);
        return transaction;
    }

    public async Task<CounterTransaction> AddMoneyOrderAsync(
        Guid transactionId, MoneyOrderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var failures = validator.Validate(request);
        if (failures.Count > 0)
            throw new ValidationFailedException(failures);

        var transaction = await LoadAsync(transactionId, cancellationToken);

        var amount = new Money(request.Amount);
        var fee = new Money(Math.Max(moneyOrderSettings.MinimumFee, amount.Amount * moneyOrderSettings.FeePercent));

        transaction.AddLine(new MoneyOrderLineItem(amount, fee, request.Sender, request.Recipient));
        await transactions.UpdateAsync(transaction, cancellationToken);
        return transaction;
    }

    public async Task<CounterTransaction> AttachVerificationAsync(
     Guid transactionId, DocumentType documentType, string documentNumber, CancellationToken cancellationToken)
    {
        var transaction = await LoadAsync(transactionId, cancellationToken);

        // The system decides the result. The clerk only supplies the document.
        var result = await verificationService.VerifyAsync(documentType, documentNumber, cancellationToken);

        // The full document number is only used to build the masked value. It is never stored or logged.
        var verification = IdentityVerification.Create(documentType, documentNumber, result, clock.UtcNow);
        transaction.AttachVerification(verification);

        await transactions.UpdateAsync(transaction, cancellationToken);
        return transaction;
    }

    public async Task<CounterTransaction> PayAsync(
        Guid transactionId, PaymentMethod method, CancellationToken cancellationToken)
    {
        // The lock is per branch. The branch is read first, then the transaction is loaded again inside the
        // lock, so the stock check always sees fresh data.
        var branch = (await LoadAsync(transactionId, cancellationToken)).BranchCode;
        var gate = StockGate.For(branch);

        await gate.WaitAsync(cancellationToken);
        try
        {
            var transaction = await LoadAsync(transactionId, cancellationToken);

            var needed = transaction.Lines
                .OfType<RetailLineItem>()
                .GroupBy(line => line.Sku)
                .Select(group => (Sku: group.Key, Quantity: group.Sum(line => line.Quantity)))
                .ToList();

            // 1. Check stock first, so nothing changes if it is short.
            var reservations = new List<(StockItem Item, int Quantity)>();
            foreach (var (sku, quantity) in needed)
            {
                var item = await stock.GetByIdAsync(StockItem.KeyFor(transaction.BranchCode, sku), cancellationToken);
                if (item is null || item.Quantity < quantity)
                    throw new InsufficientStockException(sku, item?.Quantity ?? 0, quantity);

                reservations.Add((item, quantity));
            }

            // 2. Domain rules (empty basket, money order verification, status) run before stock moves.
            var threshold = new Money(moneyOrderSettings.IdentityVerificationThreshold);
            transaction.Pay(method, threshold, tax.AsRule());

            // 3. Reduce stock and save.
            var lowStock = new List<StockLowEvent>();
            foreach (var (item, quantity) in reservations)
            {
                item.Remove(quantity);
                await stock.UpdateAsync(item, cancellationToken);

                if (item.Quantity <= stockSettings.LowStockThreshold)
                    lowStock.Add(new StockLowEvent(
                        item.BranchCode, item.Sku, item.Quantity, stockSettings.LowStockThreshold, clock.UtcNow));
            }

            await transactions.UpdateAsync(transaction, cancellationToken);

            // 4. Tell the rest of the system, only after everything is saved.
            var now = clock.UtcNow;
            events.Publish(new TransactionCompletedEvent(
                transaction.Id, transaction.BranchCode, transaction.ClerkId, transaction.Total, now));

            var verified = transaction.Verification is { Result: VerificationResult.Passed };
            foreach (var order in transaction.Lines.OfType<MoneyOrderLineItem>())
                events.Publish(new MoneyOrderIssuedEvent(
                    transaction.Id, transaction.BranchCode, order.Amount, verified, now));

            foreach (var alert in lowStock)
                events.Publish(alert);

            return transaction;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<CounterTransaction> LoadAsync(Guid transactionId, CancellationToken cancellationToken) =>
        await transactions.GetByIdAsync(transactionId, cancellationToken)
            ?? throw new NotFoundException(nameof(CounterTransaction), transactionId);
}
