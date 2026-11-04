namespace CoffeeShop.Api;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Sales;
using CoffeeShop.Contracts;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public static class SalesEndpoints
{
    public static void MapSalesEndpoints(this RouteGroupBuilder api)
    {
        var orders = api.MapGroup("/orders").RequireAuthorization();
        orders.MapGet("/", async (FulfillmentOperationsQueryHandler handler, CancellationToken ct) =>
            Results.Ok((await handler.GetOrdersAsync(ct)).Select(ToOperational).ToArray()))
            .RequireAuthorization("orders.create").Produces<OperationalOrderResponse[]>();
        orders.MapGet("/{id:guid}", async (Guid id, PosOrderHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var result = await handler.GetOrderAsync(new(id), ct);
            return result.IsSuccess ? Results.Ok(ToOrder(result.Value)) : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("orders.create").Produces<OrderResponse>();
        orders.MapPost("/", async (CreateOrderRequest request, PosOrderHandler handler, CancellationToken ct) =>
        {
            if (request.OrderId == Guid.Empty || request.TableId == Guid.Empty ||
                request.Customer?.CustomerId == Guid.Empty || request.Customer?.CustomerAddressId == Guid.Empty ||
                request.Lines is null || request.Lines.Any(line => line.MenuItemId == Guid.Empty ||
                    line.SizeVariantId == Guid.Empty || line.AddonIds?.Contains(Guid.Empty) == true)) return InvalidId();
            var customer = request.Customer ?? new();
            var result = await handler.CreateDraftAsync(new(new(request.OrderId), request.Channel ?? string.Empty,
                request.TableId is { } tableId ? new EntityId(tableId) : null,
                new(customer.CustomerId is { } customerId ? new EntityId(customerId) : null,
                    request.Channel == "delivery", customer.CustomerAddressId is { } addressId ? new EntityId(addressId) : null,
                    customer.RecipientName, customer.ContactNumber, customer.AddressLine),
                request.Lines.Select(line => new PosLineRequest(new(line.MenuItemId), new(line.SizeVariantId),
                    line.Quantity, (line.AddonIds ?? []).Select(id => new EntityId(id)).ToArray())).ToArray()), ct);
            return result.IsSuccess ? Results.Json(ToOrder(result.Value), statusCode: 201) : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("orders.create").Produces<OrderResponse>(201);
        orders.MapPost("/{id:guid}/confirm", async (Guid id, ConfirmOrderRequest request, PosOrderHandler handler,
            CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var result = await handler.ConfirmAsync(new(id), request.ReviewedTotal, ct);
            return result.IsSuccess ? Results.Ok(ToOrder(result.Value)) : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("orders.confirm").Produces<OrderResponse>();
        orders.MapPost("/{id:guid}/payments", async (Guid id, RecordPaymentRequest request,
            PaymentCommandHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty || request.PaymentId == Guid.Empty) return InvalidId();
            var result = await handler.RecordAsync(new(new(request.PaymentId), new(id), request.Method ?? string.Empty,
                request.Amount, request.Status ?? PaymentStatus.Succeeded, request.Reference), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("payments.record").Produces<PaymentOperationResult>();
        orders.MapPost("/{id:guid}/discounts", async (Guid id, DiscountRequest request,
            PosOrderHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty || request.DiscountId == Guid.Empty) return InvalidId();
            var result = await handler.RequestDiscountAsync(new(new(request.DiscountId), new(id),
                request.Type ?? string.Empty, request.Value, request.Reason ?? string.Empty), ct);
            return result.IsSuccess ? Results.Json(result.Value, statusCode: 201) : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("discounts.request");
        orders.MapPost("/{id:guid}/cancel", async (Guid id, CancelOrderRequest request,
            OrderCancellationCommandHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var result = await handler.CancelAsync(new(new(id), request.Reason ?? string.Empty), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("orders.cancel");
        orders.MapPost("/{id:guid}/preparing", (Guid id, OrderFulfillmentCommandHandler handler,
            CancellationToken ct) => Transition(id, OrderStatus.Preparing, handler, ct)).RequireAuthorization("orders.fulfill");
        orders.MapPost("/{id:guid}/ready", (Guid id, OrderFulfillmentCommandHandler handler,
            CancellationToken ct) => Transition(id, OrderStatus.Ready, handler, ct)).RequireAuthorization("orders.fulfill");
        orders.MapPost("/{id:guid}/complete", (Guid id, OrderFulfillmentCommandHandler handler,
            CancellationToken ct) => Transition(id, OrderStatus.Completed, handler, ct)).RequireAuthorization("orders.complete");

        api.MapGet("/tables", async (FulfillmentOperationsQueryHandler handler, CancellationToken ct) =>
            Results.Ok(await handler.GetTablesAsync(ct))).RequireAuthorization("orders.create");
        api.MapGet("/preparation-tickets", async (FulfillmentOperationsQueryHandler query,
            PosOrderHandler ordersHandler, CancellationToken ct) =>
        {
            var candidates = (await query.GetOrdersAsync(ct)).Where(order => order.Status is
                OrderStatus.Confirmed or OrderStatus.Preparing or OrderStatus.Ready);
            var tickets = new List<PreparationTicketResponse>();
            foreach (var candidate in candidates)
            {
                var detail = await ordersHandler.GetOrderAsync(candidate.Id, ct);
                if (detail.IsFailure) continue;
                tickets.Add(new(candidate.Id.Value, candidate.OrderNumber, candidate.Status,
                    candidate.TableNumber, detail.Value.Lines.Select(line => new PreparationTicketLine(
                        line.ItemName, line.SizeName, line.Quantity,
                        line.Addons.Select(addon => addon.Name).ToArray())).ToArray()));
            }
            return Results.Ok(tickets);
        }).RequireAuthorization("orders.fulfill").Produces<PreparationTicketResponse[]>();
        api.MapGet("/discounts/pending", async (FulfillmentOperationsQueryHandler query,
            PosOrderHandler ordersHandler, CancellationToken ct) =>
        {
            var pending = new List<PendingDiscountResponse>();
            foreach (var order in (await query.GetOrdersAsync(ct)).Where(order => order.PendingDiscountIds.Count > 0))
            {
                var detail = await ordersHandler.GetOrderAsync(order.Id, ct);
                if (detail.IsFailure) continue;
                pending.AddRange(detail.Value.Discounts.Where(discount => order.PendingDiscountIds.Contains(discount.Id))
                    .Select(discount => new PendingDiscountResponse(discount.Id.Value, order.Id.Value,
                        order.OrderNumber, discount.Type, discount.Value, discount.Reason, discount.Amount, order.Total)));
            }
            return Results.Ok(pending);
        }).RequireAuthorization("discounts.approve").Produces<PendingDiscountResponse[]>();
        api.MapPost("/discounts/{id:guid}/approve", (Guid id, DiscountCommandHandler handler,
            CancellationToken ct) => DecideDiscount(id, true, handler, ct)).RequireAuthorization("discounts.approve");
        api.MapPost("/discounts/{id:guid}/reject", (Guid id, DiscountCommandHandler handler,
            CancellationToken ct) => DecideDiscount(id, false, handler, ct)).RequireAuthorization("discounts.approve");
    }

    private static async Task<IResult> Transition(Guid id, string status, OrderFulfillmentCommandHandler handler, CancellationToken ct)
    {
        if (id == Guid.Empty) return InvalidId();
        var result = await handler.TransitionAsync(new(new(id), status), ct);
        return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
    }

    private static async Task<IResult> DecideDiscount(Guid id, bool approve, DiscountCommandHandler handler, CancellationToken ct)
    {
        if (id == Guid.Empty) return InvalidId();
        var result = approve ? await handler.ApproveAsync(new(new(id)), ct) : await handler.RejectAsync(new(new(id)), ct);
        return result.IsSuccess ? Results.Ok(new DiscountDecisionResponse(result.Value.DiscountId.Value,
            result.Value.Status, result.Value.ComputedAmount, result.Value.DiscountTotal, result.Value.Total))
            : ApiErrorResults.From(result.Error!);
    }

    private static OperationalOrderResponse ToOperational(OperationalOrder order) => new(order.Id.Value,
        order.OrderNumber, order.Channel, order.Status, order.Total, order.PaidAmount, order.TableNumber,
        order.DeliveryStatus, order.AssignedStaffId?.Value, order.PendingDiscountIds.Select(id => id.Value).ToArray());

    private static OrderResponse ToOrder(PosOrderDetail order) => new(order.Id.Value, order.OrderNumber,
        order.Channel, order.Status, order.CustomerId?.Value, order.TableId?.Value, order.TableNumber,
        order.Subtotal, order.DiscountTotal, order.Total, order.Lines.Select(line => new OrderLineResponse(
            line.ItemName, line.SizeName, line.Quantity, line.UnitPrice, line.Total,
            line.Addons.Select(addon => addon.Name).ToArray())).ToArray(),
        order.Discounts.Select(discount => new OrderDiscountResponse(discount.Id.Value, discount.Type,
            discount.Value, discount.Status, discount.Amount)).ToArray());

    private static IResult InvalidId() => ApiErrorResults.From(Error.Validation("sales.invalid_id", "A nonempty identifier is required."));
}
