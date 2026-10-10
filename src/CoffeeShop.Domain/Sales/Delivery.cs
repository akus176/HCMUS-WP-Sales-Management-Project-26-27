namespace CoffeeShop.Domain.Sales;

using System.Text.RegularExpressions;
using CoffeeShop.Domain.Common;

public class Delivery
{
    private readonly List<DeliveryStatusHistory> _statusHistory = [];
    private static readonly Regex PhoneRegex = new(@"^\+?[0-9\s\-()]{7,30}$", RegexOptions.Compiled);

    protected Delivery()
    {
    }

    public Delivery(EntityId id, EntityId orderId, string recipientName, string contactNumber, string addressLine, EntityId? customerAddressId = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(orderId);

        Id = id;
        OrderId = orderId;
        RecipientName = ValidateAndNormalizeRecipientName(recipientName);
        ContactNumber = ValidateAndNormalizeContactNumber(contactNumber);
        AddressLine = ValidateAndNormalizeAddressLine(addressLine);
        CustomerAddressId = customerAddressId;
    }

    public EntityId Id { get; protected set; } = null!;
    public EntityId OrderId { get; protected set; } = null!;
    public string RecipientName { get; protected set; } = string.Empty;
    public string ContactNumber { get; protected set; } = string.Empty;
    public string AddressLine { get; protected set; } = string.Empty;
    public EntityId? CustomerAddressId { get; protected set; }
    public string Status { get; protected set; } = DeliveryStatus.Unassigned;
    public EntityId? AssignedStaffId { get; protected set; }
    public UtcTimestamp? AssignedAt { get; protected set; }
    public UtcTimestamp? OutForDeliveryAt { get; protected set; }
    public UtcTimestamp? DeliveredAt { get; protected set; }
    public UtcTimestamp? FailedAt { get; protected set; }
    public string? FailureReason { get; protected set; }
    public IReadOnlyCollection<DeliveryStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    public bool IsOrderSpecific => CustomerAddressId is null;
    public bool IsReusableCustomerAddress => CustomerAddressId is not null;

    public static Delivery CreateOrderSpecific(
        EntityId id,
        EntityId orderId,
        string recipientName,
        string contactNumber,
        string addressLine) =>
        new(id, orderId, recipientName, contactNumber, addressLine, customerAddressId: null);

    public static Delivery CreateFromCustomerAddress(
        EntityId id,
        EntityId orderId,
        string recipientName,
        string contactNumber,
        EntityId customerAddressId,
        string addressLine)
    {
        ArgumentNullException.ThrowIfNull(customerAddressId);
        return new(id, orderId, recipientName, contactNumber, addressLine, customerAddressId);
    }

    public DeliveryStatusHistory Assign(EntityId staffId, EntityId actorId, UtcTimestamp changedAt) =>
        Transition(DeliveryStatus.Assigned, actorId, changedAt, staffId);

    public DeliveryStatusHistory StartDelivery(EntityId actorId, UtcTimestamp changedAt) =>
        Transition(DeliveryStatus.OutForDelivery, actorId, changedAt);

    public DeliveryStatusHistory Complete(EntityId actorId, UtcTimestamp changedAt) =>
        Transition(DeliveryStatus.Delivered, actorId, changedAt);

    public DeliveryStatusHistory Fail(string reason, EntityId actorId, UtcTimestamp changedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var history = Transition(DeliveryStatus.Failed, actorId, changedAt);
        FailureReason = reason.Trim();
        return history;
    }

    private DeliveryStatusHistory Transition(string target, EntityId actorId, UtcTimestamp changedAt, EntityId? staffId = null)
    {
        ArgumentNullException.ThrowIfNull(actorId); ArgumentNullException.ThrowIfNull(changedAt);
        var valid = Status switch
        {
            DeliveryStatus.Unassigned => target == DeliveryStatus.Assigned,
            DeliveryStatus.Assigned => target == DeliveryStatus.OutForDelivery,
            DeliveryStatus.OutForDelivery => target is DeliveryStatus.Delivered or DeliveryStatus.Failed,
            _ => false,
        };
        if (!valid) throw new InvalidOperationException($"Delivery cannot transition from '{Status}' to '{target}'.");
        if (target == DeliveryStatus.Assigned && staffId is null) throw new ArgumentException("Delivery assignment requires a staff member.", nameof(staffId));
        var history = new DeliveryStatusHistory(EntityId.New(), Id, Status, target, actorId, changedAt);
        Status = target;
        if (target == DeliveryStatus.Assigned) { AssignedStaffId = staffId; AssignedAt = changedAt; }
        if (target == DeliveryStatus.OutForDelivery) OutForDeliveryAt = changedAt;
        if (target == DeliveryStatus.Delivered) DeliveredAt = changedAt;
        if (target == DeliveryStatus.Failed) FailedAt = changedAt;
        _statusHistory.Add(history);
        return history;
    }

    private static string ValidateAndNormalizeRecipientName(string recipientName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientName);
        var trimmed = recipientName.Trim();
        if (trimmed.Length > 120)
        {
            throw new ArgumentException("Recipient name cannot exceed 120 characters.", nameof(recipientName));
        }

        return trimmed;
    }

    private static string ValidateAndNormalizeContactNumber(string contactNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contactNumber);
        var trimmed = contactNumber.Trim();
        if (trimmed.Length > 30)
        {
            throw new ArgumentException("Contact number cannot exceed 30 characters.", nameof(contactNumber));
        }

        if (!PhoneRegex.IsMatch(trimmed) || trimmed.Count(char.IsDigit) < 7)
        {
            throw new ArgumentException("Contact number must contain at least 7 digits and valid phone characters.", nameof(contactNumber));
        }

        return trimmed;
    }

    private static string ValidateAndNormalizeAddressLine(string addressLine)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addressLine);
        var trimmed = addressLine.Trim();
        if (trimmed.Length > 500)
        {
            throw new ArgumentException("Address line cannot exceed 500 characters.", nameof(addressLine));
        }

        return trimmed;
    }
}
