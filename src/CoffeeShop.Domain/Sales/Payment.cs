namespace CoffeeShop.Domain.Sales;

using CoffeeShop.Domain.Common;

public class Payment
{
    protected Payment()
    {
    }

    public Payment(
        EntityId id,
        EntityId orderId,
        string method,
        Money amount,
        string status,
        UtcTimestamp paidAt,
        EntityId recordedBy,
        string? reference = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(orderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        ArgumentNullException.ThrowIfNull(paidAt);
        ArgumentNullException.ThrowIfNull(recordedBy);

        if (!PaymentMethod.IsKnown(method))
        {
            throw new ArgumentException("Payment method must be cash, card, or e_wallet.", nameof(method));
        }

        if (amount.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be positive.");
        }

        if (!PaymentStatus.IsKnown(status))
        {
            throw new ArgumentException("Payment status must be pending, succeeded, or failed.", nameof(status));
        }

        var normalizedReference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        if (PaymentMethod.RequiresReference(method) && normalizedReference is null)
        {
            throw new ArgumentException("Card and e-wallet payments require a reference.", nameof(reference));
        }

        if (normalizedReference?.Length > 100)
        {
            throw new ArgumentException("Payment reference cannot exceed 100 characters.", nameof(reference));
        }

        Id = id;
        OrderId = orderId;
        Method = method;
        Amount = amount;
        Status = status;
        Reference = normalizedReference;
        PaidAt = paidAt;
        RecordedBy = recordedBy;
    }

    public EntityId Id { get; protected set; } = null!;
    public EntityId OrderId { get; protected set; } = null!;
    public string Method { get; protected set; } = string.Empty;
    public Money Amount { get; protected set; } = Money.Zero;
    public string Status { get; protected set; } = PaymentStatus.Succeeded;
    public string? Reference { get; protected set; }
    public UtcTimestamp PaidAt { get; protected set; } = null!;
    public EntityId RecordedBy { get; protected set; } = null!;

    public bool CountsTowardPaidBalance => PaymentStatus.CountsTowardPaidBalance(Status);

    public bool Matches(string method, Money amount, string status, string? reference)
    {
        ArgumentNullException.ThrowIfNull(amount);
        var normalizedReference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        return Method == method
            && Amount == amount
            && Status == status
            && string.Equals(Reference, normalizedReference, StringComparison.Ordinal);
    }
}
