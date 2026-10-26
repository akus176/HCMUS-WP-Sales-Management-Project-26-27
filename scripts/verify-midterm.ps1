[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($env:COFFEESHOP_TEST_POSTGRES)) {
    throw 'Set COFFEESHOP_TEST_POSTGRES to an isolated PostgreSQL administrator connection. A skipped database demonstration is not a pass.'
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resultsDirectory = Join-Path $repositoryRoot 'artifacts/midterm'
Push-Location $repositoryRoot
try {
    dotnet test tests/CoffeeShop.Infrastructure.Tests --configuration Release `
        --filter 'FullyQualifiedName~DirectClientCompositionTests|FullyQualifiedName~MidtermSeedDataIntegrationTests|FullyQualifiedName~MidtermDemonstrationIntegrationTests' `
        --logger 'console;verbosity=detailed' --logger 'trx;LogFileName=midterm.trx' `
        --results-directory $resultsDirectory
    if ($LASTEXITCODE -ne 0) { throw "Midterm demonstration failed (exit $LASTEXITCODE)." }
    [xml]$results = Get-Content -LiteralPath (Join-Path $resultsDirectory 'midterm.trx') -Raw
    $counters = $results.TestRun.ResultSummary.Counters
    if ([int]$counters.executed -eq 0 -or [int]$counters.notExecuted -ne 0 -or [int]$counters.failed -ne 0) {
        throw 'Midterm demonstration did not execute every selected check successfully. Inspect artifacts/midterm/midterm.trx.'
    }
    Write-Output "Midterm demonstration passed: $($counters.passed) checks, zero failures/skips. Evidence: artifacts/midterm/midterm.trx"
}
finally { Pop-Location }
