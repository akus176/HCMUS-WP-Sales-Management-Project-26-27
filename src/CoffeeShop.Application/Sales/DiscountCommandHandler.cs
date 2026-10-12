namespace CoffeeShop.Application.Sales;

using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public sealed class DiscountCommandHandler
{
    private readonly IDiscountRepository repository;
    private readonly IUnitOfWork unitOfWork;
    private readonly ITransactionManager transactionManager;
    private readonly IClock clock;
    private readonly ICurrentUser currentUser;

    public DiscountCommandHandler(IDiscountRepository repository, IUnitOfWork unitOfWork,
        ITransactionManager transactionManager, IClock clock, ICurrentUser currentUser)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        this.transactionManager = transactionManager ?? throw new ArgumentNullException(nameof(transactionManager));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<DiscountOperationResult>> RequestAsync(
        RequestDiscountCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var actor = GetActor("discounts.request");
        if (actor.IsFailure) return Result<DiscountOperationResult>.Failure(actor.Error!);
        if (command.DiscountId is null || command.OrderId is null)
        {
            return Result<DiscountOperationResult>.Failure(Error.Validation(
                "discounts.invalid", "Discount and order identifiers are required."));
        }

        return await transactionManager.ExecuteInTransactionAsync(async () =>
        {
            var order = await repository.LoadOrderForDiscountUpdateAsync(command.OrderId, cancellationToken);
            if (order is null) return Result<DiscountOperationResult>.Failure(OrderNotFound(command.OrderId));
            if (await repository.FindDiscountOrderIdAsync(command.DiscountId, cancellationToken) is not null)
            {
                return Result<DiscountOperationResult>.Failure(Error.Conflict(
                    "discounts.identifier_conflict", "The discount identifier is already recorded."));
            }

            return await ApplyAsync(order, () =>
            {
                var discount = order.RequestDiscount(command.DiscountId, command.Type,
                    command.Value, command.Reason, actor.Value, clock.UtcNow);
                repository.AddDiscount(discount);
                return discount;
            }, cancellationToken);
        }, cancellationToken);
    }

    public Task<Result<DiscountOperationResult>> ApproveAsync(
        ApproveDiscountCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return DecideAsync(command.DiscountId, approve: true, cancellationToken);
    }

    public Task<Result<DiscountOperationResult>> RejectAsync(
        RejectDiscountCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return DecideAsync(command.DiscountId, approve: false, cancellationToken);
    }

    private async Task<Result<DiscountOperationResult>> DecideAsync(
        EntityId discountId, bool approve, CancellationToken cancellationToken)
    {
        var actor = GetActor("discounts.approve");
        if (actor.IsFailure) return Result<DiscountOperationResult>.Failure(actor.Error!);
        if (discountId is null)
        {
            return Result<DiscountOperationResult>.Failure(Error.Validation(
                "discounts.invalid", "A discount identifier is required."));
        }

        return await transactionManager.ExecuteInTransactionAsync(async () =>
        {
            var orderId = await repository.FindDiscountOrderIdAsync(discountId, cancellationToken);
            if (orderId is null) return Result<DiscountOperationResult>.Failure(DiscountNotFound(discountId));
            var order = await repository.LoadOrderForDiscountUpdateAsync(orderId, cancellationToken);
            if (order is null) return Result<DiscountOperationResult>.Failure(OrderNotFound(orderId));
            if (!order.Discounts.Any(discount => discount.Id == discountId))
            {
                return Result<DiscountOperationResult>.Failure(DiscountNotFound(discountId));
            }

            return await ApplyAsync(order, () => approve
                ? order.ApproveDiscount(discountId, actor.Value, clock.UtcNow)
                : order.RejectDiscount(discountId, actor.Value, clock.UtcNow), cancellationToken);
        }, cancellationToken);
    }

    private async Task<Result<DiscountOperationResult>> ApplyAsync(
        Order order, Func<Discount> apply, CancellationToken cancellationToken)
    {
        Discount discount;
        try
        {
            discount = apply();
        }
        catch (ArgumentOutOfRangeException exception) when (exception.ParamName == "payableTotal")
        {
            return Result<DiscountOperationResult>.Failure(Error.BusinessRule(
                "discounts.excessive_or_subcent", exception.Message));
        }
        catch (ArgumentException exception)
        {
            return Result<DiscountOperationResult>.Failure(Error.Validation("discounts.invalid", exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return Result<DiscountOperationResult>.Failure(Error.Conflict("discounts.invalid_state", exception.Message));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<DiscountOperationResult>.Success(new(discount.Id, discount.Status,
            discount.ComputedAmount?.Amount, order.DiscountTotal.Amount, order.Total.Amount));
    }

    private Result<EntityId> GetActor(string permission)
    {
        if (!currentUser.IsAuthenticated || currentUser.Id is null)
        {
            return Result<EntityId>.Failure(Error.Authentication(
                "discounts.authentication_required", "An authenticated actor is required to change discounts."));
        }

        return currentUser.HasPermission(permission)
            ? Result<EntityId>.Success(currentUser.Id)
            : Result<EntityId>.Failure(Error.Authorization(
                "discounts.permission_required", $"The '{permission}' permission is required."));
    }

    private static Error OrderNotFound(EntityId id) =>
        Error.NotFound("discounts.order_not_found", $"Order '{id}' was not found.");

    private static Error DiscountNotFound(EntityId id) =>
        Error.NotFound("discounts.not_found", $"Discount '{id}' was not found.");
}
