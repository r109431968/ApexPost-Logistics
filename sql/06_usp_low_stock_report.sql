CREATE OR ALTER PROCEDURE dbo.usp_LowStockReport
    @BranchCode varchar(5) = NULL,     -- optional: one branch, or NULL for all
    @CategoryId int        = NULL,     -- optional: one product category, or NULL for all
    @Threshold  int        = 10        -- items with quantity <= threshold are reported
AS
BEGIN
    SET NOCOUNT ON;

    SELECT s.BranchCode, s.Sku, p.Name AS Product, c.Name AS Category, s.Quantity
    FROM BranchStock AS s
    JOIN Products AS p ON p.Sku = s.Sku
    JOIN ProductCategories AS c ON c.Id = p.CategoryId
    WHERE s.Quantity <= @Threshold
      AND (@BranchCode IS NULL OR s.BranchCode = @BranchCode)
      AND (@CategoryId IS NULL OR p.CategoryId = @CategoryId)
    ORDER BY s.Quantity, s.BranchCode, s.Sku
    OPTION (RECOMPILE);
    /* Why RECOMPILE: this is a "catch-all" query, one text that must serve many parameter combinations.
       A cached plan is built for the first values it sees and reused for the rest (parameter sniffing), so a plan
       made for "all branches" can be bad for "one branch" and the other way round.
       RECOMPILE builds a fresh plan on every call using the real values; NULL filters are removed from the plan.
       The cost is a compile on each call. That is fine for a report that runs a few times a day, and it would be
       wrong for a query that runs thousands of times per second. */
END;


EXEC dbo.usp_LowStockReport;
EXEC dbo.usp_LowStockReport @BranchCode = 'BR001', @Threshold = 25;
EXEC dbo.usp_LowStockReport @CategoryId = 1, @Threshold = 25;