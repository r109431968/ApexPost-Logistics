# Smoke test for ApexPost.Api. Start the API first:  dotnet run --project ApexPost.Api --launch-profile https
# Run .\scripts\set-dev-secrets.ps1 once before the first run (it stores the demo password and the partner key in user-secrets).
# Then, in another terminal:  .\scripts\smoke-api.ps1            (version 2, includes sign-in and partner cases)
#                              .\scripts\smoke-api.ps1 -Version v1  (deprecated version 1)
# It prints expected and actual HTTP status codes for every case. Uses curl.exe (-k accepts the local dev certificate).
param(
    [string]$Base = "https://localhost:7241",
    [ValidateSet("v1","v2")][string]$Version = "v2",
    [string]$DemoPassword = $env:APEXPOST_DEMO_PASSWORD,
    [string]$PartnerKey = $env:APEXPOST_PARTNER_KEY)
$api = "/api/$Version"
$project = Join-Path $PSScriptRoot "..\ApexPost.Api"

function Get-DevSecret($name) {
    $line = dotnet user-secrets list --project $project | Where-Object { $_ -like "$name = *" } | Select-Object -First 1
    if ($line) { $line.Substring($name.Length + 3) }
}
if (-not $DemoPassword) { $DemoPassword = Get-DevSecret "Auth:DemoPassword" }
if (-not $PartnerKey) { $PartnerKey = Get-DevSecret "Partner:ApiKey" }
if (-not $DemoPassword -or -not $PartnerKey) { throw "No demo password or partner key. Run .\scripts\set-dev-secrets.ps1 first." }

$tracking = "APX-" + (Get-Random -Minimum 20000000 -Maximum 29999999)
$tmp = Join-Path $env:TEMP "apexpost-smoke"
New-Item -ItemType Directory -Force $tmp | Out-Null
$results = @()

function Get-Token($user) {
    $body = @{ userName = $user; password = $DemoPassword } | ConvertTo-Json
    Set-Content -Path "$tmp\login.json" -Value $body -Encoding utf8 -NoNewline
    $text = & curl.exe -k -s -X POST "$Base/api/v2/auth/token" -H "Content-Type: application/json" --data-binary "@$tmp\login.json"
    $token = ($text | ConvertFrom-Json).accessToken
    if (-not $token) { throw "Sign-in as $user failed: $text" }
    $token
}
$tokens = @{ admin = Get-Token "admin"; courier7 = Get-Token "courier007"; courier8 = Get-Token "courier008" }

function AuthArgs($who) {
    switch ($who) {
        "admin"    { @("-H", "Authorization: Bearer $($script:tokens.admin)") }
        "courier7" { @("-H", "Authorization: Bearer $($script:tokens.courier7)") }
        "courier8" { @("-H", "Authorization: Bearer $($script:tokens.courier8)") }
        "bad"      { @("-H", "Authorization: Bearer not.a.token") }
        "key"      { @("-H", "X-Api-Key: $PartnerKey") }
        "wrongkey" { @("-H", "X-Api-Key: wrong-key") }
        default    { @() }
    }
}

# $who: admin | courier7 | courier8 | bad | key | wrongkey | none
function Send($name, $expected, $method, $path, $json = $null, $contentType = "application/json", $who = "admin") {
    $curlArgs = @("-k", "-s", "-o", "$tmp\last.txt", "-w", "%{http_code}", "-X", $method, "$Base$path") + (AuthArgs $who)
    if ($null -ne $json) {
        Set-Content -Path "$tmp\body.json" -Value $json -Encoding utf8 -NoNewline
        $curlArgs += @("-H", "Content-Type: $contentType", "--data-binary", "@$tmp\body.json")
    }
    $actual = & curl.exe @curlArgs
    $script:results += [pscustomobject]@{ Case = $name; Expected = $expected; Actual = $actual; Ok = ($actual -eq "$expected") }
}

function ParcelJson($tn) {
    @{ trackingNumber = $tn; weightKg = 2.5; recipientName = "Gita Rao"; recipientPhone = "+44 20 7946 0001"; recipientAddress = "7 Mill Road"; recipientCity = "Brightport" } | ConvertTo-Json
}
function ScansJson($courier, $type = "InTransit", $extra = @{}) {
    @{ courierId = $courier; scans = @(
        @{ scanId = "scan::" + [guid]::NewGuid(); scanType = $type; location = "Brightport hub"; scannedAtUtc = (Get-Date).ToUniversalTime().AddMinutes(-5).ToString("o") } + $extra) } | ConvertTo-Json -Depth 5
}

$update = @{ weightKg = 3.1; recipientName = "Gita Rao"; recipientPhone = "+44 20 7946 0001"; recipientAddress = "9 Mill Road"; recipientCity = "Brightport" } | ConvertTo-Json

# --- security: who may call what -------------------------------------------------------------------------------------
Send "list without a token"          401 GET    "$api/shipments" -who none
Send "list with a bad token"         401 GET    "$api/shipments" -who bad
Send "list with an API key"          401 GET    "$api/shipments" -who key
Send "courier can list"              200 GET    "$api/shipments?page=1&pageSize=5" -who courier7
Send "courier cannot create"         403 POST   "$api/shipments" (ParcelJson "APX-29999990") -who courier7
Send "courier cannot delete"         403 DELETE "$api/shipments/APX-10000001" -who courier7

# --- the shipment flow (admin) ---------------------------------------------------------------------------------------
Send "list"                          200 GET    "$api/shipments?page=1&pageSize=5"
Send "old unversioned address"       404 GET    "/api/shipments"
Send "list pageSize 500"             400 GET    "$api/shipments?pageSize=500"
Send "create"                        201 POST   "$api/shipments" (ParcelJson $tracking)
Send "create duplicate"              409 POST   "$api/shipments" (ParcelJson $tracking)
Send "create invalid"                400 POST   "$api/shipments" '{"trackingNumber":"BAD-1","weightKg":99,"recipientName":"","recipientPhone":"abc","recipientAddress":"x","recipientCity":"y"}'
Send "create wrong content type"     415 POST   "$api/shipments" "hello" "text/plain"
Send "get"                           200 GET    "$api/shipments/$tracking"
Send "get unknown"                   404 GET    "$api/shipments/APX-99999999"
Send "replace"                       200 PUT    "$api/shipments/$tracking" $update
Send "replace unknown"               404 PUT    "$api/shipments/APX-99999999" $update
Send "status PickedUp"               200 PATCH  "$api/shipments/$tracking/status" '{"status":"PickedUp"}'
Send "status skips steps"            409 PATCH  "$api/shipments/$tracking/status" '{"status":"Delivered"}'
Send "status unknown name"           400 PATCH  "$api/shipments/$tracking/status" '{"status":"Teleported"}'

# --- scans: a courier uploads only under their own courierId ---------------------------------------------------------
$scanA = "scan::" + [guid]::NewGuid(); $scanB = "scan::" + [guid]::NewGuid()
$scans = @{ courierId = "courier-007"; scans = @(
    @{ scanId = $scanA; scanType = "InTransit"; location = "Brightport hub"; scannedAtUtc = (Get-Date).ToUniversalTime().AddHours(-2).ToString("o") },
    @{ scanId = $scanB; scanType = "InTransit"; location = "Northvale depot"; scannedAtUtc = (Get-Date).ToUniversalTime().AddHours(-1).ToString("o") }) } | ConvertTo-Json -Depth 5
Send "upload 2 scans (courier-007)"  200 POST   "$api/shipments/$tracking/scans" $scans -who courier7
$first = Get-Content "$tmp\last.txt" -Raw
Send "upload same scans again"       200 POST   "$api/shipments/$tracking/scans" $scans -who courier7
$second = Get-Content "$tmp\last.txt" -Raw
Send "upload as another courier"     403 POST   "$api/shipments/$tracking/scans" (ScansJson "courier-008") -who courier7
Send "other courier's token"         403 POST   "$api/shipments/$tracking/scans" $scans -who courier8
Send "admin uploads for a courier"   200 POST   "$api/shipments/$tracking/scans" (ScansJson "courier-008") -who admin
Send "upload delivered w/o signature" 400 POST  "$api/shipments/$tracking/scans" (ScansJson "courier-007" "Delivered") -who courier7
Send "upload unknown shipment"       404 POST   "$api/shipments/APX-99999999/scans" $scans -who courier7
Send "scan history"                  200 GET    "$api/shipments/$tracking/scans" -who courier7
Send "delete (not Created)"          409 DELETE "$api/shipments/$tracking"
$other = "APX-" + (Get-Random -Minimum 30000000 -Maximum 39999999)
Send "create another"                201 POST   "$api/shipments" (ParcelJson $other)
Send "delete (Created)"              204 DELETE "$api/shipments/$other"
Send "delete unknown"                404 DELETE "$api/shipments/APX-99999999"

# --- version 2 only: sign-in and the partner route -------------------------------------------------------------------
if ($Version -eq "v2") {
    Send "sign-in: wrong password"       401 POST "/api/v2/auth/token" '{"userName":"admin","password":"wrong"}' -who none
    Send "sign-in: empty body fields"    400 POST "/api/v2/auth/token" '{"userName":"","password":""}' -who none
    Send "partner: valid key"            200 GET  "/api/v2/partner/shipments/$tracking" -who key
    $partner = Get-Content "$tmp\last.txt" -Raw
    Send "partner: no key"               401 GET  "/api/v2/partner/shipments/$tracking" -who none
    Send "partner: wrong key"            401 GET  "/api/v2/partner/shipments/$tracking" -who wrongkey
    Send "partner: a JWT is not enough"  401 GET  "/api/v2/partner/shipments/$tracking" -who admin
    Send "partner: unknown shipment"     404 GET  "/api/v2/partner/shipments/APX-99999999" -who key
}

$results | Format-Table -AutoSize
Write-Host "First upload : $first"
Write-Host "Second upload: $second   (accepted must be 0, duplicatesSkipped must be 2)"
if ($Version -eq "v2") { Write-Host "Partner view : $partner   (no recipient name, phone or address)" }
$bad = @($results | Where-Object { -not $_.Ok }).Count
if ($bad -eq 0) { Write-Host "ALL $($results.Count) CASES PASSED" -ForegroundColor Green } else { Write-Host "$bad CASE(S) FAILED" -ForegroundColor Red }
