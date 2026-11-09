# Stores random DEVELOPMENT secrets in user-secrets for ApexPost.Api. Nothing is written to a file of the repository.
# Run it once:  .\scripts\set-dev-secrets.ps1        (use -Force to replace secrets that already exist)
#
#   Auth:Jwt:SigningKey  signs and checks the access tokens
#   Auth:DemoPassword    the password of the fictional users courier007, courier008 and admin
#   Partner:ApiKey       the X-Api-Key of the partner route
#
# In production the same three names are supplied by the environment or a secret store (Auth__Jwt__SigningKey, ...), see SECURITY.md.
param([switch]$Force)

$project = Join-Path $PSScriptRoot "..\ApexPost.Api"

function New-Hex([int]$bytes) {
    $buffer = New-Object byte[] $bytes
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $generator.GetBytes($buffer)
    $generator.Dispose()
    ($buffer | ForEach-Object { $_.ToString("x2") }) -join ""
}

function Get-Existing($name) {
    $line = dotnet user-secrets list --project $project | Where-Object { $_ -like "$name = *" } | Select-Object -First 1
    if ($line) { $line.Substring($name.Length + 3) }
}

function Set-DevSecret($name, $value) {
    $current = Get-Existing $name
    if ($current -and -not $Force) {
        Write-Host ("{0,-20} kept (already set; use -Force to replace)" -f $name)
        return $current
    }
    dotnet user-secrets set $name $value --project $project | Out-Null
    Write-Host ("{0,-20} set" -f $name)
    return $value
}

$null = Set-DevSecret "Auth:Jwt:SigningKey" (New-Hex 48)
$password = Set-DevSecret "Auth:DemoPassword" (New-Hex 12)
$key = Set-DevSecret "Partner:ApiKey" ("apx_" + (New-Hex 24))

Write-Host ""
Write-Host "Copy these two values into the variables at the top of ApexPost.Api/ApexPost.Api.http (do not commit them):"
Write-Host "  @demoPassword = $password"
Write-Host "  @partnerKey   = $key"
Write-Host "scripts/smoke-api.ps1 reads them from user-secrets by itself."
