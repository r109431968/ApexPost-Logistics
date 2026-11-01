# Performance evidence (Part C)

Rule of this document: **every number and every plan comes from your own SSMS run.** Nothing below is a measured result yet:
each `__ fill in __` slot is waiting for a real value. Do not estimate or copy numbers from the internet.

Database: `ApexPostDb` on `(localdb)\MSSQLLocalDB`. Scripts: [sql/08_performance_workloads.sql](sql/08_performance_workloads.sql) (the workloads),
[sql/09_perf_load_data.sql](sql/09_perf_load_data.sql) (extra data), [sql/10_perf_cleanup.sql](sql/10_perf_cleanup.sql) (removes the extra data).
Screenshots go in `docs/performance/` (create the folder), named as in the tables below.

## 1. How the data was prepared

| Item | Value |
|---|---|
| Seeded transactions (app seeder) | 240 |
| Extra rows added by script 09 (tag `CreatedBy = 'perf-load'`) | __ fill in __ transactions, __ lines, __ audit rows (the final SELECT of script 09 prints them) |
| Skew | about 70 % of the extra transactions belong to BR001 (the result of the last SELECT in script 09 shows the real split: __ fill in __) |
| Machine | __ fill in: CPU, RAM, SSD or HDD __ |
| SQL Server version | __ fill in: `SELECT @@VERSION` __ |

Why extra data: 240 transactions fit in a few pages, so every plan is fast and an index makes no visible difference.

## 2. Method

For each workload: (1) run with **Include Actual Execution Plan** (Ctrl+M) and `SET STATISTICS IO, TIME ON`, **before** the index migration;
(2) apply the index migration (section 4); (3) run the same statement again. Run each statement twice and note the second run
(the first run reads the pages from disk into memory). Record logical reads (not physical reads) and the CPU/elapsed time from the Messages tab.

## 3. Workloads: before and after

| # | Workload (query in script 08) | Plan before (screenshot) | Logical reads before | CPU / elapsed before | Index added | Plan after (screenshot) | Logical reads after | CPU / elapsed after |
|---|---|---|---|---|---|---|---|---|
| 1 | Newest 20 transactions of BR001 (app: `RecentForBranchAsync`) | `w1-before.png` | __ | __ | `(BranchCode, OpenedAtUtc DESC)` INCLUDE (Status, TaxTotal) | `w1-after.png` | __ | __ |
| 2 | One day of sales for BR001 (SARGable range) | `w2-before.png` | __ | __ | `(BranchCode, Status, OpenedAtUtc)` INCLUDE (TaxTotal) | `w2-after.png` | __ | __ |
| 3 | Top 10 products by revenue | `w3-before.png` | __ | __ | `TransactionLines (LineType, Sku)` INCLUDE (Quantity, UnitPrice) | `w3-after.png` | __ | __ |
| 4 | Latest 50 audit entries | `w4-before.png` | __ | __ | `AuditEntries (TimestampUtc DESC)` | `w4-after.png` | __ | __ |

What to write under the table (two or three sentences per workload, in your own words): which operator was the expensive one before
(Table Scan, Clustered Index Scan, Sort, Key Lookup), which operator replaced it (Index Seek, no Sort), and why the logical reads changed.
__ write the explanation after you have seen the real plans __

## 4. Applying the index migration

Do this only **after** the "before" measurements.

1. Open `ApexPost.Infrastructure/Persistence/Sql/Configurations/TransactionConfigurations.cs`. In `CounterTransactionConfiguration.Configure` add:

   ```csharp
   builder.HasIndex(x => new { x.BranchCode, x.OpenedAtUtc })
       .IsDescending(false, true)
       .IncludeProperties(x => new { x.Status, x.TaxTotal });
   builder.HasIndex(x => new { x.BranchCode, x.Status, x.OpenedAtUtc })
       .IncludeProperties(x => x.TaxTotal);
   ```

   In `TransactionLineConfiguration.Configure` add:

   ```csharp
   builder.HasIndex(x => new { x.LineType, x.Sku })
       .IncludeProperties(x => new { x.Quantity, x.UnitPrice });
   ```

2. In `SystemConfigurations.cs`, in `AuditEntryConfiguration.Configure`, add:

   ```csharp
   builder.HasIndex(x => x.TimestampUtc).IsDescending();
   ```

3. Create and apply the migration (from the solution folder):

   ```bat
   dotnet ef migrations add AddReportingIndexes --project ApexPost.Infrastructure --startup-project ApexPost.Web --output-dir Persistence/Sql/Migrations
   dotnet ef database update --project ApexPost.Infrastructure --startup-project ApexPost.Web
   dotnet ef migrations script --idempotent --project ApexPost.Infrastructure --startup-project ApexPost.Web --output database/migrations-idempotent.sql
   ```

   Open the generated migration and check that it only creates the new indexes (and drops `IX_CounterTransactions_BranchCode`, which the new composite index replaces).
4. Run script 08 again and fill the "after" columns.

## 5. A non-SARGable predicate and its fix

Script 08, section "NON-SARGable". Query A uses `CONVERT(date, OpenedAtUtc) = @Day`, query B uses `OpenedAtUtc >= @Day AND OpenedAtUtc < DATEADD(DAY, 1, @Day)`.
Both return the same count; the point is how they find the rows.

| | A: `CONVERT(date, col) = @Day` | B: range on the bare column |
|---|---|---|
| Plan operator on `CounterTransactions` (screenshot `sargable-a.png` / `sargable-b.png`) | __ | __ |
| Logical reads | __ | __ |
| Reason | A function wraps the column, so SQL Server cannot seek on `OpenedAtUtc`: it has to compute the function for each row of the branch. | The column is bare, so the index on `(BranchCode, ..., OpenedAtUtc)` can seek to the start of the range. |

Run it before and after the index migration if you can; the difference is easiest to see after the index exists.

## 6. Estimated versus actual rows (parameter sniffing)

Script 08, section "ESTIMATED vs ACTUAL ROWS". The plan is compiled for `BR005` (few rows) and then reused for `BR001` (most of the rows).

| | Step 1: compiled for BR005 | Step 2: same plan reused for BR001 | Fix: `OPTION (RECOMPILE)` | Fix: `OPTIMIZE FOR UNKNOWN` |
|---|---|---|---|---|
| Estimated rows (hover on the operator) | __ | __ | __ | __ |
| Actual rows | __ | __ | __ | __ |
| Operators in the plan | __ | __ | __ | __ |
| Logical reads | __ | __ | __ | __ |
| Screenshot | `sniff-1.png` | `sniff-2.png` | `sniff-fix-recompile.png` | `sniff-fix-unknown.png` |

Explain in your own words (fill in after the run): the optimiser estimates rows from the **statistics histogram** (output of `DBCC SHOW_STATISTICS`, section at the end of script 08).
A plan is cached with the first parameter value it sees. If that value is rare, the plan is a seek plus key lookups; reused for a very common value, the estimate is far too low, the lookups run
once per row, and the reads explode. `RECOMPILE` fixes it by compiling for every value (cost: compile time per call); `OPTIMIZE FOR UNKNOWN` uses the average density (cost: a plan that is never perfect).
`usp_LowStockReport` uses `OPTION (RECOMPILE)` because its optional filters make one plan wrong for some combinations.

If your numbers do **not** show a difference (a small table, or the optimiser chose a scan both times), say so here and explain what you saw. An honest "no difference, because ..." is better than a made-up number.

## 7. EF Core generated SQL (`ToQueryString`)

The integration test `Generated_sql_for_the_main_queries_is_printed_for_the_performance_document` prints the SQL for three queries.
Run it and read the output:

```bat
dotnet test ApexPost.IntegrationTests --filter "FullyQualifiedName~Generated_sql" --logger "console;verbosity=detailed"
```

Paste the three SQL texts here and, next to each, one sentence: which index of section 3 it uses.

```sql
-- RECENT TRANSACTIONS
__ paste __

-- LOW STOCK
__ paste __

-- TRANSACTIONS WITH LINES (split query)
__ paste __
```

## 8. No N+1 problem

The test `The_number_of_sql_commands_does_not_depend_on_the_number_of_rows_returned` captures the "Executed DbCommand" log lines of EF Core
(`Microsoft.EntityFrameworkCore.Database.Command`) while loading 3 and then 30 transactions, and asserts that the **number of commands is the same**.
Its output (shown with `--logger "console;verbosity=detailed"`):

```
__ paste the two lines "3 requested: ..." and "30 requested: ..." __
```

Development logging also prints every command to the console (`appsettings.Development.json`). Open `/Transactions` in the browser with the app running and count the
`Executed DbCommand` lines for that request: __ fill in __.

## 9. Cost of the indexes

Run the last query of script 08 before and after the migration:

| Index | Size (KB) |
|---|---|
| `IX_CounterTransactions_BranchCode` (before) | __ |
| new `CounterTransactions` indexes (after) | __ |
| new `TransactionLines` index | __ |
| new `AuditEntries` index | __ |

Every insert into these tables now updates more index pages. For a counter system with many reads and a modest number of writes per second this is a good trade;
say in one sentence what you would watch if the write rate grew.

## 10. Cleaning up

Run [sql/10_perf_cleanup.sql](sql/10_perf_cleanup.sql) when you are done, so the demo database is small again. It deletes only the rows tagged `perf-load`.
