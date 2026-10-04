namespace CoffeeShop.Domain.Sales;

using System.Text.RegularExpressions;
using CoffeeShop.Domain.Common;

public class Delivery
{
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
    public string Status { get; protected set; } = "unassigned";
    public EntityId? AssignedStaffId { get; protected set; }
    public UtcTimestamp? AssignedAt { get; protected set; }
    public UtcTimestamp? OutForDeliveryAt { get; protected set; }
    public UtcTimestamp? DeliveredAt { get; protected set; }
    public UtcTimestamp? FailedAt { get; protected set; }
    public string? FailureReason { get; protected set; }

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

