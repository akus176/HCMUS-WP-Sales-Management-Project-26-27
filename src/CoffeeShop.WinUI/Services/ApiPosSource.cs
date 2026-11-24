using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Customers;
using CoffeeShop.Application.Sales;
using CoffeeShop.Contracts;
using CoffeeShop.Domain.Common;

namespace CoffeeShop.WinUI.Services;

/// <summary>POS operations go through the authenticated shared API.</summary>
public sealed class ApiPosSource(AuthenticatedApiClient client, StaffSessionManager session)
{
    public bool CanManageTables => session.CurrentUser?.Permissions.Contains("tables.manage") == true;

    public Task<PosTableResponse> CreateTableAsync(string number, int seats) =>
        client.PostAsync<CreateTableRequest, PosTableResponse>("tables", new(number, seats));

    public async Task<IReadOnlyList<MenuItemSummary>> MenuAsync(string search)
    {
        var items = await client.GetAsync<MenuItemResponse[]>($"menu-items?isActive=true&limit=200&search={Uri.EscapeDataString(search)}");
        return items.Select(item => new MenuItemSummary(new(item.Id), new(item.CategoryId), item.CategoryName,
            item.Name, item.Description, item.IsActive,
            item.SizeVariants.Select(size => new SizeVariantSummary(new(size.Id), size.SizeName, size.Price, size.IsActive)).ToArray(),
            item.CompatibleAddons.Select(addon => new AddonSummary(new(addon.Id), addon.Name, addon.Price, addon.IsActive)).ToArray())).ToArray();
    }

    public async Task<IReadOnlyList<CategorySummary>> CategoriesAsync()
    {
        var categories = await client.GetAsync<CategoryResponse[]>("categories?isActive=true&limit=200");
        return categories.Select(category => new CategorySummary(new(category.Id), category.Name,
            category.DisplayOrder, category.IsActive)).ToArray();
    }

    public async Task<PosOptions> OptionsAsync()
    {
        var user = session.CurrentUser ?? throw new ApiException(ApiErrorKind.Unauthorized, System.Net.HttpStatusCode.Unauthorized,
            "Sign in required", "Your session has expired. Sign in again.");
        var tables = await client.GetAsync<PosTableResponse[]>("pos/tables");
        return new([new(new(user.Id), user.FullName)], tables.Where(table => !table.IsOccupied)
            .Select(table => new PosTableOption(new(table.Id), table.TableNumber, table.Seats)).ToArray());
    }

    public async Task<PosOrderDetail> GetAsync(EntityId id) => ToOrder(await client.GetAsync<OrderResponse>($"orders/{id.Value}"));

    public async Task<PosOrderDetail> CreateAsync(EntityId id, string channel, EntityId? table,
        CustomerOrderSelectionRequest customer, IReadOnlyList<PosLineRequest> lines)
    {
        var request = new CreateOrderRequest(id.Value, channel, table?.Value,
            new(customer.CustomerId?.Value, customer.CustomerAddressId?.Value, customer.RecipientName,
                customer.ContactNumber, customer.AddressLine),
            lines.Select(line => new OrderLineRequest(line.MenuItemId.Value, line.SizeVariantId.Value,
                line.Quantity, line.AddonIds.Select(addon => addon.Value).ToArray())).ToArray());
        return ToOrder(await client.PostAsync<CreateOrderRequest, OrderResponse>("orders", request));
    }

    public async Task<PosOrderDetail> ConfirmAsync(EntityId id, decimal reviewedTotal) => ToOrder(
        await client.PostAsync<ConfirmOrderRequest, OrderResponse>($"orders/{id.Value}/confirm", new(reviewedTotal)));

    public async Task RequestDiscountAsync(EntityId orderId, EntityId discountId, string type, decimal value, string reason) =>
        _ = await client.PostAsync<DiscountRequest, DiscountDecisionResponse>($"orders/{orderId.Value}/discounts",
            new(discountId.Value, type, value, reason));

    public async Task RedeemAsync(EntityId orderId, int points) =>
        _ = await client.PostAsync<RedeemLoyaltyRequest, LoyaltyOperationResponse>($"orders/{orderId.Value}/loyalty/redeem", new(points));

    public static PosOrderDetail ToOrder(OrderResponse order) => new(new(order.Id), order.OrderNumber, order.Channel,
        order.Status, ToId(order.CustomerId), order.CustomerName, ToId(order.TableId), order.TableNumber,
        order.RecipientName, order.ContactNumber, order.AddressLine, ToId(order.CustomerAddressId),
        order.Subtotal, order.DiscountTotal, order.Total,
        order.Lines.Select(line => new PosLineSnapshot(line.ItemName, line.SizeName, line.Quantity, line.UnitPrice, line.Total,
            (line.AddonDetails ?? throw new InvalidOperationException("The API did not return add-on snapshot prices."))
                .Select(addon => new PosAddonSnapshot(addon.Name, addon.UnitPrice)).ToArray())).ToArray(),
        order.Discounts.Select(discount => new PosDiscountSummary(new(discount.Id), discount.Type, discount.Value,
            discount.Status, discount.Amount, discount.Reason ?? string.Empty)).ToArray());

    private static EntityId? ToId(Guid? id) => id is { } value ? new EntityId(value) : null;
}
