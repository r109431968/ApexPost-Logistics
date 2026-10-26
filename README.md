# ApexPost Counter Platform (Week 2)

ASP.NET Core MVC (.NET 10, C# 14) application for ApexPost branches: retail sales, parcel and letter shipping,
money orders and identity verification from one counter. Data is stored in JSON files behind repository
interfaces, so next week's database can replace them without changing business logic.

Design decisions, diagrams and ADRs are in [DESIGN.md](DESIGN.md). A sample of the audit log is in
[AUDIT-SAMPLE.json](AUDIT-SAMPLE.json).

## Solution layout

| Project | Purpose |
|---|---|
| `ApexPost.Domain` | Entities, value objects (Money, Weight, Address, BranchCode, Sku, IdentityVerification), enums, rules, events. No dependencies. |
| `ApexPost.Application` | Use cases (checkout, reversal, reports, login, staff...), interfaces (ports), specifications, validation attributes, extension members. |
| `ApexPost.Infrastructure` | JSON repositories, audit store, credential store, cache, simulated courier service, seed data. |
| `ApexPost.Web` | MVC controllers and views, middleware, filters, composition root (`Program.cs`). |
| `ApexPost.UnitTests` | xUnit tests for rules, pricing, tax, validation, persistence, cache, courier. |
| `ApexPost.IntegrationTests` | `WebApplicationFactory` tests for the middleware pipeline, security and audit. |

## Run it

Needs the .NET 10 SDK and a trusted HTTPS development certificate (`dotnet dev-certs https --trust`, once).

1. **Set the secrets** (they are never stored in the repository). Run `setup-secrets.bat`, or by hand:

   ```bat
   dotnet user-secrets set "Seed:DemoPassword" "<a password with at least 12 characters>" --project ApexPost.Web
   dotnet user-secrets set "Integration:ApiKeys:CourierPartner" "<a long random key>" --project ApexPost.Web
   ```

2. **Start the web app**:

   ```bat
   dotnet run --project ApexPost.Web --launch-profile https
   ```

   Open https://localhost:7141. The first start creates `ApexPost.Web/data/*.json` (6 branches, 26 staff,
   30+ products, 10 zones, 48 transactions). Delete that folder to start again with fresh seed data.

3. **Tests**: `dotnet test ApexPost.slnx`

4. **Windows program (.exe)**: `publish.bat` creates `publish\ApexPost.Web.exe`; `run-published.bat` starts it.

## Demo accounts

Every seeded account uses the demo password you set in step 1.

| Role | Email | Home branch | Can do |
|---|---|---|---|
| Counter Clerk | `clerk1.br001@apexpost.test` | BR001 | Sell, ship, money orders, identity checks at BR001 |
| Branch Manager | `manager.br001@apexpost.test` | BR001 | Everything a clerk does, plus void, refund, stock adjustment, reports |
| Auditor | `auditor1@apexpost.test` | BR001 | Read-only: reports for all branches, money order audit view, audit log |
| Administrator | `admin1@apexpost.test` | BR001 | Staff and branch administration, audit log, reports. Does not process sales |
| Inactive clerk | `clerk4.br002@apexpost.test` | BR002 | Cannot sign in (demonstrates the inactive-staff rule) |
| Manager of a closed branch | `manager.br006@apexpost.test` | BR006 | Branch BR006 is closed, so it cannot accept transactions |

Other branches follow the same pattern: `manager.br002`, `clerk2.br003`, and so on (BR001 to BR005).

## Features

* Branches with counters, opening hours and open/closed status; staff add, update, deactivate.
* Retail catalogue with per-branch stock (never negative); stock adjustment with a reason.
* Counter transaction (basket) mixing retail items, shipping and money orders; payment; Open, Paid, Voided, Refunded.
* Shipping price from zone rate, weight band and service level; weight limits per service level.
* Money orders with identity verification above a configurable threshold; only the last four characters of the document are stored.
* Void and refund (Branch Manager and a reason); stock returns on reversal.
* Four reports: daily sales split retail/services, low stock, shipping volume by zone and level, money orders and verifications (auditor view).
* Simulated notifications for low stock, high-value money orders and every void or refund.
* Security and observability: Problem Details errors, correlation ID, request timing, maintenance mode, security headers, HTTPS and HSTS, API key, IP allowlist, cookie authentication with claims and policies, antiforgery, rate limiting, CORS, `/health`.
* Action filters: audit, validation, performance, domain exception, branch access.

## Sample requests (Postman or curl)

Integration endpoint for courier partners (needs the API key you stored in step 1, and a client address on the allowlist):

```bat
curl -k https://localhost:7141/integration/parcel-status/APX123456 -H "X-Api-Key: <your key>" -H "X-Correlation-Id: demo-001"
```

| Request | Expected |
|---|---|
| Same call without `X-Api-Key` | `401` Problem Details |
| Same call with a wrong key | `401` |
| API key not configured at all | `500` |
| From an address not in `Security:IntegrationAllowedIps` | `403` |
| `/integration/parcel-status/SLOW0001` | `504` (courier timeout) |
| `/integration/parcel-status/FAIL0001` | `502` (courier failure) |
| More than 30 calls a minute with one key | `429` with `Retry-After` |
| `GET /health` | `200 Healthy` (also while maintenance mode is on) |
| Sixth login attempt within a minute from one address | `429` |

Maintenance mode: set `"Maintenance": { "Enabled": true }` in `appsettings.json` (it reloads without a restart);
every page returns `503` with `Retry-After`, `/health` stays available.

## Configuration

All business and security settings are in `ApexPost.Web/appsettings.json`: weight limits, price multipliers, tax rates,
money order thresholds and fee, low-stock threshold, courier timeout and concurrency, cache lifetime, IP allowlists,
approved CORS origin, rate limits and the slow-action threshold. Secrets (demo password, API keys) come from
user-secrets or environment variables only.

## Evidence for assessment

Screenshots and measurements are listed in `docs/EVIDENCE.md` (fill it in while running the app).
