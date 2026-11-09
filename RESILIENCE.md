# Resilience of the courier app (Week 4, Part D)

A courier works on a mobile network: tunnels, lifts, weak signal, and sometimes a server that is down. The app must keep working, must not hammer a server that is already struggling, and must never lose or duplicate a scan. This note says what the app does and why. The code is in `ApexPost.Courier.Core/Resilience` and the numbers are in `ResilienceOptions`.

## The pipeline

Every call to the API goes through the same handlers, from the outside in:

1. **AuthHeaderHandler**: adds the Bearer token from SecureStorage. A `401` clears the token and sends the courier to the Login page.
2. **Total timeout (45 s)**: the whole call, with all retries and waits. The courier never waits longer than this for one call.
3. **Retry**: up to 3 retries (so up to 4 requests for one call).
4. **Circuit breaker**: stops calling a server that keeps failing.
5. **Attempt timeout (10 s)**: one request. A request that hangs is cancelled, and the retry strategy above may try again.

Order matters. The attempt timeout is innermost so a hung request becomes a failure that the retry and the breaker can see. The total timeout is outermost so retries cannot add up to a minute of waiting.

## Retry rules

| Outcome | Retried? | Why |
|---|---|---|
| Network error (no route, connection reset, DNS) | Yes | Temporary by nature. |
| Request timeout (our 10 s limit, or HTTP 408) | Yes | The next try may be faster. |
| 5xx from the server | Yes | The server may recover. |
| 400, 401, 403, 404, 409 | **No** | The same request would get the same answer. A retry only adds load. |
| Open circuit | No | The call is refused at once, without touching the network. |

**Backoff with jitter.** The wait doubles each time (about 0.5 s, 1 s, 2 s) and gets random jitter. Without jitter, thousands of phones that lost the signal together would all retry at the same moment and hit the server together ("thundering herd"). Jitter spreads them out.

**Why retrying is safe.** A retry can send a request the server already processed (the answer was lost, not the request). For a scan upload this is harmless because each scan has its own id (`scan::{guid}`) and the server treats a repeated scan id as a duplicate, not as a second scan. The upload is idempotent. A call that is not idempotent must not be retried automatically.

## Circuit breaker

If at least **50 %** of the calls in a **30 s** window fail, and there were at least **4** calls, the circuit **opens** for **20 s**:

- **Open**: every call fails at once with `BrokenCircuitException`. The app does not wait for a timeout, and the server gets no extra load. The red banner **"Server unavailable – working offline"** is shown on every screen. Scans are still saved on the device.
- **Half-open** (after 20 s): one call is let through as a test.
- **Closed**: if that call works, the circuit closes, the banner goes away and the sync continues. If it fails, the circuit opens again for 20 s.

Only failures that can get better count (network error, timeout, 5xx). A 400 or 404 means the server is alive and answered, so it does not open the circuit.

## Timeouts

Two levels, because they protect different things: the **attempt** timeout (10 s) stops one hung request; the **total** timeout (45 s) stops the retries and waits from adding up. The total is larger than 4 attempts of 10 s would suggest only when the server is slow, never because of waiting forever.

## What the courier sees

- Saving a scan never needs the network. It is stored as `Pending` in Couchbase Lite first.
- Offline or circuit open: the banner shows, scans stay `Pending`. Offline is **not** a failure of the scan.
- The sync starts again when the network comes back (`Connectivity.ConnectivityChanged`), after sign-in, every 30 seconds, and when the courier presses **Sync now**. The 30-second timer is how the app finds out that a server which was down is back.
- Scans go in batches of 20, oldest first. If a batch cannot be sent, the run stops and the rest stays `Pending`.
- A scan the server **refuses** (400, 403, 404, 409) becomes `Failed` and is not sent again. The courier sees it on the Pending Scans screen and the reason on the Sync Status screen.
- A `401` ends the session: the token is deleted, nothing is marked, and the Login page is shown. Pending scans stay on the device.

## Logging

Every retry (attempt number, delay and reason), every circuit change (open, half-open, closed) and every sync run is logged with `ILogger`. Tokens, passwords and scan contents are never logged.

## How it is tested

`ApexPost.Courier.Core.Tests/ResilienceTests.cs` builds the real pipeline on a fake network with very small times: it checks that 5xx and network errors are retried, 4xx are not, the circuit opens after repeated failures, calls are then refused without touching the network, the banner state changes, and the circuit recovers. `BatchUploaderTests` and `ApexPost.Courier.Offline.Tests/BatchSyncServiceTests.cs` check the batches of 20, offline behaviour, refused scans and the expired session.

## Known limits

- The retry count and the timeouts are fixed in code (`ResilienceOptions`), not changed remotely.
- The sync is foreground only: it runs while the app is open. Background sync (Android WorkManager) would be the next step.
- There is no automatic token refresh: after the token expires the courier signs in again.
