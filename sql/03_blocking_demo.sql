/* Blocking demo. Three query windows (tabs) on ApexPostDb. Do not run this file as a whole: run each block in its own tab.

   Note: this database has READ_COMMITTED_SNAPSHOT ON, so a plain SELECT is never blocked by a writer
   (it reads the last committed value). Writers still block each other, which is the real case:
   two clerks changing the stock of the same item at the same time. */
USE ApexPostDb;
GO

/* ---- TAB A (writer): run this and leave the tab open. The transaction stays open and holds an update lock. ---- */
BEGIN TRANSACTION;
UPDATE BranchStock SET Quantity = Quantity, ModifiedAtUtc = SYSUTCDATETIME()
WHERE BranchCode = 'BR001' AND Sku = 'STA-0010';

/* ---- TAB B (second writer): this waits (blocked) until tab A commits or rolls back. ---- */
UPDATE BranchStock SET Quantity = Quantity, ModifiedAtUtc = SYSUTCDATETIME()
WHERE BranchCode = 'BR001' AND Sku = 'STA-0010';

/* ---- TAB C (observer): run while tab B waits. Shows who blocks whom. ---- */
SELECT r.session_id, r.blocking_session_id, r.wait_type, r.wait_time AS wait_ms, st.text AS sql_text
FROM sys.dm_exec_requests AS r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) AS st
WHERE r.blocking_session_id <> 0;
/* Result seen: session 82 (tab B) blocked by session 73 (tab A), wait type LCK_M_U (waiting for an update lock). */

/* ---- TAB A again: release the lock. Tab B finishes at once. ---- */
ROLLBACK TRANSACTION;