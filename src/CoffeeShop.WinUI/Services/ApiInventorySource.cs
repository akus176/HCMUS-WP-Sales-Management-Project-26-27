using CoffeeShop.Application.Inventory;
using CoffeeShop.Contracts;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

namespace CoffeeShop.WinUI.Services;

/// <summary>Inventory and procurement operations for the authenticated WinUI API mode.</summary>
public sealed class ApiInventorySource(AuthenticatedApiClient client)
{
    public async Task<IReadOnlyList<Ingredient>> ListIngredientsAsync(bool includeInactive = true)
    {
        var active = includeInactive ? string.Empty : "&isActive=true";
        var page = await client.GetAsync<PageResponse<IngredientBalanceResponse>>($"ingredients?page=1&pageSize=200{active}");
        return page.Items.Select(ToIngredient).ToArray();
    }

    public async Task<IReadOnlyList<Supplier>> ListSuppliersAsync() =>
        (await client.GetAsync<SupplierResponse[]>("suppliers")).Select(ToSupplier).ToArray();

    public async Task<IReadOnlyList<PurchaseReceipt>> ListReceiptsAsync() =>
        (await client.GetAsync<ReceiptResponse[]>("purchase-receipts")).Select(ToReceipt).ToArray();

    public async Task<IReadOnlyList<StockMovement>> ListMovementsAsync() =>
        (await client.GetAsync<StockMovementResponse[]>("stock-movements")).Select(ToMovement).ToArray();

    public async Task<IReadOnlyList<StockMovement>> ListMovementsAsync(EntityId ingredientId) =>
        (await client.GetAsync<StockMovementResponse[]>($"ingredients/{ingredientId.Value}/movements")).Select(ToMovement).ToArray();

    public async Task<Supplier?> FindSupplierAsync(EntityId id)
    {
        var suppliers = await ListSuppliersAsync();
        return suppliers.SingleOrDefault(supplier => supplier.Id == id);
    }

    public async Task<Ingredient?> FindIngredientAsync(EntityId id) =>
        ToIngredient(await client.GetAsync<IngredientBalanceResponse>($"ingredients/{id.Value}"));

    public async Task<Ingredient> CreateIngredientAsync(string name, string unit, decimal balance, decimal threshold) =>
        ToIngredient(await client.PostAsync<CreateIngredientRequest, IngredientBalanceResponse>("ingredients",
            new(name, unit, balance, threshold)));

    public Task<StockAdjustmentResponse> AdjustAsync(EntityId ingredientId, decimal delta, string reason) =>
        client.PostAsync<StockAdjustmentRequest, StockAdjustmentResponse>($"ingredients/{ingredientId.Value}/adjustments",
            new(Guid.NewGuid(), delta, reason));

    public async Task<Supplier> CreateSupplierAsync(string name, string phone, string? email, string? address) =>
        ToSupplier(await client.PostAsync<SupplierRequest, SupplierResponse>("suppliers", new(name, phone, email, address)));

    public async Task UpdateSupplierAsync(EntityId id, string name, string phone, string? email, string? address) =>
        _ = await client.PutAsync<SupplierRequest, object>($"suppliers/{id.Value}", new(name, phone, email, address));

    public async Task<PurchaseReceipt> CreateReceiptAsync(EntityId supplierId, string number) =>
        ToReceipt(await client.PostAsync<CreateReceiptRequest, ReceiptResponse>("purchase-receipts", new(supplierId.Value, number)));

    public async Task<PurchaseReceipt> ReceiveDeliveryAsync(EntityId supplierId, string number, IReadOnlyList<ReceiveDeliveryLine> lines) =>
        ToReceipt(await client.PostAsync<ReceiveDeliveryRequest, ReceiptResponse>("purchase-receipts/receive",
            new(supplierId.Value, number, lines.Select(line => new ReceiptLineRequest(line.IngredientId.Value, line.Quantity, line.UnitCost)).ToArray())));

    public async Task<PurchaseReceipt> AddReceiptLineAsync(EntityId receiptId, EntityId ingredientId, decimal quantity, decimal unitCost) =>
        ToReceipt(await client.PostAsync<ReceiptLineRequest, ReceiptResponse>($"purchase-receipts/{receiptId.Value}/lines",
            new(ingredientId.Value, quantity, unitCost)));

    public Task PostReceiptAsync(EntityId receiptId) => client.PostAsync<object, object>($"purchase-receipts/{receiptId.Value}/post", new { });

    public Task SetThresholdAsync(EntityId ingredientId, decimal threshold) =>
        client.PostAsync<SetReorderThresholdRequest, object>($"ingredients/{ingredientId.Value}/threshold", new(threshold));

    public Task DeactivateIngredientAsync(EntityId ingredientId) => client.PostAsync<object, object>($"ingredients/{ingredientId.Value}/deactivate", new { });
    public Task ActivateIngredientAsync(EntityId ingredientId) => client.PostAsync<object, object>($"ingredients/{ingredientId.Value}/activate", new { });
    public Task DeactivateSupplierAsync(EntityId supplierId) => client.PostAsync<object, object>($"suppliers/{supplierId.Value}/deactivate", new { });
    public Task ActivateSupplierAsync(EntityId supplierId) => client.PostAsync<object, object>($"suppliers/{supplierId.Value}/activate", new { });

    private static Ingredient ToIngredient(IngredientBalanceResponse row)
    {
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        return new(new EntityId(row.IngredientId), row.Name, row.BaseUnit, now, now,
            row.AvailableQuantity, row.ReorderThreshold, row.IsActive);
    }

    private static Supplier ToSupplier(SupplierResponse row)
    {
        var created = new UtcTimestamp(row.CreatedAt ?? DateTimeOffset.UtcNow);
        var updated = new UtcTimestamp(row.UpdatedAt ?? row.CreatedAt ?? DateTimeOffset.UtcNow);
        return new(new EntityId(row.Id), row.Name, row.Phone, created, updated, row.Email, row.Address, row.IsActive);
    }

    private static PurchaseReceipt ToReceipt(ReceiptResponse row)
    {
        var created = new UtcTimestamp(row.CreatedAt ?? row.PostedAt ?? DateTimeOffset.UtcNow);
        var updated = new UtcTimestamp(row.UpdatedAt ?? row.PostedAt ?? row.CreatedAt ?? DateTimeOffset.UtcNow);
        var status = Enum.Parse<PurchaseReceiptStatus>(row.Status, ignoreCase: true);
        var lines = row.Lines.Select(line => new PurchaseReceiptLine(new EntityId(line.Id),
            new EntityId(line.IngredientId), line.Quantity, new Money(line.UnitCost))).ToArray();
        return PurchaseReceipt.Reconstitute(new(row.Id), new(row.SupplierId), row.ReceiptNumber,
            created, updated, row.PostedAt is { } posted ? new UtcTimestamp(posted) : null, status, lines);
    }

    private static StockMovement ToMovement(StockMovementResponse row)
    {
        if (!Enum.TryParse<StockMovementType>(row.Type, ignoreCase: true, out var type))
            throw new InvalidOperationException($"Unsupported stock movement type '{row.Type}'.");
        return new(new(row.Id), new(row.IngredientId), type, row.QuantityDelta, new(row.ActorId), row.Reason,
            new(row.OccurredAt), row.PurchaseReceiptId is { } receipt ? new(receipt) : null,
            row.OrderId is { } order ? new(order) : null);
    }
}
