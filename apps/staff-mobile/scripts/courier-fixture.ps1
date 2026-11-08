param(
    [Parameter(Mandatory)][ValidateSet('Seed', 'Verify')][string]$Action,
    [Parameter(Mandatory)][string]$FixturePath
)
$ErrorActionPreference = 'Stop'
$base = $env:COFFEESHOP_MOBILE_TEST_API_BASE_URL
if (!$base -or ([uri]$base).Host -ne '127.0.0.1' -or ([uri]$base).AbsolutePath -ne '/api/v1') {
    throw 'Use a disposable loopback API ending in /api/v1.'
}
$login = Invoke-RestMethod -Method Post -Uri "$base/auth/login" -ContentType 'application/json' -Body (@{
    username = $env:COFFEESHOP_MOBILE_TEST_MANAGER_USERNAME
    password = $env:COFFEESHOP_MOBILE_TEST_MANAGER_PASSWORD
} | ConvertTo-Json)
$headers = @{ Authorization = "Bearer $($login.accessToken)" }
function Send([string]$Path, $Body) {
    Invoke-RestMethod -Method Post -Uri "$base/$Path" -Headers $headers -ContentType 'application/json' -Body ($Body | ConvertTo-Json -Depth 8)
}
if ($Action -eq 'Seed') {
    $suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
    $category = Send 'categories' @{ name = "Courier E2E $suffix"; displayOrder = 0 }
    $item = Send 'menu-items' @{ categoryId = $category.id; name = "Courier test $suffix"; initialVariants = @(@{ sizeName = 'Regular'; price = 5 }) }
    $courier = @(Invoke-RestMethod -Uri "$base/delivery-staff/options" -Headers $headers) | Where-Object { $_.fullName -eq $env:COFFEESHOP_MOBILE_TEST_COURIER_NAME }
    if (@($courier).Count -ne 1) { throw 'Exactly one synthetic eligible courier must match the supplied name.' }
    function New-Assignment {
        $id = [guid]::NewGuid().ToString()
        $null = Send 'orders' @{
            orderId = $id; channel = 'delivery'
            customer = @{ recipientName = 'Synthetic recipient'; contactNumber = '1234567890'; addressLine = 'Synthetic test address' }
            lines = @(@{ menuItemId = $item.id; sizeVariantId = $item.sizeVariants[0].id; quantity = 1; addonIds = @() })
        }
        $order = Send "orders/$id/confirm" @{ reviewedTotal = 5 }
        $null = Send "orders/$id/preparing" @{}
        $null = Send "orders/$id/ready" @{}
        $null = Send "orders/$id/assign-delivery" @{ staffId = $courier.id }
        return $order
    }
    $delivered = New-Assignment
    $failed = New-Assignment
    @{ deliveredId = $delivered.id; deliveredOrder = $delivered.orderNumber; failedId = $failed.id; failedOrder = $failed.orderNumber; courierId = $courier.id } | ConvertTo-Json | Set-Content -LiteralPath $FixturePath
    Write-Output "Set MAESTRO_DELIVERED_ORDER=$($delivered.orderNumber) and MAESTRO_FAILED_ORDER=$($failed.orderNumber)."
} else {
    $fixture = Get-Content -LiteralPath $FixturePath -Raw | ConvertFrom-Json
    $orders = @(Invoke-RestMethod -Uri "$base/orders" -Headers $headers)
    foreach ($expected in @(@($fixture.deliveredId, 'delivered'), @($fixture.failedId, 'failed'))) {
        $order = $orders | Where-Object { $_.id -eq $expected[0] }
        if (!$order -or $order.deliveryStatus -ne $expected[1] -or $order.assignedStaffId -ne $fixture.courierId) { throw "Unexpected delivery state for $($expected[0])." }
    }
    Write-Output 'PASS: separate manager session observes delivered and failed states for the assigned courier.'
}
