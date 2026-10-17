<#
.SYNOPSIS
    Runs all Android publish scripts in the Apps directory.
.DESCRIPTION
    Discovers and executes all Publish-*Android.ps1 scripts (HistoricTempests, PwImageMetadataViewer,
    PwTrackTrimmer, SiteViewer, WaterDaze, etc.). Accepts a signing password on the command line
    (or prompts once if omitted) and forwards it to all child scripts.
.PARAMETER Password
    Signing password for the keystore and private key. If not provided, the script prompts once interactively.
.PARAMETER KeyAlias
    Optional key alias to forward to the publish scripts.
.PARAMETER StorePassword
    Optional keystore password to forward (defaults to Password).
.EXAMPLE
    .\Publish-All-Android.ps1 -Password "YourSigningPassword"
.EXAMPLE
    .\Publish-All-Android.ps1
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0, Mandatory = $false, HelpMessage = "Password for the Android keystore and key")]
    [string]$Password,

    [Parameter(Position = 1, Mandatory = $false, HelpMessage = "Key alias in the keystore (optional)")]
    [string]$KeyAlias,

    [Parameter(Mandatory = $false, HelpMessage = "Password for the keystore store (defaults to Password)")]
    [string]$StorePassword
)

$ErrorActionPreference = 'Continue'
$originalLocation = Get-Location
Push-Location $PSScriptRoot

try {
    # 1. Prompt for password once if not provided on the command line
    if ([string]::IsNullOrWhiteSpace($Password)) {
        if ($PSVersionTable.PSVersion.Major -ge 6) {
            $Password = Read-Host "Enter Android signing password" -MaskInput
        } else {
            $secure = Read-Host "Enter Android signing password" -AsSecureString
            $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
            $Password = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
        }
    }

    if ([string]::IsNullOrWhiteSpace($Password)) {
        throw "A signing password must be provided."
    }

    if ([string]::IsNullOrWhiteSpace($StorePassword)) {
        $StorePassword = $Password
    }

    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host " Running All Android Application Publish Scripts" -ForegroundColor Cyan
    Write-Host " Base Directory: $PSScriptRoot" -ForegroundColor DarkGray
    Write-Host " Start Time:     $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor DarkGray
    Write-Host "============================================================" -ForegroundColor Cyan

    $scripts = Get-ChildItem -Path $PSScriptRoot -Filter "Publish-*Android.ps1" |
        Where-Object { $_.Name -ne $MyInvocation.MyCommand.Name -and $_.Name -notlike "Publish-All*" } |
        Sort-Object Name

    if ($scripts.Count -eq 0) {
        Write-Warning "No Android publish scripts (Publish-*Android.ps1) found in $PSScriptRoot."
        return
    }

    Write-Host "Found $($scripts.Count) Android publish script(s):" -ForegroundColor Gray
    foreach ($s in $scripts) {
        Write-Host "  - $($s.Name)" -ForegroundColor Gray
    }
    Write-Host ""

    # Build common script arguments hashtable for named parameter splatting
    $childArgs = @{
        Password = $Password
    }
    if (-not [string]::IsNullOrWhiteSpace($StorePassword)) {
        $childArgs['StorePassword'] = $StorePassword
    }
    if (-not [string]::IsNullOrWhiteSpace($KeyAlias)) {
        $childArgs['KeyAlias'] = $KeyAlias
    }

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
            & ".\$($script.Name)" @childArgs
            
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
    Write-Host " Android Publish Summary ($([math]::Round($overallStopwatch.Elapsed.TotalSeconds, 1))s total)" -ForegroundColor Cyan
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
        Write-Host "All $($results.Count) Android publish script(s) completed successfully!" -ForegroundColor Green
    }
}
finally {
    Pop-Location
}
