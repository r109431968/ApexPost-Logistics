# ApexPost testing strategy (Week 5)

This document is written before the new tests. It says what we test, at which level, with which tools, and why.
It is finished at the end of the week with the data-access section, the existing-test review and the reflection.

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
