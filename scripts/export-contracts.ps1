# Saves the two OpenAPI documents of the running API into /contracts.
# Start the API first:  dotnet run --project ApexPost.Api
param([string]$Base = "https://localhost:7241")

New-Item -ItemType Directory -Force "$PSScriptRoot\..\contracts" | Out-Null
foreach ($v in "v1", "v2") {
    $target = "$PSScriptRoot\..\contracts\$v.json"
    curl.exe -k -s -f "$Base/openapi/$v.json" -o $target
    if ($LASTEXITCODE -ne 0) { throw "Could not download $Base/openapi/$v.json. Is the API running in Development?" }
    Write-Host "Saved $target ($((Get-Item $target).Length) bytes)"
}
