<#
.SYNOPSIS
    Runs all Windows publish scripts in the Apps directory.
.DESCRIPTION
    Discovers and executes all Publish-*Win.ps1 scripts (HistoricTempests, PwImageMetadataViewer,
    PwTrackTrimmer, WaterDaze, etc.), reporting progress and a final status summary.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'
$originalLocation = Get-Location
Push-Location $PSScriptRoot

try {
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host " Running All Windows Application Publish Scripts" -ForegroundColor Cyan
    Write-Host " Base Directory: $PSScriptRoot" -ForegroundColor DarkGray
    Write-Host " Start Time:     $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor DarkGray
    Write-Host "============================================================" -ForegroundColor Cyan

    $scripts = Get-ChildItem -Path $PSScriptRoot -Filter "Publish-*Win.ps1" |
        Where-Object { $_.Name -ne $MyInvocation.MyCommand.Name -and $_.Name -notlike "Publish-All*" } |
        Sort-Object Name

    if ($scripts.Count -eq 0) {
        Write-Warning "No Windows publish scripts (Publish-*Win.ps1) found in $PSScriptRoot."
        return
    }

    Write-Host "Found $($scripts.Count) Windows publish script(s):" -ForegroundColor Gray
    foreach ($s in $scripts) {
        Write-Host "  - $($s.Name)" -ForegroundColor Gray
    }
    Write-Host ""

    $results = [System.Collections.Generic.List[PSCustomObject]]::new()
    $overallStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $index = 0

    foreach ($script in $scripts) {
        $index++
        Write-Host "------------------------------------------------------------" -ForegroundColor Cyan
        Write-Host " [$index/$($scripts.Count)] Executing $($script.Name)..." -ForegroundColor Yellow
        Write-Host "------------------------------------------------------------" -ForegroundColor Cyan

        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $success = $false
        $errorMsg = $null

        try {
            $LASTEXITCODE = 0
            & ".\$($script.Name)"
            
            if ($LASTEXITCODE -ne 0) {
                throw "Script exited with code $LASTEXITCODE"
            }
            $success = $true
        }
        catch {
            $errorMsg = $_.Exception.Message
            Write-Host "ERROR in $($script.Name): $errorMsg" -ForegroundColor Red
        }
        finally {
            $sw.Stop()
        }

        $results.Add([PSCustomObject]@{
            Script   = $script.Name
            Status   = if ($success) { "Success" } else { "FAILED" }
            Duration = "$([math]::Round($sw.Elapsed.TotalSeconds, 1))s"
            Error    = if ($errorMsg) { $errorMsg } else { "-" }
        })

        Write-Host ""
    }

    $overallStopwatch.Stop()

    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host " Windows Publish Summary ($([math]::Round($overallStopwatch.Elapsed.TotalSeconds, 1))s total)" -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor Cyan

    foreach ($res in $results) {
        $color = if ($res.Status -eq "Success") { "Green" } else { "Red" }
        Write-Host (" {0,-40} [{1,7}] ({2})" -f $res.Script, $res.Status, $res.Duration) -ForegroundColor $color
        if ($res.Status -ne "Success") {
            Write-Host ("    Error: {0}" -f $res.Error) -ForegroundColor Red
        }
    }
    Write-Host ""

    $failedCount = ($results | Where-Object { $_.Status -ne "Success" }).Count
    if ($failedCount -gt 0) {
        Write-Host "Completed with $failedCount failure(s)." -ForegroundColor Red
        exit 1
    } else {
        Write-Host "All $($results.Count) Windows publish script(s) completed successfully!" -ForegroundColor Green
    }
}
finally {
    Pop-Location
}
