param([string]$ResultsDirectory = '.maestro/results/quality')
$ErrorActionPreference = 'Stop'
# Run from apps/staff-mobile against freshly provisioned, disposable accounts.
$roles = @('Manager', 'Admin', 'Barista', 'InventoryStaff', 'Cashier', 'DeliveryStaff')
foreach ($role in $roles) {
    if (![Environment]::GetEnvironmentVariable("COFFEESHOP_E2E_${role}_USERNAME")) { throw "Set COFFEESHOP_E2E_${role}_USERNAME." }
}
if (!$env:COFFEESHOP_E2E_PASSWORD) { throw 'Set the synthetic accounts password in COFFEESHOP_E2E_PASSWORD.' }
foreach ($command in @('maestro', 'adb')) { $null = Get-Command $command -ErrorAction Stop }
New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null
$flows = @(
    @('session', 'Barista'), @('barista', 'Barista'), @('approvals', 'Manager'),
    @('inventory', 'InventoryStaff'), @('courier', 'DeliveryStaff'),
    @('cashier', 'Cashier'), @('manager-orders', 'Manager'),
    @('administration', 'Admin'), @('offline', 'InventoryStaff'),
    @('notification-links', 'Barista')
)
# Reset the summary before fixture setup, so a failed seed cannot leave a green report.
@{ expected = $flows.Count; passed = 0; failedOrMissing = $flows.Count; serverVerificationPassed = $false; workflows = @() } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ResultsDirectory 'summary.json')
$env:COFFEESHOP_MOBILE_TEST_MANAGER_USERNAME = $env:COFFEESHOP_E2E_MANAGER_USERNAME
$env:COFFEESHOP_MOBILE_TEST_MANAGER_PASSWORD = $env:COFFEESHOP_E2E_PASSWORD
$baristaPath = Join-Path $ResultsDirectory 'barista-fixture.json'
$ordersPath = Join-Path $ResultsDirectory 'orders-fixture.json'
$courierPath = Join-Path $ResultsDirectory 'courier-fixture.json'
$qualityPath = Join-Path $ResultsDirectory 'quality-fixture.json'
& "$PSScriptRoot/barista-fixture.ps1" -Action Seed -FixturePath $baristaPath
& "$PSScriptRoot/cashier-manager-fixture.ps1" -Action Seed -FixturePath $ordersPath
& "$PSScriptRoot/courier-fixture.ps1" -Action Seed -FixturePath $courierPath
$env:COFFEESHOP_MOBILE_TEST_MANAGER_USERNAME = $env:COFFEESHOP_E2E_ADMIN_USERNAME
& "$PSScriptRoot/quality-fixture.ps1" -Action Seed -FixturePath $qualityPath
$barista = Get-Content -LiteralPath $baristaPath -Raw | ConvertFrom-Json
$orders = Get-Content -LiteralPath $ordersPath -Raw | ConvertFrom-Json
$courier = Get-Content -LiteralPath $courierPath -Raw | ConvertFrom-Json
$quality = Get-Content -LiteralPath $qualityPath -Raw | ConvertFrom-Json
$env:MAESTRO_ORDER_NUMBER = $barista.orderNumber
$env:MAESTRO_QUEUE_PAGE_SKIP = "$($barista.queuePageSkip)"
$env:MAESTRO_PAYMENT_ORDER = $orders.paymentOrder
$env:MAESTRO_CANCEL_ORDER = $orders.cancelOrder
$env:MAESTRO_DELIVERY_ORDER = $orders.deliveryOrder
$env:MAESTRO_COURIER_NAME = $orders.courierName
$env:MAESTRO_DELIVERED_ORDER = $courier.deliveredOrder
$env:MAESTRO_FAILED_ORDER = $courier.failedOrder
$env:MAESTRO_APPROVE_ORDER = $quality.decisions[0].orderNumber
$env:MAESTRO_REJECT_ORDER = $quality.decisions[1].orderNumber
$env:MAESTRO_DISCOUNT_ID = $quality.decisions[0].discountId
$env:MAESTRO_INGREDIENT = $quality.ingredientName
$env:MAESTRO_OFFLINE_INGREDIENT = $quality.offlineName
$env:MAESTRO_RECEIPT = $quality.receiptNumber
$env:MAESTRO_TARGET_NAME = $quality.targetName
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
        & maestro test --format junit --output $report ".maestro/$name.yaml"
        $exitCode = $LASTEXITCODE
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
    & "$PSScriptRoot/courier-fixture.ps1" -Action Verify -FixturePath $courierPath
    $env:COFFEESHOP_MOBILE_TEST_MANAGER_USERNAME = $env:COFFEESHOP_E2E_ADMIN_USERNAME
    & "$PSScriptRoot/quality-fixture.ps1" -Action Verify -FixturePath $qualityPath
    $serverVerified = $true
} finally {
    & adb shell cmd connectivity airplane-mode disable | Out-Null
    @{ expected = $flows.Count; passed = @($results | Where-Object passed).Count; failedOrMissing = $flows.Count - @($results | Where-Object passed).Count; serverVerificationPassed = $serverVerified; workflows = $results } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $ResultsDirectory 'summary.json')
    Remove-Item Env:MAESTRO_STAFF_PASSWORD, Env:COFFEESHOP_MOBILE_TEST_MANAGER_PASSWORD -ErrorAction SilentlyContinue
}
if ($results.Count -ne $flows.Count -or @($results | Where-Object { !$_.passed }).Count) { throw 'Android suite failed. Read summary.json; missing, failed or skipped cases are not passing.' }
Write-Output "PASS $($flows.Count)/$($flows.Count) Android flows and separate-session API state verification."
