# ADR-003: Stored procedures and least privilege

Status: accepted (Week 3)

**Context.** Two reports aggregate many rows and one write must change two tables atomically. Doing the aggregation in the application would move every line of the branch across the network.

**Decision.** Three procedures, created by EF Core migrations (`migrationBuilder.Sql`, `CREATE OR ALTER`), so the database schema and the procedures are versioned together:
1. `usp_DailyBranchSales(@Day, @BranchCode = NULL)`: retail and services revenue and the number of Paid transactions per branch for one day. Services = shipping line totals plus the money order **fee** (not the money order amount).
2. `usp_LowStockReport(@BranchCode = NULL, @CategoryId = NULL, @Threshold = 10)`: optional filters. It uses `OPTION (RECOMPILE)`: with optional parameters one cached plan cannot be good for every combination, and the report runs rarely and on a small table, so a compile per call is cheaper than a bad plan.
3. `usp_AdjustStock(@BranchCode, @Sku, @Delta, @Reason, @ChangedBy, @QuantityAfter OUTPUT)`: updates `BranchStock` and inserts the `StockAdjustments` history row in one transaction (`SET XACT_ABORT ON`, `TRY/CATCH`, `THROW`). The `UPDATE ... WHERE Quantity + @Delta >= 0` refuses to go below zero without a separate read. It creates the stock row when a branch receives an item for the first time.

The application calls them from `SqlReportService` and `SqlCatalogueService` with `Database.SqlQuery<T>($"EXEC ...")` and `ExecuteSqlAsync($"EXEC ...")`. EF Core turns every interpolation hole into a `SqlParameter`; nothing is concatenated. The output parameter is a `SqlParameter` with `ParameterDirection.Output`. Custom errors (50010 to 50013) are translated to domain exceptions in `SqlCatalogueService`.

**Input checks.** `usp_DailyBranchSales` and `usp_LowStockReport` validate their parameters and raise a specific error with `THROW` instead of returning an empty result: 50020 (day is NULL), 50021 (branch code is not like `BR001`), 50022 (threshold is NULL or negative), 50023 (unknown product category). `usp_AdjustStock` already uses 50010-50013. The application never sends such values, so the checks protect anyone who calls the procedures directly; an integration test covers each error number.

**Least privilege.** The application login should not be `db_owner`. `sql/11_least_privilege.sql` shows a role that has `EXECUTE` on the three procedures and `SELECT`/`INSERT`/`UPDATE` only where the application needs them, with no `DELETE` on history tables and only `INSERT`/`SELECT` on `AuditEntries`. Because the procedures and the tables share an owner (ownership chaining), a reporting user needs EXECUTE on `usp_DailyBranchSales` and `usp_LowStockReport` and no table permission at all. Migrations run with a different, more powerful login (used only by `dotnet ef database update` or the idempotent script).

**Consequences.** Faster reports and a safe stock write, at the price of logic in two languages. The rule "services revenue excludes the money order amount" exists in C# and in SQL; an integration test compares both results on the seeded data so they cannot drift apart silently.

**Alternatives.** LINQ aggregation only (kept as the fallback in the base `ReportService`; it is what the unit tests use). Dynamic SQL for optional filters (rejected: injection risk and plan cache pollution).
