namespace CoffeeShop.Application.Sales;

using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public sealed class DeliveryCommandHandler(IDeliveryRepository repository, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<Result> AssignAsync(AssignDeliveryCommand command, CancellationToken cancellationToken = default)
    {
        var order = await repository.FindOrderAsync(command.OrderId, cancellationToken);
        if (order is null) return Result.Failure(NotFound("order", command.OrderId));
        if (order.Channel != "delivery" || order.Status != OrderStatus.Ready) return Result.Failure(Error.Conflict("delivery.order_not_ready", "Only ready delivery orders can be assigned."));
        if (!await repository.IsEligibleDeliveryStaffAsync(command.StaffId, cancellationToken)) return Result.Failure(Error.Validation("delivery.ineligible_staff", "Assigned staff must be active DeliveryStaff."));
        var delivery = await repository.FindDeliveryForOrderAsync(command.OrderId, cancellationToken);
        if (delivery is null) return Result.Failure(NotFound("delivery", command.OrderId));
        return await ApplyAsync(() => delivery.Assign(command.StaffId, command.ActorId, clock.UtcNow), cancellationToken);
    }

    public async Task<Result> StartAsync(StartDeliveryCommand command, CancellationToken cancellationToken = default) =>
        await TransitionAsync(command.OrderId, command.ActorId, delivery => delivery.StartDelivery(command.ActorId, clock.UtcNow), cancellationToken);
    public async Task<Result> CompleteAsync(CompleteDeliveryCommand command, CancellationToken cancellationToken = default) =>
        await TransitionAsync(command.OrderId, command.ActorId, delivery => delivery.Complete(command.ActorId, clock.UtcNow), cancellationToken);
    public async Task<Result> FailAsync(FailDeliveryCommand command, CancellationToken cancellationToken = default) =>
        await TransitionAsync(command.OrderId, command.ActorId, delivery => delivery.Fail(command.Reason, command.ActorId, clock.UtcNow), cancellationToken);

    private async Task<Result> TransitionAsync(EntityId orderId, EntityId actorId, Func<Delivery, DeliveryStatusHistory> transition, CancellationToken token)
    {
        var delivery = await repository.FindDeliveryForOrderAsync(orderId, token);
        if (delivery is null) return Result.Failure(NotFound("delivery", orderId));
        return await ApplyAsync(() => transition(delivery), token);
    }
    private async Task<Result> ApplyAsync(Func<DeliveryStatusHistory> apply, CancellationToken token)
    {
        try { repository.AddStatusHistory(apply()); await unitOfWork.SaveChangesAsync(token); return Result.Success(); }
        catch (ArgumentException ex) { return Result.Failure(Error.Validation("delivery.invalid_input", ex.Message)); }
        catch (InvalidOperationException ex) { return Result.Failure(Error.Conflict("delivery.invalid_transition", ex.Message)); }
    }
    private static Error NotFound(string type, EntityId id) => Error.NotFound($"delivery.{type}_not_found", $"{type} '{id}' was not found.");
}
