# ADR-006: Optimistic concurrency with rowversion

Status: accepted (Week 3)

**Context.** Two clerks can sell the last item of a product at the same time, or two managers can adjust the same stock row.

**Decision.** Optimistic concurrency. `BranchStock` and `CounterTransactions` have a `rowversion` column configured as a concurrency token. The repository keeps the tracked row, so the version read at load time is sent in the `WHERE` clause of the update. If another commit changed the row first, `SaveChanges` affects 0 rows and EF Core throws `DbUpdateConcurrencyException`. `UnitOfWork` translates it to the application's `ConcurrencyConflictException`, which `ProblemMapping` answers as **HTTP 409** with a Problem Details body telling the user to reload and retry. The stored procedure `usp_AdjustStock` is naturally safe (the guard is inside the `UPDATE`), and a lost race on the first insert of a stock row (error 2627) is mapped to the same exception.

Rows are always touched in SKU order (`CheckoutService` sorts the stock lines), so two sales with the same items cannot wait for each other in a circle: see `sql/04_deadlock_avoidance.md`.

**Consequences.** No locks are held while a user thinks. A conflict is rare and shows a clear message. The cost is that the second user must repeat the action. The integration test `Two_people_changing_the_same_stock_row_cause_a_concurrency_conflict_for_the_second` loads the same row in two scopes and proves the second commit fails.

**Alternatives.** Pessimistic locking with `UPDLOCK` (rejected: long locks, deadlock risk). Last write wins (rejected: loses stock changes silently).
