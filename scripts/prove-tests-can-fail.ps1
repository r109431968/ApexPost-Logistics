# prove-tests-can-fail.ps1  -  breaks production code on purpose and checks that the tests FAIL, then restores the code.
# Run from the repository root:  powershell -ExecutionPolicy Bypass -File .\scripts\prove-tests-can-fail.ps1
# Needs a clean working copy for the files below (it restores them with git checkout).

$ErrorActionPreference = 'Continue'
Set-Location (Resolve-Path (Join-Path $PSScriptRoot '..'))

$mutations = @(
    @{ Name = '1. Future-scan check removed (ParcelService)';
       File = 'ApexPost.Application\Services\ParcelService.cs';
       Find = 'if (scan.ScannedAtUtc > now + ClockSkew)';
       Replace = 'if (scan.ScannedAtUtc > now + ClockSkew + TimeSpan.FromDays(36500))';
       Tests = 'ApexPost.UnitTests'; Filter = $null },
    @{ Name = '2. Surcharge boundary > changed to >= (ShippingPricer), TDD tests';
       File = 'ApexPost.Application\Services\ShippingPricer.cs';
       Find = 'weight.Kilograms > settings.HeavyParcelThresholdKg';
       Replace = 'weight.Kilograms >= settings.HeavyParcelThresholdKg';
       Tests = 'ApexPost.UnitTests'; Filter = $null },
    @{ Name = '3. Outbox stops on first failure -> keeps going (OutboxService: break changed to continue)';
       File = 'ApexPost.Courier.Offline\Sync\OutboxService.cs';
       Regex = 'break;(\s*)// the network';
       Replace = 'continue;$1// the network';
       Tests = 'ApexPost.Courier.Offline.Tests'; Filter = $null },
    @{ Name = '4. COPILOT test UploadScans_DeliveredScanReachesFinalStatus: final-status check never fires';
       File = 'ApexPost.Application\Services\ParcelService.cs';
       Find = 'if (ParcelTransitions.IsFinal(status))';
       Replace = 'if (ParcelTransitions.IsFinal(status) && status == (ParcelStatus)(-1))';
       Tests = 'ApexPost.UnitTests'; Filter = 'FullyQualifiedName~DeliveredScanReachesFinalStatus' },
    @{ Name = '5. COPILOT test Delete_NonCreatedParcel: a PickedUp shipment may now be deleted';
       File = 'ApexPost.Application\Services\ParcelService.cs';
       Find = 'if (current.Status != ParcelStatus.Created)';
       Replace = 'if (current.Status != ParcelStatus.Created && current.Status != ParcelStatus.PickedUp)';
       Tests = 'ApexPost.UnitTests'; Filter = 'FullyQualifiedName~Delete_NonCreatedParcel' },
    @{ Name = '6. COPILOT BDD feature ParcelStatusTracking: status change is never saved';
       File = 'ApexPost.Application\Services\ParcelService.cs';
       Find = 'await parcels.SetStatusAsync(trackingNumber, current.Status, status, clock.UtcNow, cancellationToken);';
       Replace = 'await Task.CompletedTask;';
       Tests = 'ApexPost.Specs'; Filter = 'FullyQualifiedName~ParcelStatusTracking' }
)

$log = @("Mutation proof, " + (Get-Date -Format 'yyyy-MM-dd HH:mm'), "")
$allGood = $true

foreach ($m in $mutations) {
    Write-Host ""; Write-Host "=== Mutation $($m.Name)" -ForegroundColor Cyan
    $text = [IO.File]::ReadAllText((Resolve-Path $m.File))
    if ($m.Find) {
        if (-not $text.Contains($m.Find)) { Write-Host "Could not find the code to break. Skipping." -ForegroundColor Yellow; $log += "$($m.Name): SKIPPED (code not found)"; $allGood = $false; continue }
        $broken = $text.Replace($m.Find, $m.Replace)
    } else {
        if ($text -notmatch $m.Regex) { Write-Host "Could not find the code to break. Skipping." -ForegroundColor Yellow; $log += "$($m.Name): SKIPPED (code not found)"; $allGood = $false; continue }
        $broken = [regex]::Replace($text, $m.Regex, $m.Replace)
    }
    try {
        [IO.File]::WriteAllText((Resolve-Path $m.File), $broken, (New-Object Text.UTF8Encoding($false)))
        $a = @('test', $m.Tests, '--nologo')
        if ($m.Filter) { $a += @('--filter', $m.Filter) }
        $output = & dotnet @a 2>&1 | Out-String
        $failed = ($LASTEXITCODE -ne 0)
        $firstFail = ($output -split "`n" | Where-Object { $_ -match '^\s*Failed ' } | Select-Object -First 2) -join "; "
        $reason = ($output -split "`n" | Where-Object { $_ -match 'Expected:|Actual:|Assert\.' } | Select-Object -First 3) -join " | "
    }
    finally {
        git checkout -- $m.File
    }
    if ($failed) { Write-Host "GOOD: the tests FAILED, so they catch this bug.  $firstFail" -ForegroundColor Green; $log += "$($m.Name): tests FAILED as expected. $firstFail  [$reason]" }
    else         { Write-Host "BAD: the tests still PASSED. They do not protect this code." -ForegroundColor Red; $log += "$($m.Name): tests PASSED (not protected!)"; $allGood = $false }
}

New-Item -ItemType Directory -Force -Path 'docs\week5' | Out-Null
$log | Set-Content -Path 'docs\week5\mutation-proof.log' -Encoding UTF8
Write-Host ""; git status --short | Select-String -NotMatch 'Screenshots|api-docs|week3'
if ($allGood) { Write-Host "All mutations were caught. Log: docs\week5\mutation-proof.log" -ForegroundColor Green }
else { Write-Host "At least one mutation was NOT caught or skipped. Log: docs\week5\mutation-proof.log" -ForegroundColor Red }
