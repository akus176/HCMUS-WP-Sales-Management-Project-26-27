[CmdletBinding()]
param(
    [string]$OutputDirectory = 'artifacts/ui-parity-review',
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__CoffeeShop)) {
    throw 'Set ConnectionStrings__CoffeeShop to a disposable database migrated and populated with midterm-seed.sql and ui-parity-seed.sql from scripts/postgres.'
}
if (-not [IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot $OutputDirectory
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) {
    throw 'Use a new evidence directory for each run so stale captures cannot be accepted.'
}
$executable = Join-Path $repositoryRoot 'src/CoffeeShop.WinUI/bin/Release/net10.0-windows10.0.19041.0/win-x64/CoffeeShop.WinUI.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build the WinUI project in Release before capture.' }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$previousDirectory = $env:COFFEESHOP_UI_EVIDENCE
try {
    $env:COFFEESHOP_UI_EVIDENCE = $OutputDirectory
    $process = Start-Process -FilePath $executable -ArgumentList '--verify-shell', '--verify-parity', '--verify-exit' -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        $process.Kill()
        throw 'Native verification timed out. Check the database connection and inspect the application error report.'
    }
    $errorPath = Join-Path $OutputDirectory 'parity-error.txt'
    if (Test-Path -LiteralPath $errorPath) { throw (Get-Content -LiteralPath $errorPath -Raw) }
    $reportPath = Join-Path $OutputDirectory 'parity-verification.json'
    if (-not (Test-Path -LiteralPath $reportPath)) { throw 'Application exited without a native verification report.' }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    Push-Location $repositoryRoot
    try {
        $manifest = [ordered]@{
            capturedAtUtc = [DateTime]::UtcNow.ToString('o')
            implementationCommit = (git rev-parse HEAD)
            dirtyWorktree = [bool](git status --porcelain)
            conceptCommit = (git log -1 --format=%H -- docs/ui-concepts)
            referenceDirectory = 'docs/ui-evidences'
            seedFile = 'scripts/postgres/midterm-seed.sql'
            seedSha256 = (Get-FileHash scripts/postgres/midterm-seed.sql -Algorithm SHA256).Hash
            supplementFile = 'scripts/postgres/ui-parity-seed.sql'
            supplementSha256 = (Get-FileHash scripts/postgres/ui-parity-seed.sql -Algorithm SHA256).Hash
            executableSha256 = (Get-FileHash $executable -Algorithm SHA256).Hash
            changedSources = @(git ls-files --modified --others --exclude-standard -- src tests scripts | Where-Object {
                Test-Path -LiteralPath $_ -PathType Leaf
            } | ForEach-Object {
                [ordered]@{ path = $_; sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
            })
            visualAcceptance = 'Pending visual review against docs/ui-evidences and current user instructions'
        }
        $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'manifest.json') -Encoding utf8
    }
    finally { Pop-Location }
    $failures = @($report.results | Where-Object {
        -not $_.samePage -or -not $_.statePreserved -or -not $_.keyboardFocus -or
        -not $_.primaryTargetsPass -or -not $_.namesPass -or
        [Math]::Abs($_.actualWidth - $_.width) -gt 1 -or [Math]::Abs($_.actualHeight - 900) -gt 1 -or
        @($_.horizontalOverflow | Where-Object pageLevel).Count -gt 0
    })
    $smokeFailures = @($report.smoke | Where-Object {
        $_.checks.skipped -eq $true -or @($_.checks.PSObject.Properties | Where-Object {
            $_.Value -is [bool] -and $_.Name -ne 'skipped' -and -not $_.Value
        }).Count -gt 0
    })
    $polishFailures = @($report.results | Where-Object {
        ($_.tag -eq 'counter' -and $_.layout.menuItems -gt 0 -and -not $_.layout.threeRowsFit) -or
        @($_.navigation | Where-Object { $null -ne $_.contentInset -and $_.contentInset -lt 11 }).Count -gt 0
    })
    foreach ($board in @($report.smoke | Where-Object tag -eq 'service-board')) {
        if (-not $board.checks.forms.paymentOpens -or -not $board.checks.forms.formsReset -or -not $board.checks.forms.deliveryIsContextual -or
            @($board.checks.measurements | Where-Object { $_.notes -lt 1 -or -not $_.matchesPaper }).Count -gt 0) {
            $smokeFailures += $board
        }
    }
    if (-not $report.defaultIsCounter -or -not $report.connected -or $report.destinations.Count -ne 6 -or $report.results.Count -ne 42 -or $report.smoke.Count -ne 12 -or $failures.Count -gt 0 -or $smokeFailures.Count -gt 0 -or $polishFailures.Count -gt 0 -or -not $report.editor.compactPreserved -or -not $report.editor.expandedPreserved -or -not $report.editor.savedRecordUnchanged) {
        throw "Native checks failed in $($failures.Count) layout rows, $($smokeFailures.Count) smoke rows, and $($polishFailures.Count) polish rows. Inspect $reportPath; screenshots are candidates, not accepted parity evidence."
    }
    Write-Output "All six workspaces captured at seven breakpoint widths. Native checks passed; review screenshots against docs/ui-evidences: $OutputDirectory"
}
finally { $env:COFFEESHOP_UI_EVIDENCE = $previousDirectory }
