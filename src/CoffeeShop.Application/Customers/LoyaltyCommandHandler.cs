namespace CoffeeShop.Application.Customers;

using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Customers;
using CoffeeShop.Domain.Sales;

public sealed class LoyaltyCommandHandler
{
    private static readonly EntityId SettingsId = new(new Guid("00000000-0000-0000-0000-000000000501"));
    private readonly ICustomerRepository repository;
    private readonly IUnitOfWork unitOfWork;
    private readonly ITransactionManager transactionManager;
    private readonly IClock clock;
    private readonly ICurrentUser currentUser;

    public LoyaltyCommandHandler(
        ICustomerRepository repository,
        IUnitOfWork unitOfWork,
        ITransactionManager transactionManager,
        IClock clock,
        ICurrentUser currentUser)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        this.transactionManager = transactionManager ?? throw new ArgumentNullException(nameof(transactionManager));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<Result<LoyaltyOperationResult>> EarnForCompletedOrderAsync(
        EarnOrderLoyaltyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var actor = GetActor();
        if (actor.IsFailure)
        {
            return Result<LoyaltyOperationResult>.Failure(actor.Error!);
        }

        return await transactionManager.ExecuteInTransactionAsync(async () =>
        {
            var loaded = await LoadOrderCustomerAndSettingsAsync(command.OrderId, cancellationToken);
            if (loaded.IsFailure)
            {
                return Result<LoyaltyOperationResult>.Failure(loaded.Error!);
            }

            var (order, customer, settings) = loaded.Value;
            var existing = customer.LoyaltyTransactions.SingleOrDefault(transaction =>
                transaction.OrderId == order.Id && transaction.Type == LoyaltyTransactionType.Earn);
            if (existing is not null)
            {
                return Result<LoyaltyOperationResult>.Success(new(
                    existing.Id, existing.Points, 0m, AlreadyApplied: true));
            }

            var calculated = LoyaltyCalculations.CalculateEarnedPoints(customer, order, settings);
            if (calculated.IsFailure)
            {
                return Result<LoyaltyOperationResult>.Failure(calculated.Error!);
            }

            if (calculated.Value == 0)
            {
                return Result<LoyaltyOperationResult>.Success(new(
                    TransactionId: null, Points: 0, DiscountAmount: 0m, AlreadyApplied: false));
            }

            var earned = LoyaltyLedger.Earn(
                customer, EntityId.New(), calculated.Value, clock.UtcNow, order.Id, actor.Value);
            if (earned.IsFailure)
            {
                return Result<LoyaltyOperationResult>.Failure(earned.Error!);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<LoyaltyOperationResult>.Success(new(
                earned.Value.Id, earned.Value.Points, 0m, AlreadyApplied: false));
        }, cancellationToken);
    }

    public async Task<Result<LoyaltyOperationResult>> RedeemForOrderAsync(
        RedeemOrderLoyaltyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var actor = GetActor();
        if (actor.IsFailure)
        {
            return Result<LoyaltyOperationResult>.Failure(actor.Error!);
        }

        return await transactionManager.ExecuteInTransactionAsync(async () =>
        {
            var loaded = await LoadOrderCustomerAndSettingsAsync(command.OrderId, cancellationToken);
            if (loaded.IsFailure)
            {
                return Result<LoyaltyOperationResult>.Failure(loaded.Error!);
            }

            var (order, customer, settings) = loaded.Value;
            var existing = customer.LoyaltyTransactions.SingleOrDefault(transaction =>
                transaction.OrderId == order.Id && transaction.Type == LoyaltyTransactionType.Redeem);
            if (existing is not null)
            {
                if (existing.Points != -command.Points || order.LoyaltyPointsRedeemed != command.Points)
                {
                    return Result<LoyaltyOperationResult>.Failure(Error.Conflict(
                        "loyalty.retry_mismatch",
                        "The order already has a different loyalty redemption."));
                }

                return Result<LoyaltyOperationResult>.Success(new(
                    existing.Id, command.Points, order.LoyaltyDiscountAmount.Amount, AlreadyApplied: true));
            }

            if (order.LoyaltyPointsRedeemed != 0 || order.LoyaltyDiscountAmount.Amount != 0)
            {
                return Result<LoyaltyOperationResult>.Failure(Error.Conflict(
                    "loyalty.order_redemption_mismatch",
                    "The order contains a loyalty discount without its ledger transaction."));
            }

            var discount = LoyaltyCalculations.CalculateRedemptionDiscount(customer, order, settings, command.Points);
            if (discount.IsFailure)
            {
                return Result<LoyaltyOperationResult>.Failure(discount.Error!);
            }

            var applied = LoyaltyCalculations.ApplyRedemption(order, command.Points, discount.Value);
            if (applied.IsFailure)
            {
                return Result<LoyaltyOperationResult>.Failure(applied.Error!);
            }

            var redeemed = LoyaltyLedger.Redeem(
                customer, EntityId.New(), command.Points, clock.UtcNow, order.Id, actor.Value);
            if (redeemed.IsFailure)
            {
                return Result<LoyaltyOperationResult>.Failure(redeemed.Error!);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<LoyaltyOperationResult>.Success(new(
                redeemed.Value.Id, command.Points, discount.Value.Amount, AlreadyApplied: false));
        }, cancellationToken);
    }

    public async Task<Result<LoyaltyReversalResult>> ReverseForCancelledOrderAsync(
        ReverseCancelledOrderLoyaltyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var actor = GetActor();
        if (actor.IsFailure)
        {
            return Result<LoyaltyReversalResult>.Failure(actor.Error!);
        }

        return await transactionManager.ExecuteInTransactionAsync(async () =>
        {
            var order = await repository.LoadOrderForLoyaltyUpdateAsync(command.OrderId, cancellationToken);
            if (order is null)
            {
                return Result<LoyaltyReversalResult>.Failure(OrderNotFound(command.OrderId));
            }

            if (order.Status != OrderStatus.Cancelled)
            {
                return Result<LoyaltyReversalResult>.Failure(Error.Conflict(
                    "loyalty.order_not_cancelled",
                    "Loyalty transactions can be reversed only after the order is cancelled."));
            }

            if (order.CustomerId is null)
            {
                return Result<LoyaltyReversalResult>.Success(new([], AlreadyApplied: true));
            }

            var customer = await repository.LoadCustomerForLoyaltyUpdateAsync(order.CustomerId, cancellationToken);
            if (customer is null)
            {
                return Result<LoyaltyReversalResult>.Failure(CustomerNotFound(order.CustomerId));
            }

            var originals = customer.LoyaltyTransactions
                .Where(transaction => transaction.OrderId == order.Id
                    && transaction.Type is LoyaltyTransactionType.Earn or LoyaltyTransactionType.Redeem)
                .OrderBy(transaction => transaction.Type == LoyaltyTransactionType.Redeem ? 0 : 1)
                .ThenBy(transaction => transaction.CreatedAt.Value)
                .ThenBy(transaction => transaction.Id.Value)
                .ToArray();
            var existing = customer.LoyaltyTransactions
                .Where(transaction => transaction.ReversesTransactionId is not null
                    && originals.Any(original => original.Id == transaction.ReversesTransactionId))
                .ToArray();
            var reversedOriginalIds = existing.Select(transaction => transaction.ReversesTransactionId!).ToHashSet();
            var pending = originals.Where(original => !reversedOriginalIds.Contains(original.Id)).ToArray();
            if (pending.Length == 0)
            {
                return Result<LoyaltyReversalResult>.Success(new(
                    existing.Select(transaction => transaction.Id).ToArray(), AlreadyApplied: true));
            }

            var resultingBalance = (long)customer.LoyaltyPointsBalance - pending.Sum(transaction => (long)transaction.Points);
            if (resultingBalance < 0 || resultingBalance > int.MaxValue)
            {
                return Result<LoyaltyReversalResult>.Failure(Error.Conflict(
                    "loyalty.reversal_balance_conflict",
                    "Reversing the order's loyalty transactions would exceed the supported customer balance range."));
            }

            var reversalIds = existing.Select(transaction => transaction.Id).ToList();
            foreach (var original in pending)
            {
                var reversed = LoyaltyLedger.Reverse(
                    customer, EntityId.New(), original.Id, clock.UtcNow, actor.Value);
                if (reversed.IsFailure)
                {
                    return Result<LoyaltyReversalResult>.Failure(reversed.Error!);
                }

                reversalIds.Add(reversed.Value.Id);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<LoyaltyReversalResult>.Success(new(reversalIds.AsReadOnly(), AlreadyApplied: false));
        }, cancellationToken);
    }

    public async Task<Result<EntityId>> UpdateSettingsAsync(
        UpdateLoyaltySettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var actor = GetActor();
        if (actor.IsFailure)
        {
            return Result<EntityId>.Failure(actor.Error!);
        }

        return await transactionManager.ExecuteInTransactionAsync(async () =>
        {
            var settings = await repository.LoadLoyaltySettingsForUpdateAsync(cancellationToken);
            if (settings is null)
            {
                var created = LoyaltyCalculations.CreateSettings(
                    SettingsId,
                    command.EarnPointsPerCurrencyUnit,
                    command.RedeemValuePerPoint,
                    command.MaxRedeemablePercentage,
                    clock.UtcNow,
                    actor.Value);
                if (created.IsFailure)
                {
                    return Result<EntityId>.Failure(created.Error!);
                }

                repository.AddLoyaltySettings(created.Value);
                settings = created.Value;
            }
            else
            {
                var updated = LoyaltyCalculations.UpdateSettings(
                    settings,
                    command.EarnPointsPerCurrencyUnit,
                    command.RedeemValuePerPoint,
                    command.MaxRedeemablePercentage,
                    clock.UtcNow,
                    actor.Value);
                if (updated.IsFailure)
                {
                    return Result<EntityId>.Failure(updated.Error!);
                }
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<EntityId>.Success(settings.Id);
        }, cancellationToken);
    }

    private async Task<Result<(Order Order, Customer Customer, LoyaltySettings Settings)>> LoadOrderCustomerAndSettingsAsync(
        EntityId orderId,
        CancellationToken cancellationToken)
    {
        var order = await repository.LoadOrderForLoyaltyUpdateAsync(orderId, cancellationToken);
        if (order is null)
        {
            return Result<(Order, Customer, LoyaltySettings)>.Failure(OrderNotFound(orderId));
        }

        if (order.CustomerId is null)
        {
            return Result<(Order, Customer, LoyaltySettings)>.Failure(Error.Conflict(
                "loyalty.customer_required", "The order does not have an attached customer."));
        }

        var customer = await repository.LoadCustomerForLoyaltyUpdateAsync(order.CustomerId, cancellationToken);
        if (customer is null)
        {
            return Result<(Order, Customer, LoyaltySettings)>.Failure(CustomerNotFound(order.CustomerId));
        }

        var settings = await repository.LoadLoyaltySettingsForUpdateAsync(cancellationToken);
        return settings is null
            ? Result<(Order, Customer, LoyaltySettings)>.Failure(Error.NotFound(
                "loyalty.settings_not_found", "Loyalty settings have not been configured."))
            : Result<(Order, Customer, LoyaltySettings)>.Success((order, customer, settings));
    }

    private Result<EntityId> GetActor() => currentUser.IsAuthenticated && currentUser.Id is not null
        ? Result<EntityId>.Success(currentUser.Id)
        : Result<EntityId>.Failure(Error.Authentication(
            "loyalty.authentication_required", "An authenticated actor is required for loyalty changes."));

    private static Error OrderNotFound(EntityId id) =>
        Error.NotFound("loyalty.order_not_found", $"Order '{id}' was not found.");

    private static Error CustomerNotFound(EntityId id) =>
        Error.NotFound("customers.customer_not_found", $"Customer '{id}' was not found.");
}
