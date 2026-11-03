/* Same text as the latest migrations (RefineDailySalesServices, then ReportProceduresValidateParameters).
   Services = shipping lines + the FEE of money orders (the order amount itself is not revenue).
   Errors: 50020 day is NULL, 50021 branch code is not like BR001. */
CREATE OR ALTER PROCEDURE dbo.usp_DailyBranchSales
    @Day        date,
    @BranchCode varchar(5) = NULL      -- optional: NULL means all branches
AS
BEGIN
    SET NOCOUNT ON;

    IF @Day IS NULL
        THROW 50020, 'The day is required.', 1;
    IF @BranchCode IS NOT NULL AND @BranchCode NOT LIKE 'BR[0-9][0-9][0-9]'
        THROW 50021, 'The branch code must look like BR001.', 1;

    SELECT t.BranchCode,
           @Day AS SaleDate,
           COALESCE(SUM(CASE WHEN l.LineType = 'Retail' THEN l.Quantity * l.UnitPrice END), 0) AS RetailSales,
           COALESCE(SUM(CASE WHEN l.LineType = 'Shipping' THEN l.Quantity * l.UnitPrice
                             WHEN l.LineType = 'MoneyOrder' THEN mo.Fee END), 0) AS ServiceSales,
           COUNT(DISTINCT t.Id) AS Transactions
    FROM CounterTransactions AS t
    JOIN TransactionLines AS l ON l.TransactionId = t.Id
    LEFT JOIN MoneyOrders AS mo ON mo.TransactionId = l.TransactionId AND mo.LineNumber = l.LineNumber
    WHERE t.Status = 2                                   -- Paid
      AND t.OpenedAtUtc >= @Day
      AND t.OpenedAtUtc <  DATEADD(DAY, 1, @Day)
      AND (@BranchCode IS NULL OR t.BranchCode = @BranchCode)
    GROUP BY t.BranchCode
    ORDER BY t.BranchCode;
END;
GO

EXEC dbo.usp_DailyBranchSales @Day = '2026-10-31';
EXEC dbo.usp_DailyBranchSales @Day = '2026-10-31', @BranchCode = 'BR001';

-- Parameter checks (each of these raises an error on purpose):
-- EXEC dbo.usp_DailyBranchSales @Day = NULL;
-- EXEC dbo.usp_DailyBranchSales @Day = '2026-10-31', @BranchCode = 'XX';
