# VERSIONING: ApexPost.Api

The version is part of the URL: `/api/v1/shipments` and `/api/v2/shipments` (URL-segment versioning, `Asp.Versioning`).
There is no unversioned address and no default: a client always says which contract it speaks.

| Version | Status | Contract |
|---|---|---|
| v1 | **Deprecated** (`[ApiVersion("1.0", Deprecated = true)]`) | [contracts/v1.json](contracts/v1.json) |
| v2 | Current | [contracts/v2.json](contracts/v2.json) |

Both versions call the same `IParcelService`. Only the controllers, the response DTOs and the mapping differ
(`Contracts/` and `Mapping/ShipmentMapping.cs` for v1, `Contracts/V2/` and `Mapping/ShipmentMappingV2.cs` for v2).
With `ReportApiVersions = true` every response reports the state of the version that answered it: a version 2 response carries `api-supported-versions: 2.0`, a version 1 response carries `api-deprecated-versions: 1.0`.

## Change log

| # | Change | Version | Breaking? | Why |
|---|---|---|---|---|
| 1 | The version moves into the path: `/api/shipments` becomes `/api/v1/shipments` | v1 | **Breaking** for Part B callers | A URL segment is explicit, easy to see in logs, and cacheable. Only Part B's own test callers existed, so the cost was small |
| 2 | Recipient fields `recipientName`, `recipientPhone`, `recipientAddress`, `recipientCity` become one object `recipient { name, phone, address, city }` | v2 | **Breaking** | A client that reads `recipientName` finds nothing. That is why it is a new major version and v1 keeps the flat shape |
| 3 | `latestScan { scanType, location, scannedAtUtc }` is added to a shipment | v2 | Non-breaking on its own | Clients ignore unknown fields, and the field is null when there is no scan |
| 4 | `status` is text (`"InTransit"`) instead of a number (`2`) | v2 | **Breaking** | A client that parses an integer fails on a string. The text is stable if the enum is ever reordered |
| 5 | `scanType` in scan history is text instead of a number | v2 | **Breaking** | Same reason as 4 |
| 6 | Request bodies (create, replace, status, scan upload) | v1 and v2 | Unchanged | Requests accept a status or scan type by name or by number in both versions, so no client has to change what it sends |
| 7 | The scan upload answer (`accepted`, `duplicatesSkipped`, `rejected`) | v1 and v2 | Unchanged | Part D's courier app depends on it |
| 8 | Route names get a `V2` suffix (`CreateShipmentV2`) | v2 | Non-breaking | Route names must be unique in the application. They become the `operationId`, so the generated client has `CreateShipmentV2Async` |

**Rules we follow.** Adding an optional response field, a new endpoint or a new optional request field is non-breaking.
Removing or renaming a field, changing a type, making an optional request field required, tightening validation, or
changing a status code is breaking and needs a new major version.

## Deprecation and sunset plan for v1

1. **Now:** v1 is marked deprecated. Every v1 operation is `deprecated: true` in `/openapi/v1.json`, Swagger UI strikes it through, and every
   response carries the `api-deprecated-versions` header.
2. **Announcement (month 0):** partners and the courier app team are told the date below. VERSIONING.md and the release notes name v2 as the replacement.
3. **Migration window (months 0 to 6):** both versions run. New features go to v2 only. v1 gets security and data fixes only.
4. **Warning (month 5):** v1 responses add a `Sunset` header with the removal date. Callers still on v1 in the logs are contacted one by one.
5. **Sunset (month 6):** v1 controllers are removed. A call to `/api/v1/...` then gets 404 (unknown route). `contracts/v1.json` stays in the repository as history.
6. **Rollback:** if an important caller is not ready, the date moves once, by at most 3 months, and the new date is announced the same day.

## Contract tracking

`/contracts/v1.json` and `/contracts/v2.json` are saved from the running API with `scripts/export-contracts.ps1`. They are committed, so a change
to the public contract shows up as a diff in a pull request. The C# client is generated from `v2.json` by `scripts/generate-client.ps1` (NSwag).
