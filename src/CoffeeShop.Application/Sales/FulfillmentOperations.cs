namespace CoffeeShop.Application.Sales;

using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public sealed record OperationalOrder(
    EntityId Id, string OrderNumber, string Channel, string Status, decimal Total, decimal PaidAmount,
    string? TableNumber, string? DeliveryStatus, EntityId? AssignedStaffId,
    IReadOnlyList<EntityId> PendingDiscountIds)
{
    public decimal OutstandingBalance => decimal.Max(0m, Total - PaidAmount);
    public bool CanStartPreparing => Status == OrderStatus.Confirmed;
    public bool CanMarkReady => Status == OrderStatus.Preparing;
    public bool CanComplete => Status == OrderStatus.Ready && OutstandingBalance == 0m;
    public bool CanRecordPayment => Status is not (OrderStatus.Completed or OrderStatus.Cancelled);
    public bool CanCancel => Status is OrderStatus.Draft or OrderStatus.Confirmed or OrderStatus.Preparing or OrderStatus.Ready;
    public bool CanAssignDelivery => Channel == "delivery" && Status == OrderStatus.Ready && DeliveryStatus == DeliveryStatusNames.Unassigned;
    public bool CanStartDelivery => DeliveryStatus == DeliveryStatusNames.Assigned;
    public bool CanFinishDelivery => DeliveryStatus == DeliveryStatusNames.OutForDelivery;
}

public static class DeliveryStatusNames
{
    public const string Unassigned = "unassigned";
    public const string Assigned = "assigned";
    public const string OutForDelivery = "out_for_delivery";
}

public sealed record OperationalTable(EntityId Id, string TableNumber, int? Seats, bool IsOccupied, EntityId? ActiveOrderId);
public sealed record DeliveryStaffOption(EntityId Id, string FullName);

public interface IFulfillmentOperationsRepository
{
    Task<IReadOnlyList<OperationalOrder>> GetOrdersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OperationalTable>> GetTablesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeliveryStaffOption>> GetDeliveryStaffAsync(CancellationToken cancellationToken = default);
}

public sealed class FulfillmentOperationsQueryHandler(IFulfillmentOperationsRepository repository)
{
    public Task<IReadOnlyList<OperationalOrder>> GetOrdersAsync(CancellationToken cancellationToken = default) => repository.GetOrdersAsync(cancellationToken);
    public Task<IReadOnlyList<OperationalTable>> GetTablesAsync(CancellationToken cancellationToken = default) => repository.GetTablesAsync(cancellationToken);
    public Task<IReadOnlyList<DeliveryStaffOption>> GetDeliveryStaffAsync(CancellationToken cancellationToken = default) => repository.GetDeliveryStaffAsync(cancellationToken);
}

public sealed record TransitionOrderCommand(EntityId OrderId, string TargetStatus);

public interface IOrderFulfillmentRepository
{
    Task<Order?> LoadOrderForUpdateAsync(EntityId orderId, CancellationToken cancellationToken = default);
    Task<DiningTable?> LoadTableForUpdateAsync(EntityId tableId, CancellationToken cancellationToken = default);
}

public sealed class OrderFulfillmentCommandHandler(
    IOrderFulfillmentRepository repository,
    IUnitOfWork unitOfWork,
    ITransactionManager transactionManager,
    IClock clock,
    ICurrentUser currentUser)
{
    public async Task<Result> TransitionAsync(TransitionOrderCommand command, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.Id is null)
            return Result.Failure(Error.Authentication("orders.authentication_required", "Select an operator before changing fulfillment."));
        if (!currentUser.HasPermission("orders.fulfill"))
            return Result.Failure(Error.Authorization("orders.permission_required", "The 'orders.fulfill' permission is required."));

        return await transactionManager.ExecuteInTransactionAsync(async () =>
        {
            var order = await repository.LoadOrderForUpdateAsync(command.OrderId, cancellationToken);
            if (order is null) return Result.Failure(Error.NotFound("orders.order_not_found", $"Order '{command.OrderId}' was not found."));
            try
            {
                order.TransitionTo(command.TargetStatus, currentUser.Id, clock.UtcNow);
                if (command.TargetStatus == OrderStatus.Completed && order.TableId is not null)
                {
                    var table = await repository.LoadTableForUpdateAsync(order.TableId, cancellationToken);
                    if (table is null || !table.IsOccupied || table.ActiveOrderId != order.Id)
                        return Result.Failure(Error.Conflict("orders.table_assignment_conflict", "The table is no longer occupied by this order."));
                    table.Release(order.Id);
                }
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return Result.Success();
            }
            catch (InvalidOperationException exception) { return Result.Failure(Error.Conflict("orders.invalid_transition", exception.Message)); }
        }, cancellationToken);
    }
}
