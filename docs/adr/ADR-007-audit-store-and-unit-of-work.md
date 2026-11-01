# ADR-007: The audit store does not share the unit of work

Status: accepted (Week 3)

**Context.** `AuditActionFilter` writes one audit entry per audited request. Question: should that write join the request's `DbContext` and unit of work, or use its own?

**Decision.** `SqlAuditStore` is a singleton that creates its own short-lived `DbContext` from `IDbContextFactory` for every append and saves immediately.

Why separate:
1. The audit entry must be written even when the business operation fails and rolls back (a refused refund, a failed login, an access denied). With the shared unit of work the audit row would roll back together with the failed business change.
2. The filter runs after the action has already committed. Staging the entry in the shared context would need a second `SaveChanges` on a context that may be in a faulted state.
3. An audit write is append-only and independent; it never needs to be atomic with business data.

The price: for a successful sale there are two commits (business data, then the audit row). If the process stops between them, the sale exists without its audit row. For a counter system this is acceptable, and it is better than losing the audit of failures. A transactional outbox would close the gap and is listed as future work.

`ICredentialStore` is the opposite case: setting a password hash must be atomic with creating the staff member, so `SqlCredentialStore` stages the change in the shared context and the staff service commits once.

**Consequences.** `DenyAccessAsync` (the access-denied handler) must not fail the response if the audit write fails, so it catches database exceptions. `IAuditStore` still exposes only `AppendAsync` and `ReadRecentAsync`.

**Alternatives.** Shared unit of work (rejected for the reasons above). Writing audit to a file (the Week 2 solution; rejected now because the audit page, filtering and permissions are easier in the database).
