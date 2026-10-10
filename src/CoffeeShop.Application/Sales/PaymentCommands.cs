namespace CoffeeShop.Application.Sales;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public sealed record RecordPaymentCommand(
    EntityId PaymentId,
    EntityId OrderId,
    string Method,
    decimal Amount,
    string Status = PaymentStatus.Succeeded,
    string? Reference = null);

public sealed record PaymentOperationResult(
    EntityId PaymentId,
    decimal PaidAmount,
    decimal OutstandingBalance,
    bool IsFullyPaid,
    bool AlreadyRecorded);
