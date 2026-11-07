using CoffeeShop.Application.Sales;
using CoffeeShop.Contracts;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

namespace CoffeeShop.WinUI.Services;

/// <summary>Loads and changes the service board through permission-scoped API routes.</summary>
public sealed class ApiOperationsSource(AuthenticatedApiClient client, StaffSessionManager session)
{
    private IReadOnlyCollection<string> Permissions => session.CurrentUser?.Permissions ?? [];
    private bool Can(string permission) => Permissions.Contains(permission);

    public async Task<OperationsSnapshot> LoadAsync()
    {
        IReadOnlyList<OperationalOrderResponse> summaries;
        if (Can("orders.create"))
            summaries = await client.GetAsync<OperationalOrderResponse[]>("orders");
        else if (Can("orders.fulfill"))
            summaries = await client.GetAsync<OperationalOrderResponse[]>("operations/orders");
        else if (Can("delivery.update"))
        {
            var assignments = await client.GetAsync<PageResponse<CourierAssignmentResponse>>("courier/assignments?page=1&pageSize=200");
            summaries = assignments.Items.Select(item => new OperationalOrderResponse(item.OrderId,
                item.OrderNumber, "delivery", "ready", 0m, 0m, null, item.DeliveryStatus, null, [])).ToArray();
        }
        else summaries = [];

        var orders = summaries.Select(ToOperational).ToArray();
        IReadOnlyList<OperationalTable> tables = [];
        IReadOnlyList<DeliveryStaffOption> deliveryStaff = [];
        IReadOnlyList<PosStaffOption> staff = [];
        if (Can("orders.create"))
        {
            var tableRows = await client.GetAsync<PosTableResponse[]>("pos/tables");
            tables = tableRows.Select(table => new OperationalTable(new(table.Id), table.TableNumber,
                table.Seats, table.IsOccupied, ToId(table.ActiveOrderId))).ToArray();
        }
        if (Can("delivery.assign"))
        {
            var deliveryRows = await client.GetAsync<DeliveryStaffResponse[]>("delivery-staff/options");
            deliveryStaff = deliveryRows.Select(staffRow => new DeliveryStaffOption(new(staffRow.Id), staffRow.FullName)).ToArray();
        }
        if (session.CurrentUser is { } user) staff = [new(new(user.Id), user.FullName)];

        return new(orders, tables, deliveryStaff, staff);
    }

    public async Task<PosOrderDetail> GetDetailAsync(EntityId id)
    {
        if (Can("orders.create"))
            return ApiPosSource.ToOrder(await client.GetAsync<OrderResponse>($"orders/{id.Value}"));
        if (Can("orders.fulfill"))
        {
            var detail = await client.GetAsync<OperationsOrderDetailResponse>($"operations/orders/{id.Value}");
            return ToDetail(detail);
        }
        if (Can("delivery.update"))
        {
            var detail = await client.GetAsync<CourierAssignmentDetailResponse>($"courier/assignments/{id.Value}");
            return new(new(detail.OrderId), detail.OrderNumber, "delivery", "ready", null, null,
                null, null, detail.RecipientName, detail.ContactNumber, detail.AddressLine, null,
                0m, 0m, 0m, [], []);
        }
        throw new ApiException(ApiErrorKind.Forbidden, System.Net.HttpStatusCode.Forbidden,
            "No operational access", "Your account cannot view service board orders.");
    }

    public Task MoveAsync(EntityId id, string target) =>
        client.PostAsync<object, object>($"orders/{id.Value}/{target}", new { });

    public Task RecordPaymentAsync(EntityId orderId, string method, decimal amount, string? reference) =>
        client.PostAsync<RecordPaymentRequest, PaymentResponse>($"orders/{orderId.Value}/payments",
            new(Guid.NewGuid(), method, amount, Reference: reference));

    public Task CancelAsync(EntityId id, string reason) =>
        client.PostAsync<CancelOrderRequest, System.Text.Json.JsonElement>($"orders/{id.Value}/cancel", new(reason));

    public Task DecideDiscountAsync(EntityId id, bool approve) =>
        client.PostAsync<object, DiscountDecisionResponse>($"discounts/{id.Value}/{(approve ? "approve" : "reject")}", new { });

    public Task AssignDeliveryAsync(EntityId orderId, EntityId staffId) =>
        client.PostAsync<AssignDeliveryRequest, object>($"orders/{orderId.Value}/assign-delivery", new(staffId.Value));

    public Task StartDeliveryAsync(EntityId id) => client.PostAsync<object, object>($"courier/assignments/{id.Value}/start", new { });
    public Task CompleteDeliveryAsync(EntityId id) => client.PostAsync<object, object>($"courier/assignments/{id.Value}/complete", new { });
    public Task FailDeliveryAsync(EntityId id, string reason) =>
        client.PostAsync<FailDeliveryRequest, object>($"courier/assignments/{id.Value}/fail", new(reason));

    private static OperationalOrder ToOperational(OperationalOrderResponse order) => new(new(order.Id),
        order.OrderNumber, order.Channel, order.Status, order.Total, order.PaidAmount, order.TableNumber,
        order.DeliveryStatus, ToId(order.AssignedStaffId), order.PendingDiscountIds.Select(id => new EntityId(id)).ToArray());

    private static PosOrderDetail ToDetail(OperationsOrderDetailResponse response)
    {
        var order = response.Order;
        return new(new(order.Id), order.OrderNumber, order.Channel, order.Status, null, null, null,
            order.TableNumber, null, null, null, null, order.Total, 0m, order.Total,
            response.Lines.Select(line => new PosLineSnapshot(line.ItemName, line.SizeName, line.Quantity,
                0m, 0m, line.Addons.Select(addon => new PosAddonSnapshot(addon, 0m)).ToArray())).ToArray(),
            response.Discounts.Select(discount => new PosDiscountSummary(new(discount.DiscountId), discount.Type,
                discount.Value, "pending", discount.ComputedAmount, string.Empty)).ToArray());
    }

    private static EntityId? ToId(Guid? id) => id is { } value ? new EntityId(value) : null;
}

public sealed record OperationsSnapshot(IReadOnlyList<OperationalOrder> Orders,
    IReadOnlyList<OperationalTable> Tables, IReadOnlyList<DeliveryStaffOption> DeliveryStaff,
    IReadOnlyList<PosStaffOption> Staff);
