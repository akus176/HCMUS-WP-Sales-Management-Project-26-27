param([string]$ResultsDirectory = '.maestro/results/quality')
$ErrorActionPreference = 'Stop'
# Run from apps/staff-mobile against freshly provisioned, disposable accounts.
$roles = @('Manager', 'DeliveryStaff')
foreach ($role in $roles) {
    if (![Environment]::GetEnvironmentVariable("COFFEESHOP_E2E_${role}_USERNAME")) { throw "Set COFFEESHOP_E2E_${role}_USERNAME." }
}
if (!$env:COFFEESHOP_E2E_PASSWORD) { throw 'Set the synthetic accounts password in COFFEESHOP_E2E_PASSWORD.' }
foreach ($command in @('maestro', 'adb')) { $null = Get-Command $command -ErrorAction Stop }
New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null
$flows = @(
    @('session', 'DeliveryStaff'), @('courier', 'DeliveryStaff'),
    @('notification-links', 'DeliveryStaff')
)
# Reset the summary before fixture setup, so a failed seed cannot leave a green report.
@{ expected = $flows.Count; passed = 0; failedOrMissing = $flows.Count; serverVerificationPassed = $false; workflows = @() } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ResultsDirectory 'summary.json')
$env:COFFEESHOP_MOBILE_TEST_MANAGER_USERNAME = $env:COFFEESHOP_E2E_MANAGER_USERNAME
$env:COFFEESHOP_MOBILE_TEST_MANAGER_PASSWORD = $env:COFFEESHOP_E2E_PASSWORD
$courierPath = Join-Path $ResultsDirectory 'courier-fixture.json'
$courierSession = Invoke-RestMethod -Method Post -Uri "$env:COFFEESHOP_MOBILE_TEST_API_BASE_URL/auth/login" -ContentType 'application/json' -Body (@{
    username = $env:COFFEESHOP_E2E_DELIVERYSTAFF_USERNAME; password = $env:COFFEESHOP_E2E_PASSWORD
} | ConvertTo-Json)
$env:COFFEESHOP_MOBILE_TEST_COURIER_NAME = $courierSession.user.fullName
& "$PSScriptRoot/courier-fixture.ps1" -Action Seed -FixturePath $courierPath
$courier = Get-Content -LiteralPath $courierPath -Raw | ConvertFrom-Json
$env:MAESTRO_DELIVERED_ORDER = $courier.deliveredOrder
$env:MAESTRO_FAILED_ORDER = $courier.failedOrder
$env:MAESTRO_RETIRED_RECORD_ID = '00000000-0000-0000-0000-000000000186'
$results = @()
$serverVerified = $false
try {
    foreach ($flow in $flows) {
        $name, $role = $flow
        $env:MAESTRO_STAFF_USERNAME = [Environment]::GetEnvironmentVariable("COFFEESHOP_E2E_${role}_USERNAME")
        $env:MAESTRO_STAFF_PASSWORD = $env:COFFEESHOP_E2E_PASSWORD
        if ($name -eq 'session') {
            $session = Invoke-RestMethod -Method Post -Uri "$env:COFFEESHOP_MOBILE_TEST_API_BASE_URL/auth/login" -ContentType 'application/json' -Body (@{
                username = $env:MAESTRO_STAFF_USERNAME; password = $env:MAESTRO_STAFF_PASSWORD
            } | ConvertTo-Json)
            $env:MAESTRO_STAFF_NAME = $session.user.fullName
        }
        $report = Join-Path $ResultsDirectory "$name.xml"
        # Remove only the old report for this flow: a stale success must never count.
        if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
        $nativeOutput = Join-Path $ResultsDirectory "private/$name"
        New-Item -ItemType Directory -Path $nativeOutput -Force | Out-Null
        $savedPreference = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'Continue'
            & maestro test --format junit --output $report --test-output-dir $nativeOutput ".maestro/$name.yaml"
            $exitCode = $LASTEXITCODE
        } finally { $ErrorActionPreference = $savedPreference }
        $cases = @()
        if (Test-Path -LiteralPath $report) {
            [xml]$xml = Get-Content -LiteralPath $report -Raw
            $cases = @($xml.SelectNodes('//testcase'))
        }
        $passed = @($cases | Where-Object { !$_.SelectSingleNode('failure') -and !$_.SelectSingleNode('error') -and !$_.SelectSingleNode('skipped') }).Count
        $ok = $exitCode -eq 0 -and $cases.Count -eq 1 -and $passed -eq 1
        $results += @{ workflow = $name; passed = $ok; testCases = $cases.Count; passedCases = $passed; exitCode = $exitCode }
        # Restore connectivity even when the offline flow fails before its final step.
        if ($name -eq 'offline') { & adb shell cmd connectivity airplane-mode disable | Out-Null }
    }
    $env:COFFEESHOP_MOBILE_TEST_MANAGER_USERNAME = $env:COFFEESHOP_E2E_MANAGER_USERNAME
    & "$PSScriptRoot/courier-fixture.ps1" -Action Verify -FixturePath $courierPath
    $serverVerified = $true
} finally {
    & adb shell cmd connectivity airplane-mode disable | Out-Null
    @{ expected = $flows.Count; passed = @($results | Where-Object passed).Count; failedOrMissing = $flows.Count - @($results | Where-Object passed).Count; serverVerificationPassed = $serverVerified; workflows = $results } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $ResultsDirectory 'summary.json')
    Remove-Item Env:MAESTRO_STAFF_PASSWORD, Env:COFFEESHOP_MOBILE_TEST_MANAGER_PASSWORD -ErrorAction SilentlyContinue
}
if ($results.Count -ne $flows.Count -or @($results | Where-Object { !$_.passed }).Count) { throw 'Android suite failed. Read summary.json; missing, failed or skipped cases are not passing.' }
Write-Output "PASS $($flows.Count)/$($flows.Count) Android flows and separate-session API state verification."
