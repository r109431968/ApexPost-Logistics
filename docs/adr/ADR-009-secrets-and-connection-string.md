# ADR-009: Connection string and secrets

Status: accepted (Week 3)

**Context.** No password or key may be stored in the repository.

**Decision.** The connection string name is `ConnectionStrings:ApexPostDb`. It is **not** in `appsettings.json`. In development it is set with `dotnet user-secrets` and uses LocalDB with Windows authentication (`Server=(localdb)\MSSQLLocalDB;Database=ApexPostDb;Trusted_Connection=True;TrustServerCertificate=True`), so it contains no password at all. In other environments it comes from an environment variable (`ConnectionStrings__ApexPostDb`) or a secret store. The application fails at start-up with a clear message if it is missing. The integration tests build their own connection string for a disposable database `ApexPostDb_Test_<guid>` on LocalDB (Windows authentication) and drop it afterwards.

`Database:ApplyMigrationsOnStartup` is true only in `appsettings.Development.json`. Production applies the reviewed idempotent script `database/migrations-idempotent.sql`. The application never calls `EnsureCreated`.

**Consequences.** A new developer must run one `user-secrets set` command (see README). Nothing sensitive is committed.

**Alternatives.** Connection string in `appsettings.json` (rejected: ends up in git and mixes environments).
