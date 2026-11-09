# API-NOTES: ApexPost.Api (Week 4, Part B)

`ApexPost.Api` is a REST API for shipments and their scans. It uses the same SQL Server database, `ApexPostDbContext`,
unit of work and layering as the Week 3 application. Controllers only bind input, call `IParcelService` and return an
HTTP result; every rule is in `ParcelService` and `ParcelTransitions`.

Run it: `dotnet run --project ApexPost.Api` (https://localhost:7241). The connection string is the same user-secret as
`ApexPost.Web` (`ConnectionStrings:ApexPostDb`); nothing secret is in a committed file. In Development the API applies the
migrations and adds six fictional shipments (`APX-10000001` to `APX-10000006`).

## New tables (additive migration `AddParcels`)

Week 3 had no standalone shipment with a tracking number, a recipient and a status (a Week 3 shipment is a line of a
counter transaction). Two new tables were added; no Week 3 table or class was changed.

| Table | Key | Notes |
|---|---|---|
| `Parcels` | `TrackingNumber` varchar(12) | `WeightKg` decimal(5,2) with a CHECK 0.1 to 70, `Status` with a CHECK 0 to 5, `rowversion`, index on (`Status`, `RecipientCity`) |
| `ParcelScans` | `ScanId` varchar(50) | The Part A id `scan::{guid}` is the primary key, which is what makes the upload idempotent. FK to `Parcels`, cascade delete |

## Endpoints

| Verb | Route | Route name | Success | Errors |
|---|---|---|---|---|
| GET | `/api/shipments?page=&pageSize=&status=&city=` | `ListShipments` | 200 | 400 (page < 1, pageSize outside 1 to 50, unknown status), 500 |
| GET | `/api/shipments/{trackingNumber}` | `GetShipment` | 200 | 404, 500 |
| POST | `/api/shipments` | `CreateShipment` | 201 + `Location` | 400, 409 (duplicate), 415, 500 |
| PUT | `/api/shipments/{trackingNumber}` | `ReplaceShipment` | 200 | 400, 404, 409 (already Delivered/Failed), 415, 500 |
| PATCH | `/api/shipments/{trackingNumber}/status` | `UpdateShipmentStatus` | 200 | 400, 404, 409 (invalid transition), 415, 500 |
| DELETE | `/api/shipments/{trackingNumber}` | `DeleteShipment` | 204 | 404, 409 (status is not Created), 500 |
| POST | `/api/shipments/{trackingNumber}/scans` | `UploadScans` | 200 | 400, 404, 409, 415, 500 |
| GET | `/api/shipments/{trackingNumber}/scans` | `ListScans` | 200 | 404, 500 |

The `{trackingNumber}` route has the constraint `regex(^APX-\d{8}$)`. A value that does not match does not reach a controller and
gets the usual 404 for an unknown route.

## Status codes

| Code | When |
|---|---|
| 200 | A successful read or update |
| 201 | A shipment was created; the `Location` header points to `GET /api/shipments/{trackingNumber}` |
| 204 | A shipment was deleted |
| 400 | `ValidationProblemDetails`: a DTO rule failed (tracking number pattern, weight 0.1 to 70, required recipient fields, phone format, paging limits, a Delivered scan without a signature name) |
| 404 | Unknown tracking number |
| 409 | Duplicate tracking number, a status change that is not allowed, deleting a shipment that is not Created, editing a finished shipment, or a concurrent change (rowversion) |
| 415 | The request body is not `application/json` |
| 500 | An unhandled error, returned by the global exception handler. No stack trace outside Development |

All errors are `application/problem+json` (`ProblemDetails`).

## Status transitions

`Created` to `PickedUp` to `InTransit` to `OutForDelivery` to `Delivered` or `Failed`. Delivered and Failed are final. Anything
else (skipping a step, going back, leaving a final status) is a 409 whose message names both statuses.

## Idempotent scan upload

`POST /api/shipments/{trackingNumber}/scans` takes `{ "courierId": "...", "scans": [ ... ] }` with 1 to 100 scans. Each scan carries
its Part A id (`scan::{guid}`). The answer is always
`{ "accepted": n, "duplicatesSkipped": n, "rejected": [ { "scanId": "...", "reason": "..." } ] }`, and the three numbers add up to the
number of scans sent.

* A scan id that is already stored (from this or an earlier request) is counted in `duplicatesSkipped` and not stored again.
* A scan with a time more than 5 minutes in the future, or a scan for a shipment that is already Delivered or Failed, is `rejected`
  with a reason. The other scans of the batch are still accepted.
* A scan can move the shipment forward (Pickup to PickedUp, InTransit to InTransit, Delivered to Delivered, Failed to Failed). It never
  moves it back and never changes a finished shipment.
* A Delivered scan without `recipientSignatureName` fails the DTO validation and the whole request is a 400 (custom rule).
* If two identical uploads run at exactly the same moment, the primary key stops the second insert and that request gets a 409.
  Repeating it then returns `duplicatesSkipped`.

## Validation (DTOs, never entities)

`CreateShipmentRequest`, `UpdateShipmentRequest`, `UpdateStatusRequest` and `ScanUploadRequest` are separate from the EF entities
(`ParcelEntity`, `ParcelScanEntity`). `[ApiController]` validates them automatically and returns `ValidationProblemDetails` with 400.

## Tests

* `ApexPost.UnitTests/Parcels/ParcelServiceTests.cs`: transitions, duplicates, delete rule, paging and filter, idempotent upload,
  commit count, with an in-memory repository.
* `ApexPost.Api.http`: one success and one failure request for every endpoint.
