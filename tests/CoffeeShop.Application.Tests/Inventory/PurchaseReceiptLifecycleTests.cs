namespace CoffeeShop.Application.Tests.Inventory;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

public class PurchaseReceiptLifecycleTests
{
    private static readonly UtcTimestamp Created = new(new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero));
    private static readonly UtcTimestamp Changed = new(Created.Value.AddHours(1));

    private static PurchaseReceipt CreateDraftReceipt() =>
        new(EntityId.New(), EntityId.New(), "PR-2026-001", Created, Created);

    private static Ingredient CreateIngredient(decimal availableQuantity = 10m) =>
        new(EntityId.New(), "Coffee beans", "g", Created, Created, availableQuantity, 2m);

    [Fact]
    public void CreateDraftReceipt_WithValidData_ReturnsSuccess()
    {
        var result = PurchaseReceiptLifecycle.CreateDraftReceipt(EntityId.New(), EntityId.New(), " PR-2026-002 ", Created);

        Assert.True(result.IsSuccess);
        Assert.Equal("PR-2026-002", result.Value.ReceiptNumber);
        Assert.Equal(PurchaseReceiptStatus.Draft, result.Value.Status);
    }

    [Fact]
    public void CreateDraftReceipt_WithInvalidNumber_ReturnsValidationError()
    {
        var result = PurchaseReceiptLifecycle.CreateDraftReceipt(EntityId.New(), EntityId.New(), "  ", Created);

        AssertValidation(result, "receiptNumber");
    }

    [Fact]
    public void AddLine_WithValidData_ReturnsSuccessAndAddsLine()
    {
        var receipt = CreateDraftReceipt();

        var result = PurchaseReceiptLifecycle.AddLine(receipt, EntityId.New(), EntityId.New(), 5m, new Money(30m), Changed);

        Assert.True(result.IsSuccess);
        Assert.Single(receipt.Lines);
    }

    [Fact]
    public void AddLine_WithInvalidQuantity_ReturnsValidationError()
    {
        var receipt = CreateDraftReceipt();

        var result = PurchaseReceiptLifecycle.AddLine(receipt, EntityId.New(), EntityId.New(), 0m, new Money(30m), Changed);

        AssertValidation(result, "quantity");
        Assert.Empty(receipt.Lines);
    }

    [Fact]
    public void RemoveLine_WithExistingLine_ReturnsSuccess()
    {
        var receipt = CreateDraftReceipt();
        var line = PurchaseReceiptLifecycle.AddLine(receipt, EntityId.New(), EntityId.New(), 5m, new Money(30m), Changed).Value;

        var result = PurchaseReceiptLifecycle.RemoveLine(receipt, line.Id, Changed);

        Assert.True(result.IsSuccess);
        Assert.Empty(receipt.Lines);
    }

    [Fact]
    public void CancelReceipt_OnDraft_ReturnsSuccess()
    {
        var receipt = CreateDraftReceipt();

        var result = PurchaseReceiptLifecycle.CancelReceipt(receipt, Changed);

        Assert.True(result.IsSuccess);
        Assert.Equal(PurchaseReceiptStatus.Cancelled, receipt.Status);
    }

    [Fact]
    public void CancelReceipt_OnPosted_ReturnsConflict()
    {
        var receipt = CreateDraftReceipt();
        PurchaseReceiptLifecycle.AddLine(receipt, EntityId.New(), EntityId.New(), 5m, new Money(30m), Changed);
        PurchaseReceiptLifecycle.PostReceipt(receipt, _ => CreateIngredient(), Changed);

        var result = PurchaseReceiptLifecycle.CancelReceipt(receipt, Changed);

        AssertConflict(result);
        Assert.Equal(PurchaseReceiptStatus.Posted, receipt.Status);
    }

    [Fact]
    public void PostReceipt_WithValidLines_PostsAndReceivesStock()
    {
        var receipt = CreateDraftReceipt();
        var ingredient = CreateIngredient(availableQuantity: 10m);
        PurchaseReceiptLifecycle.AddLine(receipt, EntityId.New(), ingredient.Id, 5m, new Money(30m), Changed);
        PurchaseReceiptLifecycle.AddLine(receipt, EntityId.New(), ingredient.Id, 3m, new Money(28m), Changed);

        var result = PurchaseReceiptLifecycle.PostReceipt(receipt, id => id == ingredient.Id ? ingredient : null, Changed);

        Assert.True(result.IsSuccess);
        Assert.Equal(PurchaseReceiptStatus.Posted, receipt.Status);
        Assert.Equal(Changed, receipt.PostedAt);
        Assert.Equal(18m, ingredient.AvailableQuantity);
        Assert.Contains(ingredient, result.Value);
    }

    [Fact]
    public void PostReceipt_WithUnknownIngredient_ReturnsNotFoundWithoutMutation()
    {
        var receipt = CreateDraftReceipt();
        var ingredient = CreateIngredient();
        PurchaseReceiptLifecycle.AddLine(receipt, EntityId.New(), EntityId.New(), 5m, new Money(30m), Changed);

        var result = PurchaseReceiptLifecycle.PostReceipt(receipt, _ => null, Changed);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("inventory.ingredient_not_found", result.Error.Code);
        Assert.Equal(PurchaseReceiptStatus.Draft, receipt.Status);
        Assert.Null(receipt.PostedAt);
        Assert.Equal(10m, ingredient.AvailableQuantity);
    }

    [Fact]
    public void PostReceipt_OnEmptyReceipt_ReturnsConflictWithoutMutation()
    {
        var receipt = CreateDraftReceipt();

        var result = PurchaseReceiptLifecycle.PostReceipt(receipt, _ => CreateIngredient(), Changed);

        AssertConflict(result);
        Assert.Equal(PurchaseReceiptStatus.Draft, receipt.Status);
    }

    [Fact]
    public void PostReceipt_OnAlreadyPostedReceipt_ReturnsConflict()
    {
        var receipt = CreateDraftReceipt();
        var ingredient = CreateIngredient();
        PurchaseReceiptLifecycle.AddLine(receipt, EntityId.New(), ingredient.Id, 5m, new Money(30m), Changed);
        PurchaseReceiptLifecycle.PostReceipt(receipt, _ => ingredient, Changed);

        var result = PurchaseReceiptLifecycle.PostReceipt(receipt, _ => ingredient, Changed);

        AssertConflict(result);
        Assert.Equal(15m, ingredient.AvailableQuantity);
    }

    private static void AssertValidation(Result result, string target)
    {
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("inventory.invalid_input", result.Error.Code);
        Assert.Equal(target, Assert.Single(result.Error.Details).Target);
    }

    private static void AssertConflict(Result result)
    {
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("inventory.invalid_state", result.Error.Code);
    }
}
