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
    $suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
    $category = Send 'categories' @{ name = "Quality $suffix"; displayOrder = 0 }
    $item = Send 'menu-items' @{ categoryId = $category.id; name = "Quality latte $suffix"; initialVariants = @(@{ sizeName = 'Large'; price = 10 }) }
    $decisions = foreach ($decision in @('approve', 'reject')) {
        $id = [guid]::NewGuid().ToString()
        $order = Send 'orders' @{ orderId = $id; channel = 'takeaway'; lines = @(@{ menuItemId = $item.id; sizeVariantId = $item.sizeVariants[0].id; quantity = 1; addonIds = @() }) }
        $discountId = [guid]::NewGuid().ToString()
        $null = Send "orders/$id/discounts" @{ discountId = $discountId; type = 'fixed'; value = 1; reason = "Quality $decision" }
        @{ orderId = $id; orderNumber = $order.orderNumber; discountId = $discountId; expected = $decision }
    }
    $ingredient = Send 'ingredients' @{ name = "Quality milk $suffix"; baseUnit = 'l'; availableQuantity = 1; reorderThreshold = 3 }
    $offline = Send 'ingredients' @{ name = "Offline milk $suffix"; baseUnit = 'l'; availableQuantity = 1; reorderThreshold = 3 }
    $supplier = Send 'suppliers' @{ name = "Quality supplier $suffix"; phone = '1234567890'; email = 'quality@example.test'; address = 'Synthetic supplier address' }
    $receipt = Send 'purchase-receipts' @{ supplierId = $supplier.id; receiptNumber = "QUALITY-$suffix" }
    $null = Send "purchase-receipts/$($receipt.id)/lines" @{ ingredientId = $ingredient.ingredientId; quantity = 4; unitCost = 2 }
    $accounts = @(Read "users?search=$($env:COFFEESHOP_MOBILE_TEST_TARGET_USERNAME)&isActive=true")
    $target = @($accounts | Where-Object username -eq $env:COFFEESHOP_MOBILE_TEST_TARGET_USERNAME)
    if ($target.Count -ne 1) { throw 'Provision exactly one eligible synthetic target account before seeding.' }
    @{
        decisions = @($decisions); ingredientId = $ingredient.ingredientId; ingredientName = $ingredient.name
        offlineId = $offline.ingredientId; offlineName = $offline.name
        receiptId = $receipt.id; receiptNumber = $receipt.receiptNumber
        targetId = $target[0].id; targetName = $target[0].fullName
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $FixturePath
    Write-Output 'Seeded two pending discounts, adjustment/offline ingredients, a prepared receipt, and an eligible account target.'
} else {
    $fixture = Get-Content -LiteralPath $FixturePath -Raw | ConvertFrom-Json
    foreach ($decision in $fixture.decisions) {
        $order = Read "orders/$($decision.orderId)"
        $discount = @($order.discounts | Where-Object id -eq $decision.discountId)
        $expectedStatus = if ($decision.expected -eq 'approve') { 'approved' } else { 'rejected' }
        $expectedTotal = if ($decision.expected -eq 'approve') { 9 } else { 10 }
        if ($discount.Count -ne 1 -or $discount[0].status -ne $expectedStatus -or $order.total -ne $expectedTotal) { throw 'Discount decision or authoritative total is incorrect.' }
    }
    $balances = Read "ingredients?search=$([uri]::EscapeDataString($fixture.ingredientName))"
    $balance = @($balances.items | Where-Object ingredientId -eq $fixture.ingredientId)
    $movements = @(Read "ingredients/$($fixture.ingredientId)/movements")
    $receipt = Read "purchase-receipts/$($fixture.receiptId)"
    if ($balance.Count -ne 1 -or $balance[0].availableQuantity -ne 7 -or $receipt.status -ne 'posted') { throw 'Adjustment/receipt shared balance verification failed.' }
    if (@($movements | Where-Object type -eq 'adjustment').Count -ne 1 -or @($movements | Where-Object purchaseReceiptId -eq $fixture.receiptId).Count -ne 1) { throw 'Expected one adjustment and one receipt movement.' }
    $offline = Read "ingredients?search=$([uri]::EscapeDataString($fixture.offlineName))"
    if (@($offline.items).Count -ne 1 -or $offline.items[0].availableQuantity -ne 1 -or @(Read "ingredients/$($fixture.offlineId)/movements").Count -ne 0) { throw 'Offline interaction changed authoritative stock.' }
    $target = Read "users/$($fixture.targetId)"
    if ($target.isActive) { throw 'Administration target remains active.' }
    Write-Output 'PASS 6/6: approved/rejected totals, stock balance, one-time movements, offline no-write state, and deactivated account verified through a separate API session.'
}
