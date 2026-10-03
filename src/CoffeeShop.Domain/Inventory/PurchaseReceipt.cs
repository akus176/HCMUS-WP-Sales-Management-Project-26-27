namespace CoffeeShop.Domain.Inventory;

using CoffeeShop.Domain.Common;

public class PurchaseReceipt
{
    private readonly List<PurchaseReceiptLine> _lines = [];

    // Parameterless constructor for EF Core
    protected PurchaseReceipt()
    {
    }

    public PurchaseReceipt(
        EntityId id,
        EntityId supplierId,
        string receiptNumber,
        UtcTimestamp createdAt,
        UtcTimestamp updatedAt,
        UtcTimestamp? postedAt = null,
        PurchaseReceiptStatus status = PurchaseReceiptStatus.Draft)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(supplierId);
        ArgumentException.ThrowIfNullOrWhiteSpace(receiptNumber);
        ArgumentNullException.ThrowIfNull(createdAt);
        ArgumentNullException.ThrowIfNull(updatedAt);

        if (status == PurchaseReceiptStatus.Posted && postedAt is null)
        {
            throw new ArgumentException("A posted receipt must record the posting timestamp.", nameof(postedAt));
        }

        Id = id;
        SupplierId = supplierId;
        ReceiptNumber = receiptNumber.Trim();
        Status = status;
        PostedAt = postedAt;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public EntityId Id { get; protected set; } = null!;

    public EntityId SupplierId { get; protected set; } = null!;

    public string ReceiptNumber { get; protected set; } = string.Empty;

    public PurchaseReceiptStatus Status { get; protected set; } = PurchaseReceiptStatus.Draft;

    public UtcTimestamp? PostedAt { get; protected set; }

    public UtcTimestamp CreatedAt { get; protected set; } = null!;

    public UtcTimestamp UpdatedAt { get; protected set; } = null!;

    public IReadOnlyCollection<PurchaseReceiptLine> Lines => _lines.AsReadOnly();
}
