# Evidence checklist

Tick each item and put the screenshot or output next to it. Do not commit secrets: blur API keys and passwords.

## Build and tests
- [ ] `dotnet build ApexPost.slnx` output showing 0 Warning(s), 0 Error(s)
- [ ] `dotnet test ApexPost.slnx` output: all tests passed (unit and integration)

## Screens
- [ ] Login page, and the generic error after a wrong password
- [ ] Branch dashboard (sales, low stock, alerts)
- [ ] Catalogue and stock view; stock adjustment by a manager
- [ ] New counter transaction with a mixed basket (retail + shipping + money order)
- [ ] Money order above the threshold blocked, then accepted after identity verification (masked number visible)
- [ ] Shipping label preview
- [ ] Void by a manager (reason required); clerk gets the 403 page
- [ ] Four report pages
- [ ] Audit log page, and the downloaded JSON

## Middleware checks (Postman or curl, show status and headers)
- [ ] 500 when no API key is configured
- [ ] 401 without a key, 401 with a wrong key, 200 with the right key
- [ ] 403 from a blocked IP (use the `X-Test-Ip` style test or change the allowlist)
- [ ] 429 on login after 5 attempts; 429 on the partner endpoint after 30 calls
- [ ] `/health` while maintenance mode is on; 503 + Retry-After for other pages
- [ ] Response headers: CSP, X-Content-Type-Options, X-Frame-Options, Referrer-Policy, X-Correlation-Id
- [ ] CORS: approved origin gets Access-Control-Allow-Origin; another origin does not

## Persistence and performance measurements (write the numbers in DESIGN.md section 8)
- [ ] Stop and restart the app: a transaction created before the restart is still there
- [ ] Catalogue cache: hits vs loads after browsing the catalogue (the counters are on `ProductCatalog`)
- [ ] Task.WhenAll: dashboard time vs the three queries run one after another
- [ ] SemaphoreSlim: 10 parallel courier lookups with `MaxConcurrentLookups` = 3
