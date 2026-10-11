# Runs the automated LeafletScrollIsolationHelper test suite
$testScript = Join-Path $PSScriptRoot "LeafletScrollIsolationTests.js"
node $testScript
if ($LASTEXITCODE -ne 0) {
    Write-Error "LeafletScrollIsolationHelper tests failed."
    exit $LASTEXITCODE
} else {
    Write-Host "All LeafletScrollIsolationHelper tests passed successfully." -ForegroundColor Green
}
