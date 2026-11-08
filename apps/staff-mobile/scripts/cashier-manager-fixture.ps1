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
function Read([string]$Path) {
    $response = Invoke-RestMethod -Uri "$base/$Path" -Headers $headers
    $response
}
if ($Action -eq 'Seed') {
    $table = @(Read 'pos/tables') | Where-Object { $_.id -eq $env:COFFEESHOP_MOBILE_TEST_TABLE_ID -and !$_.isOccupied }
    if (!$table) { throw 'Supply the ID of an available synthetic table in the disposable database.' }
    $suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
    $category = Send 'categories' @{ name = "Counter E2E $suffix"; displayOrder = 0 }
    $item = Send 'menu-items' @{ categoryId = $category.id; name = "Counter latte $suffix"; initialVariants = @(@{ sizeName = 'Large'; price = 10 }) }
    function New-Order([string]$Channel) {
        $id = [guid]::NewGuid().ToString()
        $body = @{ orderId = $id; channel = $Channel; lines = @(@{ menuItemId = $item.id; sizeVariantId = $item.sizeVariants[0].id; quantity = 1; addonIds = @() }) }
        if ($Channel -eq 'dine_in') { $body.tableId = $table.id }
        if ($Channel -eq 'delivery') { $body.customer = @{ recipientName = 'Synthetic customer'; contactNumber = '1234567890'; addressLine = 'Synthetic test address' } }
        $null = Send 'orders' $body
        Send "orders/$id/confirm" @{ reviewedTotal = 10 }
    }
    $payment = New-Order 'takeaway'
    $cancel = New-Order 'dine_in'
    $delivery = New-Order 'delivery'
    $null = Send "orders/$($delivery.id)/preparing" @{}
    $null = Send "orders/$($delivery.id)/ready" @{}
    $courier = @(Read 'delivery-staff/options') | Where-Object { $_.fullName -eq $env:COFFEESHOP_MOBILE_TEST_COURIER_NAME }
    if (@($courier).Count -ne 1) { throw 'Exactly one synthetic eligible courier must match the supplied name.' }
    @{
        paymentId = $payment.id; paymentOrder = $payment.orderNumber
        cancelId = $cancel.id; cancelOrder = $cancel.orderNumber
        deliveryId = $delivery.id; deliveryOrder = $delivery.orderNumber
        courierId = $courier.id; courierName = $courier.fullName; tableId = $table.id; tableNumber = $table.tableNumber
    } | ConvertTo-Json | Set-Content -LiteralPath $FixturePath
    Write-Output 'Seeded confirmed payment/cancellation orders and one ready unassigned delivery.'
} else {
    $fixture = Get-Content -LiteralPath $FixturePath -Raw | ConvertFrom-Json
    $orders = @(Read 'orders')
    $payment = $orders | Where-Object id -eq $fixture.paymentId
    $cancel = $orders | Where-Object id -eq $fixture.cancelId
    $delivery = $orders | Where-Object id -eq $fixture.deliveryId
    $table = @(Read 'pos/tables') | Where-Object id -eq $fixture.tableId
    if ($payment.paidAmount -ne 10 -or $payment.total -ne 10) { throw 'Payment/balance verification failed.' }
    if ($cancel.status -ne 'cancelled' -or $table.isOccupied -or $table.activeOrderId) { throw 'Cancellation/table release verification failed.' }
    if ($delivery.deliveryStatus -ne 'assigned' -or $delivery.assignedStaffId -ne $fixture.courierId) { throw 'Delivery assignment verification failed.' }
    Write-Output 'PASS 3/3: separate manager API session observes exact paid balance, cancelled order/table release, and assigned courier.'
}
