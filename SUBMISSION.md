# Week 4 submission guide

Where to find each deliverable. All data is fictional. Nothing secret is in the repository (run `scripts/set-dev-secrets.ps1` once to create the development secrets).

## Part A: Couchbase Lite offline store (25)
| Deliverable | Where |
|---|---|
| Class library and console harness | `ApexPost.Courier.Offline`, `ApexPost.Courier.Offline.Harness` |
| Design note | [OFFLINE-DESIGN.md](OFFLINE-DESIGN.md) |
| Screenshots (queries, listener log) | `docs/week4/` |
| Tests | `ApexPost.Courier.Offline.Tests` |

## Parts B and C: REST API, OpenAPI, versioning, client (50)
| Deliverable | Where |
|---|---|
| API | `ApexPost.Api` (v1 deprecated, v2) |
| Requests, one success and one failure per endpoint, both versions | `ApexPost.Api/ApexPost.Api.http` |
| Endpoints, status codes, descriptions | [API-NOTES.md](API-NOTES.md) |
| OpenAPI vs generator vs Swagger UI | [README.md](README.md) |
| Versions, breaking changes, v1 sunset plan | [VERSIONING.md](VERSIONING.md) |
| Contracts | `contracts/v1.json`, `contracts/v2.json` |
| Generated client and console consumer | `ApexPost.Client`, `ApexPost.Client.Demo` |
| Interactive documentation | `https://localhost:7241/swagger` (Development) |

## Part D: security and the courier app (25)
| Deliverable | Where |
|---|---|
| Auth design, OAuth 2.0 flows, API keys, TLS and HSTS, the `#if DEBUG` exception | [SECURITY.md](SECURITY.md) |
| Retry, circuit breaker, timeouts, why the upload is safe to retry | [RESILIENCE.md](RESILIENCE.md) |
| API security (JWT, roles, courierId check, partner key) | `ApexPost.Api/Security`, controllers |
| Mobile app | `ApexPost.Courier` (MAUI), `ApexPost.Courier.Core` (client, resilience, sync), `ApexPost.Courier.Offline` (store, outbox, batches) |
| Updated .http file (401, 403, invalid key, valid token) | `ApexPost.Api/ApexPost.Api.http` |
| Test output | `docs/week4/smoke-api-output.txt` (39 cases), `docs/week4/client-demo-output.txt` |
| Demo video (3 to 5 minutes) | Submitted with the assignment (not stored in the repository because of its size) |

## How to run and check
1. `.\scripts\set-dev-secrets.ps1` (once)
2. `dotnet run --project ApexPost.Api --launch-profile https`
3. `.\scripts\smoke-api.ps1` and `.\scripts\smoke-api.ps1 -Version v1`
4. `dotnet test ApexPost.UnitTests`, `dotnet test ApexPost.Courier.Offline.Tests`, `dotnet test ApexPost.Courier.Core.Tests`
5. App: `dotnet run --project ApexPost.Courier -f net10.0-windows10.0.19041.0` (see README)
