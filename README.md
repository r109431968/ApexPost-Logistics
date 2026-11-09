# ApexPost Counter Platform (Week 3: SQL Server)

ASP.NET Core MVC application (.NET 10, C# 14) for ApexPost branches: retail sales, parcel and letter shipping,
money orders and identity verification from one counter. Data lives in **SQL Server** (Entity Framework Core 10) behind the same
application interfaces that Week 2 used for JSON files, so the business rules did not change.

* Design, ER diagram and architecture: [DESIGN.md](DESIGN.md)
* Decisions (9 records): [docs/adr](docs/adr)
* Index, execution-plan and N+1 evidence: [PERFORMANCE.md](PERFORMANCE.md)
* Reporting SQL, transaction/blocking demos, stored procedures, performance and least-privilege scripts: [sql](sql)
* Idempotent migration script: [database/migrations-idempotent.sql](database/migrations-idempotent.sql)
* A sample of the audit log: [AUDIT-SAMPLE.json](AUDIT-SAMPLE.json)

## Solution layout

|Project|Purpose|
|-|-|
|`ApexPost.Domain`|Entities, value objects (Money, Weight, Address, BranchCode, Sku, IdentityVerification), enums, rules, events. No dependencies.|
|`ApexPost.Application`|Use cases (checkout, reversal, reports, login, staff, price revision...), interfaces (ports) including `IRepository` and `IUnitOfWork`, specifications, validation, extension members.|
|`ApexPost.Infrastructure`|EF Core `ApexPostDbContext`, entity configurations, migrations, repositories, `UnitOfWork`, stored-procedure services, SQL audit and credential stores, seeding, cache, simulated courier.|
|`ApexPost.Web`|MVC controllers and views, middleware, filters, composition root (`Program.cs`).|
|`ApexPost.Api`|Week 4 REST API for shipments and scans (`/api/shipments`), built on the same database, unit of work and services. See [API-NOTES.md](API-NOTES.md). Run: `dotnet run --project ApexPost.Api`.|
|`ApexPost.UnitTests`|xUnit tests with in-memory fakes and a spy unit of work (business rules, pricing, tax, price revision, cache, courier).|
|`ApexPost.IntegrationTests`|`WebApplicationFactory` tests against a **disposable SQL Server test database** (pipeline, security, audit, seeding, transactions, concurrency, N+1, stored procedures).|

## Run it

Needs the .NET 10 SDK, SQL Server **LocalDB** (installed with Visual Studio) and a trusted HTTPS development certificate
(`dotnet dev-certs https --trust`, once). `dotnet tool install --global dotnet-ef` once, if you want to run migrations by hand.

1. **Set the secrets** (they are never stored in the repository). Run `setup-secrets.bat`, or by hand:

   ```bat
   dotnet user-secrets set "ConnectionStrings:ApexPostDb" "Server=(localdb)\MSSQLLocalDB;Database=ApexPostDb;Trusted_Connection=True;TrustServerCertificate=True" --project ApexPost.Web
   dotnet user-secrets set "Seed:DemoPassword" "<a password with at least 12 characters>" --project ApexPost.Web
   dotnet user-secrets set "Integration:ApiKeys:CourierPartner" "<a long random key>" --project ApexPost.Web
   ```

   The connection string uses Windows authentication, so it contains no password.

2. **Start the web app**:

   ```bat
   dotnet run --project ApexPost.Web --launch-profile https
   ```

   Open https://localhost:7141. In Development the app applies the migrations (`Database:ApplyMigrationsOnStartup`) and fills an empty database with
   6 branches, 26 staff, 30+ products, 10 zones and 240 transactions. To start from scratch: `dotnet ef database drop --project ApexPost.Infrastructure --startup-project ApexPost.Web`, then run the app again.

3. **Migrations by hand** (optional):

   ```bat
   dotnet ef database update --project ApexPost.Infrastructure --startup-project ApexPost.Web
   dotnet ef migrations script --idempotent --project ApexPost.Infrastructure --startup-project ApexPost.Web --output database/migrations-idempotent.sql
   ```

   Production uses the idempotent script; the application never calls `EnsureCreated`.

4. **Tests**: `dotnet test ApexPost.slnx`. The integration tests create `ApexPostDb_Test_<guid>` on LocalDB, fill it through the real migrations and seeder, and drop it afterwards
   (set `APEXPOST_TEST_SERVER` to use another SQL Server with Windows authentication). They never touch `ApexPostDb`.

   **Week 2 business-rule tests.** The Week 2 domain and service tests run against the same rules as before. Compared with the Week 2 branch
   the only edit to an existing business-rule test is one argument in `MoneyOrderVerificationTests` (`AttachVerificationAsync` no longer takes
   a `VerificationResult`, because the system now decides the result, so a clerk cannot force a pass). The file-store tests in
   `InfrastructureTests` were removed with the JSON repositories they tested; SQL Server persistence is covered by `SqlPersistenceTests` instead.
   The new tests are additions: `CommitCountTests` (one commit or none), `ConcurrencyHttpTests` (stock conflict gives HTTP 409), and the SQL tests.

5. **Windows program (.exe)**: `publish.bat` creates `publish\ApexPost.Web.exe`; `run-published.bat` starts it (set the connection string as an environment variable `ConnectionStrings__ApexPostDb`).

## Demo accounts

Every seeded account uses the demo password you set in step 1.

|Role|Email|Home branch|Can do|
|-|-|-|-|
|Counter Clerk|`clerk1.br001@apexpost.test`|BR001|Sell, ship, money orders, identity checks at BR001|
|Branch Manager|`manager.br001@apexpost.test`|BR001|Everything a clerk does, plus void, refund, stock adjustment, reports|
|Auditor|`auditor1@apexpost.test`|BR001|Read-only: reports for all branches, money order audit view, audit log|
|Administrator|`admin1@apexpost.test`|BR001|Staff and branch administration, audit log, reports. Does not process sales|
|Inactive clerk|`clerk4.br002@apexpost.test`|BR002|Cannot sign in (demonstrates the inactive-staff rule)|
|Manager of a closed branch|`manager.br006@apexpost.test`|BR006|Branch BR006 is closed, so it cannot accept transactions|

Other branches follow the same pattern: `manager.br002`, `clerk2.br003`, and so on (BR001 to BR005).

## Features

* Branches with counters, opening hours and open/closed status; staff add, update, deactivate.
* Retail catalogue with per-branch stock (never negative); stock adjustment with a reason (stored procedure `usp_AdjustStock`, with history); category price revision for administrators (one set-based update per product, one transaction).
* Counter transaction (basket) mixing retail items, shipping and money orders; payment; Open, Paid, Voided, Refunded.
* Shipping price from zone rate, weight band and service level; weight limits per service level.
* Money orders with identity verification above a configurable threshold; only the last four characters of the document are stored.
* Void and refund (Branch Manager and a reason); stock returns on reversal.
* Four reports (daily sales and low stock run as stored procedures): daily sales split retail/services, low stock, shipping volume by zone and level, money orders and verifications (auditor view).
* Simulated notifications for low stock, high-value money orders and every void or refund.
* Security and observability: Problem Details errors, correlation ID, request timing, maintenance mode, security headers, HTTPS and HSTS, API key, IP allowlist, cookie authentication with claims and policies, antiforgery, rate limiting, CORS, `/health`.
* Action filters: audit (written to the `AuditEntries` table), validation, performance, domain exception, branch access.
* Errors as Problem Details: concurrency and duplicate value are 409, not found is 404, business rule violation is 400.
* * Identity verification is decided by a simulated verification service, not by the clerk. In the demo, a document number starting with `FAIL` is rejected, one starting with `PEND` stays pending, and any other number passes.

## Sample requests (Postman or curl)

Integration endpoint for courier partners (needs the API key you stored in step 1, and a client address on the allowlist):

```bat
curl -k https://localhost:7141/integration/parcel-status/APX123456 -H "X-Api-Key: <your key>" -H "X-Correlation-Id: demo-001"
```

|Request|Expected|
|-|-|
|Same call without `X-Api-Key`|`401` Problem Details|
|Same call with a wrong key|`401`|
|API key not configured at all|`500`|
|From an address not in `Security:IntegrationAllowedIps`|`403`|
|`/integration/parcel-status/SLOW0001`|`504` (courier timeout)|
|`/integration/parcel-status/FAIL0001`|`502` (courier failure)|
|More than 30 calls a minute with one key|`429` with `Retry-After`|
|`GET /health`|`200 Healthy` (also while maintenance mode is on)|
|Sixth login attempt within a minute from one address|`429`|

Maintenance mode: set `"Maintenance": { "Enabled": true }` in `appsettings.json` (it reloads without a restart);
every page returns `503` with `Retry-After`, `/health` stays available.

## Configuration

All business and security settings are in `ApexPost.Web/appsettings.json`: weight limits, price multipliers, tax rates,
money order thresholds and fee, low-stock threshold, courier timeout and concurrency, cache lifetime, IP allowlists,
approved CORS origin, rate limits and the slow-action threshold. Secrets (connection string, demo password, API keys) come from
user-secrets or environment variables only.

## Evidence for assessment

* Screenshots of each role's screens and the test run: `docs/Screenshots/`; the blocking demo: `docs/Screenshots/blocking-session.png`.
* `AUDIT-SAMPLE.json` is a real export from `/Audit/Export`.
* Reporting SQL, demos and procedures: `sql/01` to `sql/07`; performance workloads `sql/08` to `sql/10`; least privilege `sql/11`.
* Execution-plan and timing evidence: [PERFORMANCE.md](PERFORMANCE.md).
* The Week 2 measurements (cache, `Task.WhenAll`, `SemaphoreSlim`) are in `DESIGN.md`, section 8, produced by `ApexPost.UnitTests/Measurements/MeasurementTests.cs`.
