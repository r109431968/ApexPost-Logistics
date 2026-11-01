# ADR-008: Database errors are translated once, then mapped to Problem Details

Status: accepted (Week 3)

**Context.** Controllers and middleware should not know about `DbUpdateConcurrencyException` or SQL Server error numbers.

**Decision.** `UnitOfWork` (Infrastructure) catches EF Core and `SqlException` errors and throws application exceptions: concurrency to `ConcurrencyConflictException`, unique index/constraint violations (2601, 2627) to `DuplicateValueException`. `SqlCatalogueService` translates the procedure's custom numbers. `ProblemMapping` (Web) is the single table that turns an exception into an HTTP status, used by both the exception middleware and the MVC exception filter:

|Exception|Status|
|-|-|
|`ValidationFailedException`|400|
|`DomainException` (business rule)|400|
|`NotFoundException`|404|
|`ConcurrencyConflictException`|409|
|`DuplicateValueException`|409|
|`InsufficientStockException`|409 (a conflict with the current stock, a more precise answer than 400)|
|courier timeout / failure|504 / 502|

The body is Problem Details with a trace ID; stack traces are shown only in Development.

**Consequences.** The Web project has no reference to EF Core types in its error handling. Tests call `ProblemMapping.TryMap` directly for the 400/404/409 cases. Week 2 returned 422 for business rules; Week 3 uses 400 as the lab requires.

**Alternatives.** Catch SQL exceptions in controllers (rejected: duplicated and layer-breaking).
