# Smoke test for ApexPost.Api. Start the API first:  dotnet run --project ApexPost.Api
# Then, in another terminal:  .\scripts\smoke-api.ps1            (version 2)
#                              .\scripts\smoke-api.ps1 -Version v1  (deprecated version 1)
# It prints expected and actual HTTP status codes for every case. Uses curl.exe (-k accepts the local dev certificate).
param([string]$Base = "https://localhost:7241", [ValidateSet("v1","v2")][string]$Version = "v2")
$api = "/api/$Version"

$tracking = "APX-" + (Get-Random -Minimum 20000000 -Maximum 29999999)
$tmp = Join-Path $env:TEMP "apexpost-smoke"
New-Item -ItemType Directory -Force $tmp | Out-Null
$results = @()

function Send($name, $expected, $method, $path, $json = $null, $contentType = "application/json") {
    $args = @("-k", "-s", "-o", "$tmp\last.txt", "-w", "%{http_code}", "-X", $method, "$Base$path")
    if ($null -ne $json) {
        Set-Content -Path "$tmp\body.json" -Value $json -Encoding utf8 -NoNewline
        $args += @("-H", "Content-Type: $contentType", "--data-binary", "@$tmp\body.json")
    }
    $actual = & curl.exe @args
    $script:results += [pscustomobject]@{ Case = $name; Expected = $expected; Actual = $actual; Ok = ($actual -eq "$expected") }
}

function ParcelJson($tn) {
    @{ trackingNumber = $tn; weightKg = 2.5; recipientName = "Gita Rao"; recipientPhone = "+44 20 7946 0001"; recipientAddress = "7 Mill Road"; recipientCity = "Brightport" } | ConvertTo-Json
}

$update = @{ weightKg = 3.1; recipientName = "Gita Rao"; recipientPhone = "+44 20 7946 0001"; recipientAddress = "9 Mill Road"; recipientCity = "Brightport" } | ConvertTo-Json

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
$scanA = "scan::" + [guid]::NewGuid(); $scanB = "scan::" + [guid]::NewGuid()
$scans = @{ courierId = "courier-007"; scans = @(
    @{ scanId = $scanA; scanType = "InTransit"; location = "Brightport hub"; scannedAtUtc = (Get-Date).ToUniversalTime().AddHours(-2).ToString("o") },
    @{ scanId = $scanB; scanType = "InTransit"; location = "Northvale depot"; scannedAtUtc = (Get-Date).ToUniversalTime().AddHours(-1).ToString("o") }) } | ConvertTo-Json -Depth 5
Send "upload 2 scans"                200 POST   "$api/shipments/$tracking/scans" $scans
$first = Get-Content "$tmp\last.txt" -Raw
Send "upload same scans again"       200 POST   "$api/shipments/$tracking/scans" $scans
$second = Get-Content "$tmp\last.txt" -Raw
Send "upload delivered w/o signature" 400 POST  "$api/shipments/$tracking/scans" (@{ courierId = "courier-007"; scans = @(@{ scanId = "scan::" + [guid]::NewGuid(); scanType = "Delivered"; location = "x"; scannedAtUtc = (Get-Date).ToUniversalTime().AddMinutes(-5).ToString("o") }) } | ConvertTo-Json -Depth 5)
Send "upload unknown shipment"       404 POST   "$api/shipments/APX-99999999/scans" $scans
Send "scan history"                  200 GET    "$api/shipments/$tracking/scans"
Send "delete (not Created)"          409 DELETE "$api/shipments/$tracking"
$other = "APX-" + (Get-Random -Minimum 30000000 -Maximum 39999999)
Send "create another"                201 POST   "$api/shipments" (ParcelJson $other)
Send "delete (Created)"              204 DELETE "$api/shipments/$other"
Send "delete unknown"                404 DELETE "$api/shipments/APX-99999999"

$results | Format-Table -AutoSize
Write-Host "First upload : $first"
Write-Host "Second upload: $second   (accepted must be 0, duplicatesSkipped must be 2)"
$bad = @($results | Where-Object { -not $_.Ok }).Count
if ($bad -eq 0) { Write-Host "ALL $($results.Count) CASES PASSED" -ForegroundColor Green } else { Write-Host "$bad CASE(S) FAILED" -ForegroundColor Red }
