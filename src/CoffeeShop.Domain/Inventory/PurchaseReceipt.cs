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

    public static PurchaseReceipt Reconstitute(EntityId id, EntityId supplierId, string receiptNumber,
        UtcTimestamp createdAt, UtcTimestamp updatedAt, UtcTimestamp? postedAt,
        PurchaseReceiptStatus status, IEnumerable<PurchaseReceiptLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var receipt = new PurchaseReceipt(id, supplierId, receiptNumber, createdAt, updatedAt, postedAt, status);
        receipt._lines.AddRange(lines);
        return receipt;
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
    /// Posts the receipt, transitioning it from draft to posted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Receipt state machine (authoritative):</b>
    /// <list type="bullet">
    /// <item><description><b>Draft → Posted</b> via <see cref="Post"/>: requires at least one line; stamps <see cref="PostedAt"/>.</description></item>
    /// <item><description><b>Draft → Cancelled</b> via <see cref="Cancel"/>.</description></item>
    /// <item><description><b>Posted</b> and <b>Cancelled</b> are terminal states — no further transitions exist.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// A posted receipt is an <b>immutable financial record</b>: it documents
    /// what was actually received and at what cost. Corrections happen through
    /// new receipts or stock adjustments, never by editing history. Cancelling
    /// a posted receipt is therefore not allowed.
    /// </para>
    /// </remarks>
    public void Post(UtcTimestamp postedAt)
    {
        ArgumentNullException.ThrowIfNull(postedAt);
        EnsureDraft(nameof(Post));

        if (_lines.Count == 0)
        {
            throw new InvalidOperationException("A receipt must contain at least one line before it can be posted.");
        }

        Status = PurchaseReceiptStatus.Posted;
        PostedAt = postedAt;
        UpdatedAt = postedAt;
    }

    /// <summary>
    /// Cancels the receipt, transitioning it from draft to cancelled.
    /// Posted receipts cannot be cancelled — they are immutable financial
    /// records; see <see cref="Post"/>.
    /// </summary>
    public void Cancel(UtcTimestamp cancelledAt)
    {
        ArgumentNullException.ThrowIfNull(cancelledAt);
        EnsureDraft(nameof(Cancel));

        Status = PurchaseReceiptStatus.Cancelled;
        UpdatedAt = cancelledAt;
    }
}
