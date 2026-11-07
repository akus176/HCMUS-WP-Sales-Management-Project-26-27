param(
    [Parameter(Mandatory)][ValidateSet('Seed', 'Verify')][string]$Action,
    [Parameter(Mandatory)][string]$FixturePath
)
$ErrorActionPreference = 'Stop'
$base = $env:COFFEESHOP_MOBILE_TEST_API_BASE_URL
if (!$base -or ([uri]$base).Host -ne '127.0.0.1' -or ([uri]$base).AbsolutePath -ne '/api/v1') {
    throw 'Use a disposable loopback API ending in /api/v1.'
}
if (!$env:COFFEESHOP_MOBILE_TEST_MANAGER_USERNAME -or !$env:COFFEESHOP_MOBILE_TEST_MANAGER_PASSWORD) {
    throw 'Set the disposable manager username and password environment variables.'
}
$login = Invoke-RestMethod -Method Post -Uri "$base/auth/login" -ContentType 'application/json' -Body (@{
    username = $env:COFFEESHOP_MOBILE_TEST_MANAGER_USERNAME
    password = $env:COFFEESHOP_MOBILE_TEST_MANAGER_PASSWORD
} | ConvertTo-Json)
$headers = @{ Authorization = "Bearer $($login.accessToken)" }
function Send([string]$Path, $Body) {
    Invoke-RestMethod -Method Post -Uri "$base/$Path" -Headers $headers -ContentType 'application/json' -Body ($Body | ConvertTo-Json -Depth 8)
}
function Find-QueuePage([string]$OrderId) {
    $page = 1
    do {
        $queue = Invoke-RestMethod -Uri "$base/preparation-tickets?status=confirmed,preparing&page=$page&pageSize=10" -Headers $headers
        if ($queue.items.orderId -contains $OrderId) { return $page }
        $page++
    } while (($page - 1) * 10 -lt $queue.totalCount)
    return 0
}
if ($Action -eq 'Seed') {
    $suffix = [guid]::NewGuid().ToString('N').Substring(0, 8)
    $category = Send 'categories' @{ name = "Barista E2E $suffix"; displayOrder = 0 }
    $item = Send 'menu-items' @{ categoryId = $category.id; name = "Test latte $suffix"; initialVariants = @(@{ sizeName = 'Large'; price = 5 }) }
    $id = [guid]::NewGuid().ToString()
    $null = Send 'orders' @{
        orderId = $id; channel = 'takeaway'
        lines = @(@{ menuItemId = $item.id; sizeVariantId = $item.sizeVariants[0].id; quantity = 2; addonIds = @() })
    }
    $order = Send "orders/$id/confirm" @{ reviewedTotal = 10 }
    $queuePage = Find-QueuePage $id
    if (!$queuePage) { throw 'Confirmed fixture was not found in the active queue.' }
    @{ orderId = $id; orderNumber = $order.orderNumber; itemName = $item.name; queuePageSkip = $queuePage - 1 } | ConvertTo-Json | Set-Content -LiteralPath $FixturePath
    Write-Output "Confirmed fixture saved to $FixturePath. Set MAESTRO_ORDER_NUMBER to $($order.orderNumber) and MAESTRO_QUEUE_PAGE_SKIP to $($queuePage - 1)."
} else {
    $fixture = Get-Content -LiteralPath $FixturePath -Raw | ConvertFrom-Json
    $order = Invoke-RestMethod -Uri "$base/orders/$($fixture.orderId)" -Headers $headers
    if ($order.status -ne 'ready') { throw "Expected ready; service returned $($order.status)." }
    if (Find-QueuePage $fixture.orderId) { throw 'Ready ticket remained in the active queue.' }
    Write-Output 'PASS: separate manager session observes Ready and the ticket is absent from the active preparation queue.'
}
