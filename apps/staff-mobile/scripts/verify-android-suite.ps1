param([string]$ResultsDirectory = '.maestro/results/quality')
$ErrorActionPreference = 'Stop'
# Run from apps/staff-mobile against freshly provisioned, disposable accounts.
$roles = @('Manager', 'Barista', 'Cashier')
foreach ($role in $roles) {
    if (![Environment]::GetEnvironmentVariable("COFFEESHOP_E2E_${role}_USERNAME")) { throw "Set COFFEESHOP_E2E_${role}_USERNAME." }
}
if (!$env:COFFEESHOP_E2E_PASSWORD) { throw 'Set the synthetic accounts password in COFFEESHOP_E2E_PASSWORD.' }
foreach ($command in @('maestro', 'adb')) { $null = Get-Command $command -ErrorAction Stop }
New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null
$flows = @(
    @('session', 'Barista'), @('barista', 'Barista'),
    @('cashier', 'Cashier'), @('manager-orders', 'Manager'),
    @('notification-links', 'Barista')
)
# Reset the summary before fixture setup, so a failed seed cannot leave a green report.
@{ expected = $flows.Count; passed = 0; failedOrMissing = $flows.Count; serverVerificationPassed = $false; workflows = @() } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ResultsDirectory 'summary.json')
$env:COFFEESHOP_MOBILE_TEST_MANAGER_USERNAME = $env:COFFEESHOP_E2E_MANAGER_USERNAME
$env:COFFEESHOP_MOBILE_TEST_MANAGER_PASSWORD = $env:COFFEESHOP_E2E_PASSWORD
$baristaPath = Join-Path $ResultsDirectory 'barista-fixture.json'
$ordersPath = Join-Path $ResultsDirectory 'orders-fixture.json'
& "$PSScriptRoot/barista-fixture.ps1" -Action Seed -FixturePath $baristaPath
& "$PSScriptRoot/cashier-manager-fixture.ps1" -Action Seed -FixturePath $ordersPath
# Other seeders insert confirmed orders into the sorted preparation queue.
# Locate only after all fixtures exist so a page boundary cannot stale the index.
$env:COFFEESHOP_MOBILE_TEST_MANAGER_USERNAME = $env:COFFEESHOP_E2E_MANAGER_USERNAME
& "$PSScriptRoot/barista-fixture.ps1" -Action Locate -FixturePath $baristaPath
$barista = Get-Content -LiteralPath $baristaPath -Raw | ConvertFrom-Json
$orders = Get-Content -LiteralPath $ordersPath -Raw | ConvertFrom-Json
$env:MAESTRO_ORDER_NUMBER = $barista.orderNumber
$env:MAESTRO_QUEUE_PAGE_SKIP = "$($barista.queuePageSkip)"
$env:MAESTRO_BARISTA_ADDON = $barista.addonName
$env:MAESTRO_PAYMENT_ORDER = $orders.paymentOrder
$env:MAESTRO_CANCEL_ORDER = $orders.cancelOrder
$env:MAESTRO_DELIVERY_ORDER = $orders.deliveryOrder
$env:MAESTRO_COURIER_NAME = $orders.courierName
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
    & "$PSScriptRoot/barista-fixture.ps1" -Action Verify -FixturePath $baristaPath
    & "$PSScriptRoot/cashier-manager-fixture.ps1" -Action Verify -FixturePath $ordersPath
    $serverVerified = $true
} finally {
    & adb shell cmd connectivity airplane-mode disable | Out-Null
    @{ expected = $flows.Count; passed = @($results | Where-Object passed).Count; failedOrMissing = $flows.Count - @($results | Where-Object passed).Count; serverVerificationPassed = $serverVerified; workflows = $results } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $ResultsDirectory 'summary.json')
    Remove-Item Env:MAESTRO_STAFF_PASSWORD, Env:COFFEESHOP_MOBILE_TEST_MANAGER_PASSWORD -ErrorAction SilentlyContinue
}
if ($results.Count -ne $flows.Count -or @($results | Where-Object { !$_.passed }).Count) { throw 'Android suite failed. Read summary.json; missing, failed or skipped cases are not passing.' }
Write-Output "PASS $($flows.Count)/$($flows.Count) Android flows and separate-session API state verification."
