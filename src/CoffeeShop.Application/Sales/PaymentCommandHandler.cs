namespace CoffeeShop.Application.Sales;

using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public sealed class PaymentCommandHandler
{
    private readonly IPaymentRepository repository;
    private readonly IUnitOfWork unitOfWork;
    private readonly ITransactionManager transactionManager;
    private readonly IClock clock;
    private readonly ICurrentUser currentUser;

    public PaymentCommandHandler(
        IPaymentRepository repository,
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

    public async Task<Result<PaymentOperationResult>> RecordAsync(
        RecordPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var actor = GetActor();
        if (actor.IsFailure)
        {
            return Result<PaymentOperationResult>.Failure(actor.Error!);
        }

        return await transactionManager.ExecuteInTransactionAsync(async () =>
        {
            var order = await repository.LoadOrderForPaymentUpdateAsync(command.OrderId, cancellationToken);
            if (order is null)
            {
                return Result<PaymentOperationResult>.Failure(Error.NotFound(
                    "payments.order_not_found", $"Order '{command.OrderId}' was not found."));
            }

            Money amount;
            try
            {
                amount = new Money(command.Amount);
            }
            catch (ArgumentException exception)
            {
                return Result<PaymentOperationResult>.Failure(Validation(exception.Message));
            }

            var existing = order.Payments.SingleOrDefault(payment => payment.Id == command.PaymentId);
            if (existing is not null)
            {
                return existing.Matches(command.Method, amount, command.Status, command.Reference)
                    ? Result<PaymentOperationResult>.Success(ToResult(order, existing.Id, alreadyRecorded: true))
                    : Result<PaymentOperationResult>.Failure(Error.Conflict(
                        "payments.retry_mismatch",
                        "The payment identifier is already associated with different payment data."));
            }

            var paymentWithSameId = await repository.FindPaymentAsync(command.PaymentId, cancellationToken);
            if (paymentWithSameId is not null)
            {
                return Result<PaymentOperationResult>.Failure(Error.Conflict(
                    "payments.identifier_conflict",
                    "The payment identifier is already associated with another order."));
            }

            Payment payment;
            try
            {
                payment = order.RecordPayment(
                    command.PaymentId,
                    command.Method,
                    amount,
                    command.Status,
                    clock.UtcNow,
                    actor.Value,
                    command.Reference);
            }
            catch (ArgumentException exception)
            {
                return Result<PaymentOperationResult>.Failure(Validation(exception.Message));
            }
            catch (InvalidOperationException exception)
            {
                return Result<PaymentOperationResult>.Failure(Error.Conflict(
                    "payments.invalid_state", exception.Message));
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<PaymentOperationResult>.Success(ToResult(order, payment.Id, alreadyRecorded: false));
        }, cancellationToken);
    }

    private Result<EntityId> GetActor() => currentUser.IsAuthenticated && currentUser.Id is not null
        ? Result<EntityId>.Success(currentUser.Id)
        : Result<EntityId>.Failure(Error.Authentication(
            "payments.authentication_required", "An authenticated actor is required to record payments."));

    private static PaymentOperationResult ToResult(Order order, EntityId paymentId, bool alreadyRecorded) => new(
        paymentId,
        order.PaidAmount.Amount,
        order.OutstandingBalance.Amount,
        order.IsFullyPaid,
        alreadyRecorded);

    private static Error Validation(string description) =>
        Error.Validation("payments.invalid", description);
}
