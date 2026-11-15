# ApexPost testing strategy (Week 5)

This document is written before the new tests. It says what we test, at which level, with which tools, and why.
It was finished at the end of the week (sections 4 to 11).

## 1. Risk map

Risk = how likely it is to break, times how costly it is when it breaks. The list is ordered from most to least costly.

| # | Risk | Where it lives | Why it matters | Main test level |
|---|---|---|---|---|
| 1 | Money and stock go wrong at the counter (price, tax, two payments for one sale, stock below zero) | CheckoutService, ShippingPricer, TaxCalculator, UnitOfWork | Direct money loss; a half-saved sale is hard to repair | Unit (rules), integration (one commit, real SQL) |
| 2 | Scan de-duplication: the same scan is stored twice, or a retry creates duplicates | ParcelService.UploadScansAsync, scan id as key | The courier app retries after timeouts, so repeats are normal, not rare | Unit (mocked repository), BDD, integration |
| 3 | Shipment status transitions: a parcel moves backwards or a final parcel changes | ParcelTransitions | Wrong tracking status is visible to customers and partners | Unit (parameterized, every pair of statuses) |
| 4 | Duplicate tracking numbers and invalid input (format, weight 0.1 to 70 kg) | Validation attributes, repository unique index | Two parcels with one number cannot be told apart later | Unit (validation), integration (unique index), BDD |
| 5 | JWT and role rules: 401 without a token, 403 for the wrong role, a courier uploading for another courier | Api security, MayUploadFor, fallback policy | A security hole is the most expensive kind of bug | Unit (policy logic), integration (HTTP status codes) |
| 6 | Client retry and circuit breaker: retries on a 4xx, no stop after repeated failures, wrong mapping of errors | ApexApiClient, Polly pipeline in Courier.Core | A wrong retry rule floods the server or loses scans | Unit (fake HttpMessageHandler) |
| 7 | Outbox and sync: scans lost, stuck, or marked synced when the server did not accept them | OutboxService, BatchSyncService, CouchbaseScanRepository | The scan is the only copy while the phone is offline | Unit (mocks), integration (real embedded database) |
| 8 | Two clerks change the same row at the same time | rowversion, UnitOfWork exception translation | Silent overwrite of stock or prices | Integration (real SQL Server) |

## 2. Test pyramid for ApexPost

Roughly 65 percent unit tests, 25 percent integration tests, 10 percent BDD scenarios (by number of tests; BDD has few but broad scenarios).

| Level | What it covers | Tools | What it does not prove |
|---|---|---|---|
| Unit (many, milliseconds) | One rule or one service with its dependencies replaced: status transitions, pricing, validation, scan de-duplication, API client error mapping, outbox decisions | xUnit, Moq, fake HttpMessageHandler, fixed clock | That the real database, the HTTP pipeline or the wiring in Program.cs work |
| Integration (some, seconds) | Real pieces together: repositories against a real database, an API endpoint through the real pipeline with test authentication, the real Couchbase Lite store | xUnit, WebApplicationFactory, SQLite in-memory or SQL Server test database | Every combination of inputs; they are too slow for that |
| BDD (few, business wording) | Whole business behaviours that a non-programmer can read: shipment creation, scan sync | Reqnroll with Gherkin | Small edge cases; those stay in unit tests |

The base is wide because unit tests are fast and tell exactly which rule broke. The top is narrow because BDD and integration tests are slower and fail for more reasons.

## 3. Framework choice: xUnit, one framework for the whole solution

| Reason | Explanation |
|---|---|
| Consistency | All existing test projects (about 260 tests) already use xUnit. Switching would mean rewriting tests with no gain. |
| Test isolation | xUnit creates a new instance of the test class for every test, so fields are never shared by accident. This fits the rule "no shared mutable state, no dependence on test order". |
| Setup and teardown | The constructor is the setup and IDisposable is the teardown. Shared expensive setup (a WebApplicationFactory or a database) is explicit with IClassFixture, so it is visible in the code. |
| Parameterized tests | [Theory] with [InlineData] is the natural tool for rules with many inputs (tracking number format, weight limits, status transitions). |
| Reqnroll | Reqnroll has an xUnit integration, so feature files run in the same runner as the other tests. |
| Tooling | Works in Visual Studio Test Explorer and with dotnet test, with no manual setup. |

NUnit would also have worked. We keep xUnit because it is already used everywhere, and one framework means one set of rules to explain.

## 4. Rules that keep tests honest

| Rule | How ApexPost follows it |
|---|---|
| No shared mutable state | xUnit creates a new test class instance per test. Static fields are `readonly` and immutable (a zone, a pricer). |
| No dependence on order | Every test builds what it needs. Integration tests use a new database per factory, BDD uses a new database per scenario. |
| No dependence on the real clock | Code takes `IClock`; tests use `FixedClock` with fixed dates (for example 2026-11-10). New tests never call `DateTimeOffset.UtcNow`. |
| No real network, no real secrets | The API client is tested with a fake `HttpMessageHandler`. Integration tests set a fictional password and signing key through test configuration. |
| Names say what happens | `Method_Condition_ExpectedResult`, and Arrange / Act / Assert comments in the new tests. |
| A test must be able to fail | Section 9 and `docs/week5/mutation-proof.log` show tests failing when the production code is broken on purpose. |

## 5. Mocking strategy

| Dependency | Test double | Why | What it does NOT prove |
|---|---|---|---|
| `IParcelRepository` in `ParcelService` | Moq `Strict` mock | The service rules (duplicates, status moves, the future-scan check) need no database, and Strict fails on any call we did not expect | That the SQL query, the unique index or the EF mapping work. The integration tests prove that. |
| `IUnitOfWork` | Moq mock, `Verify(Times.Once / Never)` | Proves "save once on success, never on failure" | That the transaction really commits |
| `IClock` | Fixed clock (Moq or `FixedClock`) | Same input gives the same result every day | That the server clock is correct |
| Outbox repository and API client in `OutboxService` | Moq | Proves the outbox decisions: which scans are marked synced, what happens on a rejection | That Couchbase Lite stores the data, or that the server accepts the batch |
| HTTP server for `ApexApiClient` | Fake `HttpMessageHandler` (200 with JSON, 404, 500, timeout, ProblemDetails, caller cancellation) with the REAL Polly pipeline | Tests the real retry, timeout and circuit breaker rules without a network | That the real server returns exactly this JSON. The API integration tests check the server side. |

Never mocked: the EF Core `DbContext`. A mocked `DbContext` returns whatever we tell it, so a wrong LINQ query or a missing index would still pass. Queries are tested against a real SQL Server test database.

## 6. Data-access strategy

Repository and query behaviour is tested against a **real SQL Server test database** (LocalDB, Windows authentication, no password), not against the EF Core InMemory provider or SQLite.
Reason: InMemory ignores unique indexes, foreign keys, `rowversion` and transactions, and SQLite translates some queries differently. The behaviours that matter for ApexPost (unique tracking number, concurrency token, one commit for a whole sale) only exist in SQL Server.

* Each test factory creates its own database (`ApexPostDb_..._<guid>`) with the real migrations and the fictional seed data, and drops it when it is disposed.
* Cost: these tests take seconds, not milliseconds. So rules are tested in unit tests, and only the wiring is tested here.
* The existing `SqlPersistenceTests` cover repositories, transactions and concurrency. `ApexPost.Api.IntegrationTests` covers an endpoint end to end (real JWT sign-in, 401 / 200 / 403, duplicate scan batch).

## 7. Review of the existing tests

| Project | Level | Verdict | What was done in week 5 |
|---|---|---|---|
| `ApexPost.UnitTests` | Unit, hand-written fakes | Good coverage of rules, pricing, tax, security and caching. Some tests used `DateTimeOffset.UtcNow`, which is safe here only because the same value is passed to the code under test | Added Moq tests for `ParcelService`, boundary Theory tests for API requests, TDD for the heavy parcel surcharge |
| `ApexPost.Courier.Core.Tests` | Unit, fake HTTP handler | Already covered 200, 404, 500, timeout and ProblemDetails with the real Polly pipeline | Added the missing case: caller cancellation is not treated as a server failure |
| `ApexPost.Courier.Offline.Tests` | Unit and integration (real embedded Couchbase Lite) | Good; no mock of the outbox service existed | Added Moq tests of the outbox decisions |
| `ApexPost.IntegrationTests` (Web) | Integration with SQL Server | Good; slow by design | Left as it is |
| (new) `ApexPost.Api.IntegrationTests` | Integration, API through the real pipeline | Did not exist | Added, because the Web and Api projects both define `Program` and cannot share one test project |
| (new) `ApexPost.Specs` | BDD with Reqnroll | Did not exist | Added |

Known weakness, kept honest: a few older tests still call `DateTimeOffset.UtcNow` when they build sample data. They do not depend on the date of the run, but a fixed date would be cleaner. This is listed as future work, not hidden.

## 8. BDD: level and scope

* Level: **API level**, through the in-memory server, real authentication and a real test database. Not UI level: the UI is slower and breaks on every layout change, and the business rules do not live in the UI.
* Features: `ShipmentCreation.feature` (valid shipment, duplicate tracking number, Scenario Outline with four invalid inputs) and `ScanSync.feature` (new scan, same scan twice, wrong courier, Delivered without a signature).
* Mechanics: a `Background` signs in; `ApiContext` is shared between steps and hooks by context injection; `BeforeScenario` starts a new server and database, `AfterScenario` drops them.
* BDD is for behaviours a business person can read. Small edge cases (every status pair, every weight boundary) stay in unit tests.

## 9. TDD, mutation proof and Copilot

* TDD: `TDD-LOG.md` records one rule (heavy parcel surcharge) built with RED, GREEN and REFACTOR commits.
* Mutation proof: `scripts/prove-tests-can-fail.ps1` breaks production code on purpose (a future-scan check, a boundary `>` to `>=`, a `break` to `continue`), runs the unit tests, and expects them to FAIL. Then it restores the code. A test that cannot fail proves nothing.
* Copilot: `COPILOT-LOG.md`. Rule: Copilot writes a first draft; a human reads every assertion, runs the test, and breaks the code once to see it fail. A generated test that only repeats the implementation, or that cannot fail, is rejected and the reason is written down.

## 10. What we do not test, and why

| Not tested | Reason |
|---|---|
| The MAUI courier app screens | The app is not part of the solution build; its logic is in `Courier.Core` and `Courier.Offline`, which are tested |
| Load and performance | Covered by `PERFORMANCE.md` (week 3) with measured evidence, not by unit tests |
| Exact Polly timing in milliseconds | Timing tests are flaky; we test the decisions (retry on 5xx, no retry on 4xx, breaker opens) and not the clock |
| The Swagger / OpenAPI page | Generated by a library; the contract files are checked in `contracts/` |
| Third-party libraries (EF Core, Polly, Couchbase) | They have their own tests; we test our use of them |

## 11. How to run

```powershell
dotnet test ApexPost.slnx                                  # everything
dotnet test ApexPost.UnitTests                             # fast unit tests only
dotnet test ApexPost.Api.IntegrationTests                  # API end to end (needs LocalDB)
dotnet test ApexPost.Specs                                 # BDD scenarios (needs LocalDB)
```

The result of the last full run is saved in `docs/week5/test-run.log`.

## 12. Reflection (draft: read it, then change it into your own words)

* **What the pyramid gave us:** most checks run in milliseconds, so I can run them after every change. When a unit test fails, it names the exact rule. When an integration or BDD scenario fails, I know the wiring broke, not the rule.
* **What mocks hid from us:** the Moq tests of `ParcelService` passed even though a mock never checks the real SQL. Only the SQL Server tests can show a wrong query or a missing unique index. A green mock test is not a green system.
* **Where the brief and the code disagreed:** the brief gave "surcharge over 30 kg" as the example, but the standard parcel limit is already 30 kg, so no valid parcel could ever reach it. I used 20 kg and wrote the finding in `TDD-LOG.md`.
* **What I would do next:** replace the remaining `UtcNow` calls in old tests with fixed dates, and run the whole suite in a build pipeline on every push.
* **A bug the tests found:** the boundary test for the scan id failed with a `FormatException`. The error message `"... scan::{guid}."` contains braces, and DataAnnotations formats the message with `string.Format`, so a wrong scan id would have caused an HTTP 500 instead of a clean 400. Fixed by writing `{{guid}}`. This is a real defect that no existing test had caught.
