# Courier offline store: design note (Week 4, Part A)

`ApexPost.Courier.Offline` keeps shipment scans on the courier device in an embedded Couchbase Lite database (`apexpost_courier`, default collection).
A scan is one JSON document with the id `scan::{Guid}` and the fields `trackingNumber`, `scanType`, `courierId`, `location`, `scannedAtUtc`, `notes` and `syncStatus`.
The domain record and `IScanRepository` use no Couchbase types; `CourierDatabase`, `CouchbaseScanRepository` and the mapper (`Storage/`) are the only code that does.
Lookups by id are key-value calls. The three queries are SQL++ with named parameters (`$trackingNumber`, `$syncStatus`, `$courierId`); no value is ever added to the query text.
Value indexes exist on `trackingNumber` and `syncStatus`, and the harness prints the query plan. `OutboxService` uploads Pending scans through `IShipmentUploader` (a fake in this part) and marks them Synced or Failed.
A change listener logs the ids of changed documents with the live pending count, and `CourierDatabase.Dispose` removes the listener token before it closes the database.

## Why the app saves locally first

A courier loses coverage in lifts, basements and rural roads, but a pickup or delivery happens exactly then, and the customer will not wait.
If the app called the API first, a failed call would mean a lost scan or a courier retrying by hand. So the write path is: save the scan on the device with `syncStatus = Pending`, show success at once, and let the outbox upload it later.
The local store is the source of truth until the server accepts the scan, and it survives a crash, a flat battery and a restart (a test closes and reopens the database).
The repository enforces the rule: `Create` always stores `Pending`, and only the outbox changes the status to `Synced` or `Failed`.

## How conflicts would be handled

A conflict needs two writers changing the same document. Scans are append-only facts: every scan gets its own new id, so two devices never create the same document, and only the outbox on that device changes `syncStatus`.
On one device, writes use last-write-wins, which is safe because one process writes the file.
When documents are shared between devices, I would let the **server decide**, not last-write-wins by timestamp. Device clocks are unreliable (a phone with the wrong time would win every conflict) and last-write-wins silently drops one side's data.
The server sees all scans of a tracking number, can check their order (a Delivered scan before any Pickup is suspicious) and can reject a scan. A rejected scan comes back as `Failed`, stays on the device, and the courier sees it.

## Why an embedded database must not be shared by several processes

Couchbase Lite runs inside the app process and owns its database files. It is safe from many threads of one process, but opening the same folder from two processes is not supported.
Each process has its own caches and change listeners, so one process is not told about the other's writes (a listener only fires for changes made inside the same process), and two writers can interfere at file level and risk damaged data.
So exactly one process, the courier app, owns the files. Anything else that needs the data must run inside that process or go through the outbox and API. The harness opens the database once and closes it on exit.

## How to run

`dotnet run --project ApexPost.Courier.Offline.Harness` and `dotnet test ApexPost.Courier.Offline.Tests`. All data is fictional.
The harness database is in `%LOCALAPPDATA%\ApexPost\courier-offline`; tests use temporary folders. A tracking number that starts with `FAIL` is rejected by the fake uploader, so both sync outcomes can be shown.

## Part D: how the outbox sends scans to the API

`BatchSyncService` takes the `Pending` scans oldest first and sends them in **batches of 20**. Each scan comes back with its own outcome:

- **Synced**: the server has it (a repeated scan id counts as already there, so a retry is safe).
- **Rejected** (400, 403, 404, 409): the scan becomes `Failed`, stays on the device and the courier sees the reason.
- **RetryLater** (network down, timeout, 5xx, open circuit): the scan stays `Pending` and the run stops, because the next batch would fail the same way.
- **Unauthorized** (401): the run stops, nothing is marked, and the app shows the Login page.

Only one run at a time (a second call while one is running returns at once). The REST API takes scans for one shipment per call, so a batch of 20 is split by tracking number and courier before it is sent. A `Delivered` scan carries the recipient signature name (`recipientSignatureName`), which the New Scan screen asks for before saving. The timing rules (when a run starts) are in `SyncScheduler`; the retry and circuit breaker rules are in [RESILIENCE.md](RESILIENCE.md).
