# Security of ApexPost.Api (Week 4, Part D)

All people, parcels, keys and passwords in this repository are fictional. No secret, key or production address is stored in it.

## 1. What is protected and how

| What | Rule |
|---|---|
| Every shipment and scan endpoint (v1 and v2) | A valid access token (JWT) is required. Without one: **401**. |
| `Courier` role | Read shipments and scans, change a shipment's status, upload scans. |
| `Admin` role | Everything a courier may do, and create, replace and delete shipments. A `Courier` calling those gets **403**. |
| Scan upload (`POST .../scans`) | A courier may only upload under **their own** `courierId`. The id in the body is compared with the `courierId` claim of the token; if they differ: **403**. An admin may upload for anyone. |
| `GET /api/v2/partner/shipments/{trackingNumber}` | Only the `X-Api-Key` header is accepted. A JWT is refused here and an API key is refused everywhere else. The answer has no recipient name, phone or address. |
| `POST /api/v2/auth/token` | Open (it is the sign-in). Limited to 10 attempts per minute per address (**429** after that). |
| `/openapi/*.json`, `/swagger` | Development only. |

**Secure by default.** A fallback authorization policy ("must be signed in") applies to every endpoint that has no explicit rule, so a new controller is protected even if its author forgets an attribute. The sign-in and the OpenAPI documents opt out explicitly.

**Where the rules live.** `ApexPost.Api/Security/` (settings, token creation, API-key handler, registration), the `[Authorize]` attributes on the controllers, and `ClaimsPrincipalExtensions.MayUploadFor` for the courier rule. The business rules in the service layer do not know about users: authentication and authorization stay at the edge.

### The access token

* A JWT signed with HMAC-SHA256. Claims: `sub` (user name), `role`, and for couriers `courierId`.
* Lifetime 15 minutes (`Auth:Jwt:LifetimeMinutes`). A token is signed, **not encrypted**: anyone who has it can read the claims, so it contains no personal data and must travel over HTTPS only.
* The API checks the signature, the issuer, the audience and the expiry (30 seconds of clock tolerance), and accepts only HS256 (a token with `alg: none` or another algorithm is refused). Claim names are not renamed (`MapInboundClaims = false`).
* Swagger UI has an **Authorize** button because the v1 and v2 documents declare the `Bearer` and `ApiKey` security schemes and each operation lists the one it needs.

### The demo sign-in

`POST /api/v2/auth/token` accepts a user name and a password and returns a token. It is a **stand-in for an identity provider** so that the API and the mobile app can be run alone: three fictional users (`courier007`, `courier008`, `admin`) share one demo password. In production this endpoint is removed and the token comes from a real OAuth 2.0 / OpenID Connect provider; the API then only validates the tokens (change `Authority`/`Audience` of the JWT bearer options). The wrong-name and the wrong-password answers are identical, and the password is always compared (in constant time), so the answer does not reveal which user names exist.

## 2. Secrets: nothing in the repository

Three secrets are needed. They come from user-secrets in development and from environment variables or a secret store elsewhere:

| Setting | Environment variable | Used for |
|---|---|---|
| `Auth:Jwt:SigningKey` (32+ characters) | `Auth__Jwt__SigningKey` | signing and checking tokens |
| `Auth:DemoPassword` | `Auth__DemoPassword` | password of the demo users (development stand-in only) |
| `Partner:ApiKey` | `Partner__ApiKey` | the partner's `X-Api-Key` |

`scripts/set-dev-secrets.ps1` creates random values and stores them in user-secrets (outside the repository, in your user profile). `appsettings.json` holds only non-secret values (issuer, audience, lifetime). Outside Development the API **refuses to start** without a signing key. In Development, if no key is set, a random key is made for that run (tokens then stop working at restart; a warning says so).

Rotating a secret means setting a new value and restarting; every token signed with the old key stops working at once. Never put a real key in a committed file, a `.http` file or a screenshot.

## 3. OAuth 2.0 flows: which one fits ApexPost

OAuth 2.0 lets a client get an access token from an authorization server without handling the user's password in every app. The flows differ in **who** is asking:

| Flow | Who uses it | Fit for ApexPost |
|---|---|---|
| **Authorization Code + PKCE** | An app with a user: mobile, desktop, single-page app. The app opens the system browser, the user signs in at the provider, the app receives a one-time code and exchanges it for tokens. PKCE (a secret made per login) stops a stolen code from being used by another app. | **The right flow for the courier app** (`ApexPost.Courier`): short-lived access token plus a refresh token kept in `SecureStorage`. The app never sees the password. |
| **Client Credentials** | A server talking to a server, no user. The client proves itself with its own secret or certificate and gets a token for its own permissions. | **The better future for partners**: instead of a static API key, each partner gets a client id and secret, short-lived tokens with scopes (for example `tracking.read`) and can be revoked on its own. |
| **Device Code** | Devices without a browser or keyboard (a scanner gun, a TV). The user signs in on another device. | Possible for warehouse scanners. |
| **Resource Owner Password** (user name + password sent to the app) | Old; removed in OAuth 2.1 because the app sees the password and MFA cannot work. | **Not recommended.** The demo `/auth/token` has this shape only as a stand-in, as explained above. |
| **Implicit** (token in the browser URL) | Old single-page apps. | Obsolete; replaced by Authorization Code + PKCE. |

## 4. API keys are weaker than OAuth tokens

The partner route uses an API key because it is simple for a partner to use. It is accepted only on that one read-only route, and it should be seen as the weaker option:

* A key is a **static, long-lived secret**. It does not expire by itself, so a leaked key works until someone notices and rotates it. An access token expires in minutes.
* It identifies a **system, not a person**, and carries **no scopes or roles**; permission is whatever the server decides for that key. A token carries who and what.
* It is easy to leak: in a URL (so only a header is accepted here), in logs, in source control, in a screenshot, or by a partner that shares it between teams. There is no per-use proof and no refresh.
* Revoking one key without breaking others needs one key per partner and a store that supports it.

What is done to limit the damage: header only (never the query string), constant-time comparison, the key is never logged, HTTPS only, a read-only route that returns no personal data (no recipient name, phone or address), and a clear 401 with `WWW-Authenticate` for a missing or wrong key. A production system would add one key per partner (stored hashed), expiry and rotation dates, an IP allow-list or mutual TLS, per-partner rate limits and usage logs, and would move partners to Client Credentials.

## 5. TLS and HTTPS

TLS gives three things: **confidentiality** (nobody on the network can read tokens, keys or parcel data), **integrity** (nobody can change a request or a response on the way) and **server authentication** (the certificate proves the client is talking to the real API). Tokens and API keys are bearer secrets: anyone who copies one from plain http can use it, so they must never travel without TLS.

* `UseHttpsRedirection` sends any plain-http request to https with **308** (the method and body are kept, unlike 301/302).
* `UseHsts` (outside Development) adds `Strict-Transport-Security: max-age=31536000; includeSubDomains`, so a browser or client that has seen the API once refuses to use http for a year. HSTS is not sent in Development because `localhost` must not be pinned to a development certificate.
* Behind a reverse proxy set `ASPNETCORE_HTTPS_PORT` and forward the original scheme, otherwise the redirect cannot know the https port.
* The JWT bearer handler has `RequireHttpsMetadata = true`.

### Certificate validation and the `#if DEBUG` exception

Certificate validation is **never disabled in a Release build**. The only exception is in code that is compiled out of Release:

```csharp
#if DEBUG
    ServerCertificateCustomValidationCallback = (request, _, _, _) => request.RequestUri?.IsLoopback == true
#endif
```

It exists because the ASP.NET Core **development certificate** is not trusted by a phone emulator or by every console run. It applies in two places: `ApexPost.Client.Demo/Program.cs` and the MAUI app's `HttpClientHandler` (Android emulator reaching the host as `10.0.2.2`, iOS simulator as `localhost`). It accepts the certificate only when the request address is a loopback or the emulator's host alias, so it cannot make a call to a real server insecure, and `#if DEBUG` means the code does not exist in a Release build, so it cannot be switched on by a setting by mistake. Certificate pinning is not used; a production app would trust the platform store only.

## 6. Not covered (and what would come next)

* Refresh tokens, revocation lists and "sign out everywhere" (needs the identity provider).
* Account lock-out and multi-factor sign-in (the provider's job; here only a rate limit protects the demo sign-in).
* A global rate limit on all endpoints, and the real client address behind a proxy (forwarded headers).
* An audit log of who changed which shipment.
* Per-partner API keys stored hashed, with expiry.
* Penetration testing and dependency scanning in the build.

## 7. How to check it

1. `.\scripts\set-dev-secrets.ps1` (once), then start the API.
2. `.\scripts\smoke-api.ps1` and `.\scripts\smoke-api.ps1 -Version v1`: includes 401 (no, bad or wrong-kind credentials), 403 (courier creating, courier uploading as another courier) and the partner cases.
3. `ApexPost.Api/ApexPost.Api.http`: the same cases one by one, with the answers visible.
4. `dotnet test`: `ApexPost.UnitTests/Api/ApiSecurityTests.cs` covers the token claims, signature/audience/expiry checks, the courierId rule, the demo users, the key comparison and the signing-key rules.

## 8. The courier app (ApexPost.Courier)

* **Token storage.** The access token is kept only in MAUI `SecureStorage` (Android Keystore, Windows data protection), never in Preferences, files or logs. If the device store cannot be used (for example some unpackaged Windows setups) the token stays in memory for that run only, and the courier signs in again next time. The password is cleared from the form right after sign-in and is never stored.
* **401.** `AuthHeaderHandler` attaches the token to every call except the sign-in. On a `401` it deletes the token and the app shows the Login page; scans that are still `Pending` stay on the device.
* **What is logged.** Retry attempts, circuit breaker changes and sync runs. Tokens, API keys, passwords and request bodies are never logged.
* **The `#if DEBUG` certificate exception.** In `MauiProgram.cs` the Debug build accepts certificate errors only for requests to `localhost`, `127.0.0.1` and `10.0.2.2` (the Android emulator's address for the PC), because the ASP.NET Core development certificate is not trusted there. In a Release build this code does not exist, so certificate validation is never switched off in Release.
* **No secrets in the app.** The app contains only the development addresses of the local API. The demo password is typed by the user and is not in the repository.
* **A courier account is required.** The app refuses a token without a `courierId` claim (for example an Admin token), so it cannot record scans under a made-up courier.
