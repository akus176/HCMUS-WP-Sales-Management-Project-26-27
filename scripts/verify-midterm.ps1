[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($env:COFFEESHOP_TEST_POSTGRES)) {
    throw 'Set COFFEESHOP_TEST_POSTGRES to an isolated PostgreSQL administrator connection. A skipped database demonstration is not a pass.'
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resultsDirectory = Join-Path $repositoryRoot 'artifacts/midterm'
$testProjects = @(
    'CoffeeShop.Domain.Tests',
    'CoffeeShop.Application.Tests',
    'CoffeeShop.Infrastructure.Tests',
    'CoffeeShop.WinUI.Tests',
    'CoffeeShop.Api.Tests'
)

Push-Location $repositoryRoot
try {
    New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null

    dotnet restore CoffeeShop.slnx
    if ($LASTEXITCODE -ne 0) { throw 'Midterm restore failed.' }
    dotnet build CoffeeShop.slnx --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Midterm Release build failed.' }
    dotnet format CoffeeShop.slnx --verify-no-changes --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Midterm formatting check failed.' }

    $totalPassed = 0
    foreach ($project in $testProjects) {
        $resultPath = Join-Path $resultsDirectory "$project.trx"
        if (Test-Path -LiteralPath $resultPath) { Remove-Item -LiteralPath $resultPath }

        dotnet test "tests/$project/$project.csproj" --configuration Release --no-build --no-restore `
            --logger "trx;LogFileName=$project.trx" --results-directory $resultsDirectory
        if ($LASTEXITCODE -ne 0) { throw "$project failed. Inspect $resultPath." }
        if (-not (Test-Path -LiteralPath $resultPath)) { throw "$project produced no TRX evidence." }

        [xml]$testRun = Get-Content -LiteralPath $resultPath -Raw
        $counters = $testRun.TestRun.ResultSummary.Counters
        $passed = [int]$counters.passed
        if ($passed -eq 0 -or [int]$counters.failed -ne 0 -or [int]$counters.notExecuted -ne 0 -or [int]$counters.executed -ne $passed) {
            throw "$project did not pass every test. Inspect $resultPath."
        }
        $totalPassed += $passed
        Write-Output "$project`: $passed passed, zero failures/skips."
    }

    Write-Output "Midterm verification passed: $totalPassed tests, clean Release build and format. TRX evidence: artifacts/midterm/."
}
finally { Pop-Location }
