param(
    [string]$ApiBase = 'http://127.0.0.1:52302/api/v1',
    [string]$OutputPath = 'fixture.json'
)

$ErrorActionPreference = 'Stop'
if (!$env:ISSUE200_MANAGER_PASSWORD) { throw 'Set ISSUE200_MANAGER_PASSWORD privately.' }
$login = Invoke-RestMethod "$ApiBase/auth/login" -Method Post -ContentType 'application/json' -Body (
    @{ username = 'issue200_manager'; password = $env:ISSUE200_MANAGER_PASSWORD } | ConvertTo-Json)
$headers = @{ Authorization = "Bearer $($login.accessToken)" }
function Send([string]$Path, $Body) {
    Invoke-RestMethod "$ApiBase/$Path" -Method Post -Headers $headers -ContentType 'application/json' -Body (
        $Body | ConvertTo-Json -Depth 8)
}

# Use a fresh disposable database. The +100 setup adjustment reproduces the
# opening movement seen in the captured history; the native test later adds -5.
$ingredient = Send 'ingredients' @{ name = 'I200 Verification Beans'; baseUnit = 'g'; availableQuantity = 0; reorderThreshold = 100 }
$null = Send "ingredients/$($ingredient.ingredientId)/adjustments" @{
    requestId = [guid]::NewGuid().ToString(); quantityDelta = 100; reason = 'I200 fixture opening balance'
}
$supplier = Send 'suppliers' @{
    name = 'I200 Verification Supplier'; phone = '0000000000'; email = 'supplier@example.test'; address = 'Synthetic test warehouse'
}
$receipt = Send 'purchase-receipts' @{ supplierId = $supplier.id; receiptNumber = 'I200-RECEIPT-001' }
$receipt = Send "purchase-receipts/$($receipt.id)/lines" @{ ingredientId = $ingredient.ingredientId; quantity = 10; unitCost = 2 }
$category = Send 'categories' @{ name = 'I200 Verification'; displayOrder = 99 }
$item = Send 'menu-items' @{
    categoryId = $category.id; name = 'I200 Verification Drink'; initialVariants = @(@{ sizeName = 'Regular'; price = 5 })
}
$staff = Invoke-RestMethod "$ApiBase/delivery-staff/options" -Headers $headers
$courier = $staff | Where-Object fullName -EQ 'Issue200 Courier'
if (@($courier).Count -ne 1) { throw 'Provision exactly one Issue200 Courier first.' }
$orders = foreach ($purpose in @('complete', 'fail', 'conflict')) {
    $id = [guid]::NewGuid().ToString()
    $null = Send 'orders' @{
        orderId = $id; channel = 'delivery'
        customer = @{ recipientName = "I200 $purpose"; contactNumber = '0000000000'; addressLine = 'Synthetic verification address' }
        lines = @(@{ menuItemId = $item.id; sizeVariantId = $item.sizeVariants[0].id; quantity = 1; addonIds = @() })
    }
    $order = Send "orders/$id/confirm" @{ reviewedTotal = 5 }
    $null = Send "orders/$id/preparing" @{}
    $null = Send "orders/$id/ready" @{}
    @{ id = $id; number = $order.orderNumber; purpose = $purpose }
}
@{
    ingredientId = $ingredient.ingredientId; ingredientName = 'I200 Verification Beans'; initialBalance = 100; reorderThreshold = 100
    receiptId = $receipt.id; receiptNumber = $receipt.receiptNumber; managerId = $login.user.id; courierId = $courier.id; orders = @($orders)
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputPath
