namespace CoffeeShop.Application.Sales;

using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Application.Customers;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Domain.Sales;

/// <summary>
/// Cancels the order and compensates inventory and loyalty in one transaction.
/// Nested commands join that transaction; a failed Result rolls back even their saved changes.
/// </summary>
public sealed class OrderCancellationCommandHandler(
    IOrderCancellationRepository repository,
    InventoryCommandHandler inventory,
    LoyaltyCommandHandler loyalty,
    IUnitOfWork unitOfWork,
    ITransactionManager transactionManager,
    IClock clock,
    ICurrentUser currentUser)
{
    public async Task<Result<OrderCancellationResult>> CancelAsync(
        CancelOrderCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!currentUser.IsAuthenticated || currentUser.Id is null)
        {
            return Result<OrderCancellationResult>.Failure(Error.Authentication(
                "orders.authentication_required", "An authenticated actor is required to cancel an order."));
        }

        if (!currentUser.HasPermission("orders.cancel"))
        {
            return Result<OrderCancellationResult>.Failure(Error.Authorization(
                "orders.permission_required", "The 'orders.cancel' permission is required."));
        }

        if (string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Trim().Length > 255)
        {
            return Result<OrderCancellationResult>.Failure(Error.Validation(
                "orders.cancellation_reason_required", "Cancellation requires a reason of at most 255 characters."));
        }

        var actorId = currentUser.Id;
        return await transactionManager.ExecuteInTransactionAsync(async () =>
        {
            var order = await repository.LoadOrderForCancellationAsync(command.OrderId, cancellationToken);
            if (order is null)
            {
                return Result<OrderCancellationResult>.Failure(Error.NotFound(
                    "orders.order_not_found", $"Order '{command.OrderId}' was not found."));
            }

            // The documented retry contract is conflict, with no new history or effects.
            if (order.Status is OrderStatus.Cancelled or OrderStatus.Completed)
            {
                return Result<OrderCancellationResult>.Failure(Error.Conflict(
                    "orders.invalid_state", $"A {order.Status} order cannot be cancelled."));
            }

            DiningTable? table = null;
            if (order.Channel == "dine_in" && order.TableId is not null)
            {
                table = await repository.LoadTableForCancellationAsync(order.TableId, cancellationToken);
                // A draft does not occupy its selected table. Never release another order's table.
                if (order.Status != OrderStatus.Draft && (table is null || !table.IsOccupied || table.ActiveOrderId != order.Id))
                {
                    return Result<OrderCancellationResult>.Failure(Error.Conflict(
                        "orders.table_assignment_conflict", "The table is no longer occupied by this order."));
                }
            }

            order.TransitionTo(OrderStatus.Cancelled, actorId, clock.UtcNow, command.Reason);
            var restored = await inventory.RestoreOrderInventoryAsync(new(order.Id, order.CancelReason!, actorId), cancellationToken);
            if (restored.IsFailure)
            {
                return Result<OrderCancellationResult>.Failure(restored.Error!);
            }

            var reversed = await loyalty.ReverseForCancelledOrderAsync(new(order.Id), cancellationToken);
            if (reversed.IsFailure)
            {
                return Result<OrderCancellationResult>.Failure(reversed.Error!);
            }

            if (table?.IsOccupied == true && table.ActiveOrderId == order.Id)
            {
                table.Release(order.Id);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<OrderCancellationResult>.Success(new(order.Id, order.Status, order.CancelledAt!));
        }, cancellationToken);
    }
}
