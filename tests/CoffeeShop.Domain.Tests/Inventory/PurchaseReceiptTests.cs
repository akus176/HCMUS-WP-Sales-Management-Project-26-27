namespace CoffeeShop.Domain.Tests.Inventory;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

public class PurchaseReceiptTests
{
    private static readonly UtcTimestamp Created = new(new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero));
    private static readonly UtcTimestamp Changed = new(Created.Value.AddHours(1));

    private static PurchaseReceipt CreateDraftReceipt() =>
        new(EntityId.New(), EntityId.New(), "PR-2026-001", Created, Created);

    private static PurchaseReceiptLine AddLine(PurchaseReceipt receipt, decimal quantity = 10m, decimal unitCost = 25m) =>
        receipt.AddLine(EntityId.New(), EntityId.New(), quantity, new Money(unitCost), Changed);

    [Fact]
    public void Constructor_CreatesDraftByDefault()
    {
        var receipt = CreateDraftReceipt();

        Assert.Equal(PurchaseReceiptStatus.Draft, receipt.Status);
        Assert.Null(receipt.PostedAt);
        Assert.Empty(receipt.Lines);
    }

    [Fact]
    public void AddLine_AddsLineToDraftReceiptAndStampsUpdatedAt()
    {
        var receipt = CreateDraftReceipt();
        var ingredientId = EntityId.New();

        var line = receipt.AddLine(EntityId.New(), ingredientId, 5m, new Money(30m), Changed);

        Assert.Single(receipt.Lines);
        Assert.Equal(ingredientId, line.IngredientId);
        Assert.Equal(5m, line.Quantity);
        Assert.Equal(new Money(30m), line.UnitCost);
        Assert.Equal(Changed, receipt.UpdatedAt);
    }

    [Fact]
    public void AddLine_AllowsMultipleLinesForSameIngredient()
    {
        var receipt = CreateDraftReceipt();
        var ingredientId = EntityId.New();

        receipt.AddLine(EntityId.New(), ingredientId, 5m, new Money(30m), Changed);
        receipt.AddLine(EntityId.New(), ingredientId, 3m, new Money(28m), Changed);

        Assert.Equal(2, receipt.Lines.Count);
    }

    [Fact]
    public void AddLine_RejectsNonPositiveQuantity()
    {
        var receipt = CreateDraftReceipt();

        Assert.ThrowsAny<ArgumentException>(() => receipt.AddLine(EntityId.New(), EntityId.New(), 0m, new Money(30m), Changed));
        Assert.Empty(receipt.Lines);
    }

    [Fact]
    public void AddLine_RejectsExcessPrecisionQuantity()
    {
        var receipt = CreateDraftReceipt();

        Assert.ThrowsAny<ArgumentException>(() => receipt.AddLine(EntityId.New(), EntityId.New(), 1.0001m, new Money(30m), Changed));
        Assert.Empty(receipt.Lines);
    }

    [Fact]
    public void AddLine_RejectsNegativeUnitCost()
    {
        var receipt = CreateDraftReceipt();

        Assert.ThrowsAny<ArgumentException>(() => receipt.AddLine(EntityId.New(), EntityId.New(), 5m, new Money(-1m), Changed));
        Assert.Empty(receipt.Lines);
    }

    [Fact]
    public void AddLine_RejectsPostedReceipt()
    {
        var receipt = CreateDraftReceipt();
        AddLine(receipt);
        receipt.Post(Changed);

        Assert.Throws<InvalidOperationException>(() => receipt.AddLine(EntityId.New(), EntityId.New(), 5m, new Money(30m), Changed));
        Assert.Single(receipt.Lines);
    }

    [Fact]
    public void AddLine_RejectsCancelledReceipt()
    {
        var receipt = CreateDraftReceipt();
        receipt.Cancel(Changed);

        Assert.Throws<InvalidOperationException>(() => receipt.AddLine(EntityId.New(), EntityId.New(), 5m, new Money(30m), Changed));
        Assert.Empty(receipt.Lines);
    }

    [Fact]
    public void RemoveLine_RemovesExistingLineFromDraftReceipt()
    {
        var receipt = CreateDraftReceipt();
        var line = AddLine(receipt);

        receipt.RemoveLine(line.Id, Changed);

        Assert.Empty(receipt.Lines);
        Assert.Equal(Changed, receipt.UpdatedAt);
    }

    [Fact]
    public void RemoveLine_RejectsUnknownLineId()
    {
        var receipt = CreateDraftReceipt();
        AddLine(receipt);

        Assert.Throws<InvalidOperationException>(() => receipt.RemoveLine(EntityId.New(), Changed));
        Assert.Single(receipt.Lines);
    }

    [Fact]
    public void RemoveLine_RejectsPostedReceipt()
    {
        var receipt = CreateDraftReceipt();
        var line = AddLine(receipt);
        receipt.Post(Changed);

        Assert.Throws<InvalidOperationException>(() => receipt.RemoveLine(line.Id, Changed));
        Assert.Single(receipt.Lines);
    }

    [Fact]
    public void Post_TransitionsDraftToPostedAndStampsPostedAt()
    {
        var receipt = CreateDraftReceipt();
        AddLine(receipt);

        receipt.Post(Changed);

        Assert.Equal(PurchaseReceiptStatus.Posted, receipt.Status);
        Assert.Equal(Changed, receipt.PostedAt);
        Assert.Equal(Changed, receipt.UpdatedAt);
    }

    [Fact]
    public void Post_RejectsEmptyReceipt()
    {
        var receipt = CreateDraftReceipt();

        Assert.Throws<InvalidOperationException>(() => receipt.Post(Changed));
        Assert.Equal(PurchaseReceiptStatus.Draft, receipt.Status);
        Assert.Null(receipt.PostedAt);
    }

    [Fact]
    public void Post_RejectsAlreadyPostedReceipt()
    {
        var receipt = CreateDraftReceipt();
        AddLine(receipt);
        receipt.Post(Changed);

        Assert.Throws<InvalidOperationException>(() => receipt.Post(Changed));
        Assert.Equal(PurchaseReceiptStatus.Posted, receipt.Status);
        Assert.Equal(Changed, receipt.PostedAt);
    }

    [Fact]
    public void Post_RejectsCancelledReceipt()
    {
        var receipt = CreateDraftReceipt();
        AddLine(receipt);
        receipt.Cancel(Changed);

        Assert.Throws<InvalidOperationException>(() => receipt.Post(Changed));
        Assert.Equal(PurchaseReceiptStatus.Cancelled, receipt.Status);
        Assert.Null(receipt.PostedAt);
    }

    [Fact]
    public void Cancel_TransitionsDraftToCancelled()
    {
        var receipt = CreateDraftReceipt();
        AddLine(receipt);

        receipt.Cancel(Changed);

        Assert.Equal(PurchaseReceiptStatus.Cancelled, receipt.Status);
        Assert.Null(receipt.PostedAt);
        Assert.Equal(Changed, receipt.UpdatedAt);
    }

    [Fact]
    public void Cancel_RejectsPostedReceipt()
    {
        var receipt = CreateDraftReceipt();
        AddLine(receipt);
        receipt.Post(Changed);

        Assert.Throws<InvalidOperationException>(() => receipt.Cancel(Changed));
        Assert.Equal(PurchaseReceiptStatus.Posted, receipt.Status);
    }

    [Fact]
    public void Cancel_RejectsAlreadyCancelledReceipt()
    {
        var receipt = CreateDraftReceipt();
        receipt.Cancel(Changed);

        Assert.Throws<InvalidOperationException>(() => receipt.Cancel(Changed));
        Assert.Equal(PurchaseReceiptStatus.Cancelled, receipt.Status);
    }

    [Fact]
    public void PostedReceipt_PreservesLineDataAsImmutableRecord()
    {
        var receipt = CreateDraftReceipt();
        var line = AddLine(receipt, quantity: 7.5m, unitCost: 42m);
        receipt.Post(Changed);

        Assert.Single(receipt.Lines);
        Assert.Equal(7.5m, line.Quantity);
        Assert.Equal(new Money(42m), line.UnitCost);
        Assert.Equal(PurchaseReceiptStatus.Posted, receipt.Status);
    }
}
