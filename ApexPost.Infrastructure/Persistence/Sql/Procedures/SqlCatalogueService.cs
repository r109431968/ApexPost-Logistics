using System.Data;
using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using ApexPost.Application.Services;
using ApexPost.Application.Settings;
using ApexPost.Domain.Entities;
using ApexPost.Domain.Exceptions;
using ApexPost.Domain.ValueObjects;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ApexPost.Infrastructure.Persistence.Sql.Procedures;

/// <summary>
/// The catalogue service used by the web application. The business rules (who may adjust stock, no zero change, a reason
/// is required) stay in the base class. Only the write is different: it calls the usp_AdjustStock stored procedure,
/// which changes BranchStock and writes the StockAdjustments history row inside one SQL transaction.
/// </summary>
public sealed class SqlCatalogueService : CatalogueService
{
    // Error numbers raised by THROW inside usp_AdjustStock, and the SQL Server number for a duplicate primary key.
    private const int BelowZero = 50013;
    private const int InvalidInput10 = 50010;
    private const int InvalidInput11 = 50011;
    private const int DuplicateKey = 2627;
    private const int MaxChangedByLength = 100;

    private readonly ApexPostDbContext db;

    public SqlCatalogueService(
        IProductCatalog products,
        IRepository<StockItem, string> stock,
        IRepository<Staff, Guid> staff,
        StockSettings stockSettings,
        CounterEvents events,
        IClock clock,
        IUnitOfWork unitOfWork,
        ApexPostDbContext db)
        : base(products, stock, staff, stockSettings, events, clock, unitOfWork)
    {
        this.db = db;
    }

    protected override async Task<StockItem> ApplyAdjustmentAsync(
        Staff actor, Sku sku, int delta, string reason, CancellationToken cancellationToken)
    {
        var quantityAfter = new SqlParameter("@QuantityAfter", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var changedBy = actor.Email.Length > MaxChangedByLength ? actor.Email[..MaxChangedByLength] : actor.Email;

        try
        {
            // Every value is a SqlParameter (EF Core builds them from the interpolated string); OUTPUT returns the new quantity.
            await db.Database.ExecuteSqlAsync(
                $"""
                 EXEC dbo.usp_AdjustStock
                     @BranchCode = {actor.HomeBranch.Value}, @Sku = {sku.Value}, @Delta = {delta},
                     @Reason = {reason}, @ChangedBy = {changedBy}, @QuantityAfter = {quantityAfter} OUTPUT
                 """,
                cancellationToken);
        }
        catch (SqlException error) when (error.Number == BelowZero)
        {
            var available = await db.BranchStock.AsNoTracking()
                .Where(row => row.BranchCode == actor.HomeBranch && row.Sku == sku)
                .Select(row => row.Quantity)
                .FirstOrDefaultAsync(cancellationToken);

            throw new InsufficientStockException(sku, available, -delta);
        }
        catch (SqlException error) when (error.Number is InvalidInput10 or InvalidInput11)
        {
            throw new DomainException(error.Message);
        }
        catch (SqlException error) when (error.Number == DuplicateKey)
        {
            // Two managers received the same new item at the same moment: the second insert lost the race.
            throw new ConcurrencyConflictException(error);
        }

        return new StockItem(actor.HomeBranch, sku, (int)quantityAfter.Value!);
    }
}
