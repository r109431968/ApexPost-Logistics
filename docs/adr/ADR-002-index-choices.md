# ADR-002: Index choices

Status: accepted (Week 3)

**Context.** The default indexes are the primary keys, the unique indexes and one index per foreign key. The measured workloads (see `PERFORMANCE.md`) show which extra indexes are worth their cost.

**Decision.** Add these indexes only after measuring the workload with an actual execution plan (baseline plan first, then the index, then the plan again):

|Workload|Index|Reason|
|-|-|-|
|Newest 20 transactions of a branch|`CounterTransactions (BranchCode, OpenedAtUtc DESC)` INCLUDE (`Status`, `TaxTotal`)|Seek on the branch and read the newest rows in index order, no Sort operator|
|Sales of one branch on one day|`CounterTransactions (BranchCode, Status, OpenedAtUtc)` INCLUDE (`TaxTotal`)|Equality columns first, then the range column|
|Top products|`TransactionLines (LineType, Sku)` INCLUDE (`Quantity`, `UnitPrice`)|Covering index: no lookup into the table|
|Latest audit entries|`AuditEntries (TimestampUtc DESC)`|Read 50 rows instead of sorting the table|

The existing single-column index `IX_CounterTransactions_BranchCode` is replaced by the composite one, because a composite index with `BranchCode` first serves the same queries.

**Consequences.** Reads become cheaper; every insert and update on these tables maintains more index pages (the space cost is measured in `PERFORMANCE.md` with the index size query at the end of `sql/08_performance_workloads.sql`). Indexes are not added for workloads that were not measured. Because the main predicate is written as a range on the bare column (`OpenedAtUtc >= @from AND OpenedAtUtc < @to`), it is SARGable and can use the indexes; `CONVERT(date, OpenedAtUtc) = @day` cannot.

**Alternatives.** An index on every column (rejected: write cost, space, no evidence). A filtered index on Paid transactions only (possible later; the reports also read Voided and Refunded).
