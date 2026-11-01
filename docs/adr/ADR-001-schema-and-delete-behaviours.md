# ADR-001: Table design, keys, constraints and delete behaviours

Status: accepted (Week 3)

**Context.** The Week 2 JSON files had no rules of their own: anything could be written. The database should refuse impossible data even if application code has a bug.

**Decision.**
* Natural keys where the business already has one: `Branches.BranchCode`, `Products.Sku`, `ShippingZones.Code`. Surrogate keys (`uniqueidentifier`, `bigint identity`) for transactions, staff, audit rows and the stock adjustment history. `BranchStock` has the composite key (BranchCode, Sku); `TransactionLines` has (TransactionId, LineNumber).
* Check constraints: `Quantity >= 0`, `UnitPrice >= 0`, `Delta <> 0`, `Status IN (1,2,3,4)`, retail lines must have a SKU and a description, and `IdentityVerifications.MaskedNumber LIKE '****____'` with length 8, so a full document number cannot be stored.
* Unique indexes: `Staff.Email`, `ProductCategories.Name`, `Roles.Name`, (`Counters.BranchCode`, `Label`).
* Delete behaviour: **Restrict** for master data (branch, product, zone, staff, category). History must not disappear or be orphaned; a branch is closed (`IsActive = 0`), not deleted. **Cascade** for rows that cannot exist without their transaction: lines, payment, shipment, money order, identity verification.
* Money is `decimal(18,2)`. Times are `datetime2` in UTC. Every table has `CreatedAtUtc`, `CreatedBy`, `ModifiedAtUtc` as shadow properties.

**Consequences.** Deleting a branch that has transactions fails with a foreign key error instead of destroying history. Deleting a transaction removes its children in one statement (this is also how `sql/10_perf_cleanup.sql` removes test data). The same rules exist twice (domain and database); the domain gives friendly messages, the database is the last guard.

**Alternatives.** Cascade everywhere (rejected: one wrong delete could erase a branch's history). Soft delete flags on every table (rejected: more code, and only branches and staff need to be switched off).
