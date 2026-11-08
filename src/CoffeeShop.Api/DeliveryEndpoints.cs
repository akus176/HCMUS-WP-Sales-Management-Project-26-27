namespace CoffeeShop.Api;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Application.Sales;
using CoffeeShop.Contracts;
using CoffeeShop.Domain.Sales;

public static class DeliveryEndpoints
{
    public static void MapDeliveryEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/delivery-staff", async (FulfillmentOperationsQueryHandler query, CancellationToken ct) =>
            Results.Ok(await query.GetDeliveryStaffAsync(ct))).RequireAuthorization("delivery.assign");
        api.MapGet("/delivery-staff/options", async (FulfillmentOperationsQueryHandler query, CancellationToken ct) =>
            Results.Ok((await query.GetDeliveryStaffAsync(ct)).Select(staff =>
                new DeliveryStaffResponse(staff.Id.Value, staff.FullName)).ToArray()))
            .RequireAuthorization("delivery.assign").Produces<DeliveryStaffResponse[]>().ProducesProblem(403);
        api.MapPost("/orders/{id:guid}/assign-delivery", async (Guid id, AssignDeliveryRequest request,
            DeliveryCommandHandler handler, ICurrentUser user, CancellationToken ct) =>
        {
            if (id == Guid.Empty || request.StaffId == Guid.Empty) return InvalidId();
            var result = await handler.AssignAsync(new(new(id), new(request.StaffId), user.GetRequiredId()), ct);
            return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("delivery.assign");

        var courier = api.MapGroup("/courier/assignments").RequireAuthorization("delivery.update");
        courier.MapGet("/", async (FulfillmentOperationsQueryHandler query, ICurrentUser user,
            string? status, int? page, int? pageSize, CancellationToken ct) =>
        {
            var states = status?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (states is { Length: > 0 } && states.Any(state => state is not
                (DeliveryStatusNames.Assigned or DeliveryStatusNames.OutForDelivery or DeliveryStatus.Delivered or DeliveryStatus.Failed)))
                return ApiErrorResults.From(Error.Validation("delivery.invalid_status", "Filter by assigned, out_for_delivery, delivered, or failed status."));
            var assignments = (await query.GetOrdersAsync(ct))
                .Where(order => order.AssignedStaffId == user.GetRequiredId() && order.Status != OrderStatus.Cancelled)
                .Where(order => states is null || states.Length == 0
                    ? order.DeliveryStatus is DeliveryStatusNames.Assigned or DeliveryStatusNames.OutForDelivery
                    : states.Contains(order.DeliveryStatus!))
                .OrderBy(order => order.OrderNumber).ThenBy(order => order.Id.Value)
                .Select(order => new CourierAssignmentResponse(order.Id.Value, order.OrderNumber,
                    order.DeliveryStatus!)).ToArray();
            return MobilePage.From(assignments, page, pageSize);
        }).Produces<PageResponse<CourierAssignmentResponse>>().ProducesProblem(400).ProducesProblem(403);
        courier.MapGet("/{id:guid}", async (Guid id, IDeliveryRepository repository, ICurrentUser user,
            PosOrderHandler orders, CancellationToken ct) =>
        {
            var visible = await LoadOwnedAsync(id, repository, user, ct);
            if (visible.IsFailure) return ApiErrorResults.From(visible.Error!);
            var order = await orders.GetOrderAsync(new(id), ct);
            if (order.IsFailure || order.Value.Status == OrderStatus.Cancelled ||
                visible.Value.Status is not (DeliveryStatusNames.Assigned or DeliveryStatusNames.OutForDelivery))
                return NotVisible();
            return Results.Ok(new CourierAssignmentDetailResponse(id, order.Value.OrderNumber,
                visible.Value.Status, order.Value.RecipientName!, order.Value.ContactNumber!, order.Value.AddressLine!));
        }).Produces<CourierAssignmentDetailResponse>().ProducesProblem(400).ProducesProblem(403).ProducesProblem(404);
        courier.MapPost("/{id:guid}/start", (Guid id, IDeliveryRepository repository, ICurrentUser user,
            DeliveryCommandHandler handler, CancellationToken ct) => Transition(id, repository, user, handler, 0, null, ct))
            .Produces(204).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        courier.MapPost("/{id:guid}/complete", (Guid id, IDeliveryRepository repository, ICurrentUser user,
            DeliveryCommandHandler handler, CancellationToken ct) => Transition(id, repository, user, handler, 1, null, ct))
            .Produces(204).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
        courier.MapPost("/{id:guid}/fail", (Guid id, FailDeliveryRequest request, IDeliveryRepository repository,
            ICurrentUser user, DeliveryCommandHandler handler, CancellationToken ct) =>
            Transition(id, repository, user, handler, 2, request.Reason ?? string.Empty, ct))
            .Produces(204).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
    }

    private static async Task<IResult> Transition(Guid id, IDeliveryRepository repository, ICurrentUser user,
        DeliveryCommandHandler handler, int action, string? reason, CancellationToken ct)
    {
        var visible = await LoadOwnedAsync(id, repository, user, ct);
        if (visible.IsFailure) return ApiErrorResults.From(visible.Error!);
        var actor = user.GetRequiredId();
        var result = action switch
        {
            0 => await handler.StartAsync(new(new(id), actor), ct),
            1 => await handler.CompleteAsync(new(new(id), actor), ct),
            _ => await handler.FailAsync(new(new(id), reason ?? string.Empty, actor), ct),
        };
        return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
    }

    private static async Task<Result<Delivery>> LoadOwnedAsync(Guid id, IDeliveryRepository repository,
        ICurrentUser user, CancellationToken ct)
    {
        if (id == Guid.Empty) return Result<Delivery>.Failure(Error.Validation("delivery.invalid_id", "An order identifier is required."));
        var delivery = await repository.FindDeliveryForOrderAsync(new(id), ct);
        return delivery is not null && delivery.AssignedStaffId == user.GetRequiredId()
            ? Result<Delivery>.Success(delivery)
            : Result<Delivery>.Failure(Error.NotFound("delivery.assignment_not_found", "Assignment not found."));
    }

    private static IResult InvalidId() => ApiErrorResults.From(Error.Validation("delivery.invalid_id", "A nonempty identifier is required."));
    private static IResult NotVisible() => ApiErrorResults.From(Error.NotFound("delivery.assignment_not_found", "Assignment not found."));
}
