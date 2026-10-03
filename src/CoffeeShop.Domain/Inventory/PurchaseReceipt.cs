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

    /// <summary>
    /// Adds a line to the receipt. Only draft receipts can be modified;
    /// posted and cancelled receipts are immutable.
    /// Multiple lines for the same ingredient are allowed because separate
    /// deliveries of the same ingredient can arrive at different costs.
    /// </summary>
    public PurchaseReceiptLine AddLine(EntityId lineId, EntityId ingredientId, decimal quantity, Money unitCost, UtcTimestamp changedAt)
    {
        ArgumentNullException.ThrowIfNull(changedAt);
        EnsureDraft(nameof(AddLine));

        var line = PurchaseReceiptLine.Create(lineId, ingredientId, quantity, unitCost);
        _lines.Add(line);
        UpdatedAt = changedAt;
        return line;
    }

    /// <summary>
    /// Removes a line from the receipt. Only draft receipts can be modified.
    /// </summary>
    public void RemoveLine(EntityId lineId, UtcTimestamp changedAt)
    {
        ArgumentNullException.ThrowIfNull(lineId);
        ArgumentNullException.ThrowIfNull(changedAt);
        EnsureDraft(nameof(RemoveLine));

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
        {
            throw new InvalidOperationException("The receipt does not contain the specified line.");
        }

        _lines.Remove(line);
        UpdatedAt = changedAt;
    }

    /// <summary>
    /// Guards that the receipt is still in draft status.
    /// </summary>
    private void EnsureDraft(string operation)
    {
        if (Status != PurchaseReceiptStatus.Draft)
        {
            throw new InvalidOperationException($"Only draft receipts allow {operation}; the receipt is {Status}.");
        }
    }

    /// <summary>
    /// Posts the receipt, transitioning it from draft to posted. The full
    /// posting rules are covered in the posting step.
    /// </summary>
    public void Post(UtcTimestamp postedAt)
    {
        ArgumentNullException.ThrowIfNull(postedAt);
        EnsureDraft(nameof(Post));

        Status = PurchaseReceiptStatus.Posted;
        PostedAt = postedAt;
        UpdatedAt = postedAt;
    }

    /// <summary>
    /// Cancels the receipt, transitioning it from draft to cancelled.
    /// </summary>
    public void Cancel(UtcTimestamp cancelledAt)
    {
        ArgumentNullException.ThrowIfNull(cancelledAt);
        EnsureDraft(nameof(Cancel));

        Status = PurchaseReceiptStatus.Cancelled;
        UpdatedAt = cancelledAt;
    }
}
