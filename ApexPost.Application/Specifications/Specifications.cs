using ApexPost.Domain.Entities;
using ApexPost.Domain.Enums;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Specifications;

public interface ISpecification<in T>
{
    bool IsSatisfiedBy(T item);
}

public sealed class PredicateSpecification<T>(Func<T, bool> predicate) : ISpecification<T>
{
    public bool IsSatisfiedBy(T item) => predicate(item);

    public PredicateSpecification<T> And(ISpecification<T> other) =>
        new(item => IsSatisfiedBy(item) && other.IsSatisfiedBy(item));
}

public static class TransactionSpecs
{
    public static DateOnly DayOf(DateTimeOffset moment) => DateOnly.FromDateTime(moment.UtcDateTime);

    public static PredicateSpecification<CounterTransaction> ForBranch(BranchCode branch) =>
        new(t => t.BranchCode == branch);

    public static PredicateSpecification<CounterTransaction> PaidOnDay(BranchCode branch, DateOnly day) =>
        new(t => t.BranchCode == branch
                 && t.Status == TransactionStatus.Paid
                 && DayOf(t.CreatedAt) == day);

    public static PredicateSpecification<CounterTransaction> PaidBetween(
        DateOnly fromDay, DateOnly toDay, BranchCode? branch) =>
        new(t => t.Status == TransactionStatus.Paid
                 && DayOf(t.CreatedAt) >= fromDay
                 && DayOf(t.CreatedAt) <= toDay
                 && (branch is null || t.BranchCode == branch));

    public static PredicateSpecification<CounterTransaction> WithMoneyOrders(BranchCode? branch) =>
        new(t => (branch is null || t.BranchCode == branch)
                 && t.Lines.OfType<MoneyOrderLineItem>().Any());

    public static PredicateSpecification<CounterTransaction> CreatedOnDay(BranchCode branch, DateOnly day) =>
        new(t => t.BranchCode == branch && DayOf(t.CreatedAt) == day);
}

public static class StockSpecs
{
    public static PredicateSpecification<StockItem> ForBranch(BranchCode branch) =>
        new(item => item.BranchCode == branch);

    public static PredicateSpecification<StockItem> LowStock(BranchCode branch, int threshold) =>
        new(item => item.BranchCode == branch && item.Quantity <= threshold);
}
