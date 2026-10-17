<#
.SYNOPSIS
    Runs all application publish suites: Windows, Web, and Android.
.DESCRIPTION
    Sequentially executes Publish-All-Win.ps1, Publish-All-Web.ps1, and Publish-All-Android.ps1.
    Accepts an Android keystore password on the command line (or prompts interactively once upfront)
    and forwards it to Publish-All-Android.ps1.
.PARAMETER Password
    Signing password for Android builds. Can be passed positionally or via -Password / -AndroidPassword.
.PARAMETER KeyAlias
    Optional key alias to forward to Publish-All-Android.ps1.
.PARAMETER StorePassword
    Optional keystore password to forward to Publish-All-Android.ps1 (defaults to Password).
.EXAMPLE
    .\Publish-All.ps1 -Password "YourSigningPassword"
.EXAMPLE
    .\Publish-All.ps1 YourSigningPassword
.EXAMPLE
    .\Publish-All.ps1
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0, Mandatory = $false, HelpMessage = "Password for the Android keystore and key")]
    [Alias("AndroidPassword")]
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
    # Prompt for Android password upfront if not provided so the entire pipeline runs unattended
    if ([string]::IsNullOrWhiteSpace($Password)) {
        Write-Host "An Android signing password is required for the Android publish step." -ForegroundColor Yellow
        if ($PSVersionTable.PSVersion.Major -ge 6) {
            $Password = Read-Host "Enter Android signing password" -MaskInput
        } else {
            $secure = Read-Host "Enter Android signing password" -AsSecureString
            $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
            $Password = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
        }
    }

    if ([string]::IsNullOrWhiteSpace($Password)) {
        throw "A signing password must be provided for Android publishing."
    }

    if ([string]::IsNullOrWhiteSpace($StorePassword)) {
        $StorePassword = $Password
    }

    $androidArgs = @{
        Password = $Password
    }
    if (-not [string]::IsNullOrWhiteSpace($StorePassword)) {
        $androidArgs['StorePassword'] = $StorePassword
    }
    if (-not [string]::IsNullOrWhiteSpace($KeyAlias)) {
        $androidArgs['KeyAlias'] = $KeyAlias
    }

    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host " Running Master Publish Suite (Windows, Web, Android)" -ForegroundColor Cyan
    Write-Host " Base Directory: $PSScriptRoot" -ForegroundColor DarkGray
    Write-Host " Start Time:     $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor DarkGray
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host ""

    $suites = @(
        @{
            Name       = "Publish-All-Win.ps1"
            ScriptPath = ".\Publish-All-Win.ps1"
            RunBlock   = { & ".\Publish-All-Win.ps1" }
        },
        @{
            Name       = "Publish-All-Web.ps1"
            ScriptPath = ".\Publish-All-Web.ps1"
            RunBlock   = { & ".\Publish-All-Web.ps1" }
        },
        @{
            Name       = "Publish-All-Android.ps1"
            ScriptPath = ".\Publish-All-Android.ps1"
            RunBlock   = { & ".\Publish-All-Android.ps1" @androidArgs }
        }
    )

    $results = [System.Collections.Generic.List[PSCustomObject]]::new()
    $masterStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $index = 0

    foreach ($suite in $suites) {
        $index++
        Write-Host "############################################################" -ForegroundColor Magenta
        Write-Host " [$index/$($suites.Count)] Starting Suite: $($suite.Name)" -ForegroundColor Yellow
        Write-Host "############################################################" -ForegroundColor Magenta
        Write-Host ""

        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $success = $false
        $errorMsg = $null

        try {
            $LASTEXITCODE = 0
            & $suite.RunBlock

            if ($LASTEXITCODE -ne 0) {
                throw "$($suite.Name) exited with code $LASTEXITCODE"
            }
            $success = $true
        }
        catch {
            $errorMsg = $_.Exception.Message
            Write-Host "ERROR in $($suite.Name): $errorMsg" -ForegroundColor Red
        }
        finally {
            $sw.Stop()
        }

        $results.Add([PSCustomObject]@{
            Suite    = $suite.Name
            Status   = if ($success) { "Success" } else { "FAILED" }
            Duration = "$([math]::Round($sw.Elapsed.TotalSeconds, 1))s"
            Error    = if ($errorMsg) { $errorMsg } else { "-" }
        })

        Write-Host ""
    }

    $masterStopwatch.Stop()

    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host " Master Publish Summary ($([math]::Round($masterStopwatch.Elapsed.TotalSeconds, 1))s total)" -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor Cyan

    foreach ($res in $results) {
        $color = if ($res.Status -eq "Success") { "Green" } else { "Red" }
        Write-Host (" {0,-30} [{1,7}] ({2})" -f $res.Suite, $res.Status, $res.Duration) -ForegroundColor $color
        if ($res.Status -ne "Success") {
            Write-Host ("    Error: {0}" -f $res.Error) -ForegroundColor Red
        }
    }
    Write-Host ""

    $failedCount = ($results | Where-Object { $_.Status -ne "Success" }).Count
    if ($failedCount -gt 0) {
        Write-Host "Master publish completed with $failedCount failed suite(s)." -ForegroundColor Red
        exit 1
    } else {
        Write-Host "All publish suites completed successfully!" -ForegroundColor Green
    }
}
finally {
    Pop-Location
}
