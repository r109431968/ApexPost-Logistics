# ADR-004: Repositories stage changes, IUnitOfWork commits

Status: accepted (Week 3)

**Context.** A sale changes the transaction, its lines, the payment and the stock. If each repository saved by itself, a failure half-way would leave a paid sale without the stock change.

**Decision.**
* Repositories (`IRepository<T,TId>` and the specific ones) never call `SaveChanges`. `AddAsync`, `UpdateAsync`, `DeleteAsync` only change the tracked `DbContext`.
* `IUnitOfWork.SaveChangesAsync` is the single commit point. Each use case calls it once (`SpyUnitOfWork` in the unit tests counts the calls).
* The `DbContext` is scoped, and every repository and the unit of work receive the same instance.
* `IUnitOfWork.ExecuteInTransactionAsync` is used only when one use case mixes `SaveChanges` with a stored procedure or a set-based update. It wraps the work in the SQL Server **execution strategy** and an explicit transaction, so a retry repeats the whole unit.
* Repositories return domain objects or read models, never `IQueryable`. Reads use `AsNoTracking`; collections that are loaded together use `AsSplitQuery`, to avoid a cartesian explosion.
* The product price revision uses `ExecuteUpdateAsync` (one `UPDATE` per product, no loading) inside `ExecuteInTransactionAsync`, then clears the catalogue cache.
* Do not run two queries at the same time on one scoped `DbContext` (`Task.WhenAll`): it throws. `ReportService.DashboardAsync` awaits its queries one after another.

**Consequences.** All-or-nothing sales. Tests can fake the whole persistence layer. The price is that a repository method cannot "save quickly" for a caller; this is intended.

**Alternatives.** Repositories that save (rejected: no atomic use case). Using `DbContext` directly in services (rejected: services would depend on EF Core and could not be unit tested with fakes).
