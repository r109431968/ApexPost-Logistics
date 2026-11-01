# Deadlock avoidance

A deadlock happens when two transactions each hold a lock the other one needs. SQL Server ends one of them
(error 1205) and the other continues.

Example: one sale touches stock for SKU A then B, and another sale touches B then A, at the same time.
Session 1 locks A and waits for B. Session 2 locks B and waits for A. Neither can move.

## Rules we follow

1. **Same order everywhere.** Code that changes several stock rows changes them in the same order (by SKU).
   Then one session simply waits for the other and nobody waits in a circle.
2. **Short transactions.** A transaction starts right before the writes and ends right after. No user input,
   no HTTP call and no courier lookup happens inside one.
3. **Lock only what is needed.** Updates filter on the key (`BranchCode`, `Sku`), so row locks are taken, not table locks.
4. **Optimistic concurrency for stock.** `BranchStock` has a `rowversion`. Two clerks selling the same item do not hold
   locks while they think. The second save gets `DbUpdateConcurrencyException`, which the web layer turns into HTTP 409.
5. **Retry the whole unit.** The SQL Server execution strategy retries transient failures (including deadlock victims)
   as one unit of work, never half of it.
6. **Reports do not block writers.** The database uses READ_COMMITTED_SNAPSHOT, so readers see the last committed
   value and take no shared locks. We do not use `NOLOCK`, because it can return uncommitted or duplicated rows.

## How to see blocking and deadlocks

`sys.dm_exec_requests` (blocking_session_id) and `sys.dm_tran_locks` show who waits for whom (see `03_blocking_demo.sql`).
Deadlock graphs are in Extended Events (`system_health` session, `xml_deadlock_report`).