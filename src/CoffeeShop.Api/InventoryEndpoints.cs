namespace CoffeeShop.Api;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Contracts;
using CoffeeShop.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Npgsql;

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/ingredients", async (IInventoryRepository repository, string? search, bool? isActive,
            int? page, int? pageSize, CancellationToken ct) =>
        {
            var ingredients = await repository.ListIngredientsAsync(ct);
            var result = ingredients.Where(ingredient => isActive is null || ingredient.IsActive == isActive)
                .Where(ingredient => string.IsNullOrWhiteSpace(search) || ingredient.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderBy(ingredient => ingredient.Name).ThenBy(ingredient => ingredient.Id.Value)
                .Select(ToBalance).ToArray();
            return MobilePage.From(result, page, pageSize);
        }).RequireAuthorization("inventory.manage").Produces<PageResponse<IngredientBalanceResponse>>()
            .ProducesProblem(400).ProducesProblem(403);

        api.MapGet("/ingredients/low-stock", async (IInventoryRepository repository, string? search,
            int? page, int? pageSize, CancellationToken ct) =>
        {
            var ingredients = await repository.ListIngredientsAsync(ct);
            var result = ingredients.Where(ingredient => ingredient.IsActive && ingredient.AvailableQuantity <= ingredient.ReorderThreshold)
                .Where(ingredient => string.IsNullOrWhiteSpace(search) || ingredient.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderBy(ingredient => ingredient.Name).ThenBy(ingredient => ingredient.Id.Value)
                .Select(ToBalance).ToArray();
            return MobilePage.From(result, page, pageSize);
        }).RequireAuthorization("reports.lowstock").Produces<PageResponse<IngredientBalanceResponse>>()
            .ProducesProblem(400).ProducesProblem(403);

        api.MapPost("/ingredients", async (CreateIngredientRequest request, InventoryCommandHandler handler,
            IInventoryRepository repository, CancellationToken ct) =>
        {
            var result = await handler.CreateIngredientAsync(new(request.Name ?? string.Empty, request.BaseUnit ?? string.Empty,
                request.AvailableQuantity, request.ReorderThreshold), ct);
            if (result.IsFailure) return ApiErrorResults.From(result.Error!);
            var ingredient = await repository.FindIngredientAsync(result.Value, ct);
            return Results.Json(ToBalance(ingredient!), statusCode: StatusCodes.Status201Created);
        }).RequireAuthorization("inventory.manage").Produces<IngredientBalanceResponse>(201).ProducesProblem(400);

        api.MapGet("/ingredients/{id:guid}/movements", async (Guid id, IStockMovementReader reader, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return ApiErrorResults.From(Error.Validation("inventory.invalid_id", "Ingredient identifier is required."));
            var movements = await reader.ListRecentAsync(new(id), ct);
            return Results.Ok(movements.Select(movement => new StockMovementResponse(movement.Id.Value,
                movement.IngredientId.Value, movement.Type.ToString().ToLowerInvariant(), movement.QuantityDelta,
                movement.ActorId.Value, movement.Reason, movement.OccurredAt.Value,
                movement.PurchaseReceiptId?.Value, movement.OrderId?.Value)).ToArray());
        }).RequireAuthorization("inventory.manage").Produces<StockMovementResponse[]>();

        api.MapPost("/ingredients/{id:guid}/adjustments", async (Guid id, StockAdjustmentRequest request,
            InventoryCommandHandler handler, IInventoryRepository repository, ICurrentUser user, CancellationToken ct) =>
        {
            if (id == Guid.Empty || request.RequestId == Guid.Empty)
                return ApiErrorResults.From(Error.Validation("inventory.invalid_id", "Ingredient and request identifiers are required."));
            var result = await handler.RecordStockAdjustmentAsync(new(new(id), request.QuantityDelta,
                request.Reason ?? string.Empty, user.GetRequiredId(), new(request.RequestId)), ct);
            if (result.IsFailure) return ApiErrorResults.From(result.Error!);
            var ingredient = await repository.FindIngredientAsync(new(id), ct);
            return Results.Ok(new StockAdjustmentResponse(id, ingredient!.AvailableQuantity));
        }).RequireAuthorization("inventory.adjust").Produces<StockAdjustmentResponse>()
            .ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        api.MapGet("/ingredients/{id:guid}", async (Guid id, IInventoryRepository repository, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var ingredient = await repository.FindIngredientAsync(new(id), ct);
            return ingredient is null ? ApiErrorResults.From(Error.NotFound("inventory.ingredient_not_found", "Ingredient not found."))
                : Results.Ok(ToBalance(ingredient));
        }).RequireAuthorization("inventory.manage").Produces<IngredientBalanceResponse>().ProducesProblem(404);

        api.MapPost("/ingredients/{id:guid}/threshold", async (Guid id, SetReorderThresholdRequest request,
            InventoryCommandHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var result = await handler.SetReorderThresholdAsync(new(new(id), request.ReorderThreshold), ct);
            return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("inventory.manage").Produces(204).ProducesProblem(400).ProducesProblem(404);

        api.MapPost("/ingredients/{id:guid}/deactivate", async (Guid id, InventoryCommandHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var result = await handler.DeactivateIngredientAsync(new(new(id)), ct);
            return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("inventory.manage").Produces(204).ProducesProblem(404).ProducesProblem(409);

        api.MapPost("/ingredients/{id:guid}/activate", async (Guid id, InventoryCommandHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var result = await handler.ActivateIngredientAsync(new(new(id)), ct);
            return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("inventory.manage").Produces(204).ProducesProblem(404).ProducesProblem(409);

        var suppliers = api.MapGroup("/suppliers").RequireAuthorization("inventory.manage");
        suppliers.MapGet("/", async (IInventoryRepository repository, CancellationToken ct) =>
            Results.Ok((await repository.ListSuppliersAsync(ct)).OrderBy(supplier => supplier.Name)
                .Select(ToSupplier).ToArray())).Produces<SupplierResponse[]>();
        suppliers.MapPost("/", async (SupplierRequest request, InventoryCommandHandler handler,
            IInventoryRepository repository, CancellationToken ct) =>
        {
            var result = await handler.CreateSupplierAsync(new(request.Name ?? string.Empty, request.Phone ?? string.Empty,
                request.Email, request.Address), ct);
            if (result.IsFailure) return ApiErrorResults.From(result.Error!);
            return Results.Json(ToSupplier((await repository.FindSupplierAsync(result.Value, ct))!), statusCode: 201);
        }).Produces<SupplierResponse>(201).ProducesProblem(400);
        suppliers.MapPut("/{id:guid}", async (Guid id, SupplierRequest request, InventoryCommandHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var result = await handler.UpdateSupplierAsync(new(new(id), request.Name ?? string.Empty,
                request.Phone ?? string.Empty, request.Email, request.Address), ct);
            return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
        }).Produces(204).ProducesProblem(400).ProducesProblem(404);
        suppliers.MapPost("/{id:guid}/deactivate", async (Guid id, InventoryCommandHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var result = await handler.DeactivateSupplierAsync(new(new(id)), ct);
            return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
        }).Produces(204).ProducesProblem(404).ProducesProblem(409);
        suppliers.MapPost("/{id:guid}/activate", async (Guid id, InventoryCommandHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var result = await handler.ActivateSupplierAsync(new(new(id)), ct);
            return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
        }).Produces(204).ProducesProblem(404).ProducesProblem(409);

        var receipts = api.MapGroup("/purchase-receipts").RequireAuthorization("inventory.receive");
        receipts.MapGet("/", async (IInventoryRepository repository, CancellationToken ct) =>
            Results.Ok((await repository.ListReceiptsAsync(ct)).OrderByDescending(receipt => receipt.CreatedAt.Value)
                .Select(ToReceipt).ToArray())).Produces<ReceiptResponse[]>();
        receipts.MapGet("/{id:guid}", async (Guid id, IInventoryRepository repository, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var receipt = await repository.FindReceiptWithLinesAsync(new(id), ct);
            return receipt is null ? ApiErrorResults.From(Error.NotFound("inventory.receipt_not_found", "Receipt not found."))
                : Results.Ok(ToReceipt(receipt));
        }).Produces<ReceiptResponse>().ProducesProblem(404);
        receipts.MapPost("/", async (CreateReceiptRequest request, InventoryCommandHandler handler,
            IInventoryRepository repository, CancellationToken ct) =>
        {
            if (request.SupplierId == Guid.Empty) return InvalidId();
            var result = await handler.CreateDraftReceiptAsync(new(new(request.SupplierId), request.ReceiptNumber ?? string.Empty), ct);
            if (result.IsFailure) return ApiErrorResults.From(result.Error!);
            return Results.Json(ToReceipt((await repository.FindReceiptWithLinesAsync(result.Value, ct))!), statusCode: 201);
        }).Produces<ReceiptResponse>(201).ProducesProblem(400).ProducesProblem(404);
        receipts.MapPost("/receive", async (ReceiveDeliveryRequest request, InventoryCommandHandler handler,
            ICurrentUser user, CancellationToken ct) =>
        {
            if (request.SupplierId == Guid.Empty || request.Lines?.Any(line => line is null || line.IngredientId == Guid.Empty) == true)
                return InvalidId();

            var lines = request.Lines?.Select(line => new ReceiveDeliveryLine(new(line.IngredientId), line.Quantity, line.UnitCost)).ToArray() ?? [];
            try
            {
                var result = await handler.ReceiveDeliveryAsync(new(
                    new(request.SupplierId), request.ReceiptNumber ?? string.Empty, lines, user.GetRequiredId()), ct);
                return result.IsSuccess ? Results.Ok(ToReceipt(result.Value)) : ApiErrorResults.From(result.Error!);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_purchase_receipts_receipt_number" })
            {
                return ApiErrorResults.From(Error.Conflict("inventory.receipt_number_in_use",
                    "This receipt number is already in use. Check Receipts or enter a different number."));
            }
        }).Produces<ReceiptResponse>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        receipts.MapPost("/{id:guid}/lines", async (Guid id, ReceiptLineRequest request, InventoryCommandHandler handler,
            IInventoryRepository repository, CancellationToken ct) =>
        {
            if (id == Guid.Empty || request.IngredientId == Guid.Empty) return InvalidId();
            if (request.UnitCost < 0) return ApiErrorResults.From(Error.Validation("inventory.invalid_unit_cost", "Unit cost cannot be negative."));
            var result = await handler.AddReceiptLineAsync(new(new(id), new(request.IngredientId), request.Quantity, request.UnitCost), ct);
            if (result.IsFailure) return ApiErrorResults.From(result.Error!);
            return Results.Ok(ToReceipt((await repository.FindReceiptWithLinesAsync(new(id), ct))!));
        }).Produces<ReceiptResponse>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        receipts.MapDelete("/{id:guid}/lines/{lineId:guid}", async (Guid id, Guid lineId,
            InventoryCommandHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty || lineId == Guid.Empty) return InvalidId();
            var result = await handler.RemoveReceiptLineAsync(new(new(id), new(lineId)), ct);
            return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
        }).Produces(204).ProducesProblem(404).ProducesProblem(409);
        receipts.MapPost("/{id:guid}/post", async (Guid id, InventoryCommandHandler handler, ICurrentUser user, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var result = await handler.PostReceiptAsync(new(new(id), user.GetRequiredId()), ct);
            return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
        }).Produces(204).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);
        receipts.MapPost("/{id:guid}/cancel", async (Guid id, InventoryCommandHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var result = await handler.CancelReceiptAsync(new(new(id)), ct);
            return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
        }).Produces(204).ProducesProblem(404).ProducesProblem(409);

        api.MapGet("/stock-movements", async (IStockMovementReader reader, CancellationToken ct) =>
            Results.Ok((await reader.ListRecentForAllAsync(cancellationToken: ct))
                .Select(movement => new StockMovementResponse(movement.Id.Value, movement.IngredientId.Value,
                    movement.Type.ToString().ToLowerInvariant(), movement.QuantityDelta, movement.ActorId.Value,
                    movement.Reason, movement.OccurredAt.Value, movement.PurchaseReceiptId?.Value,
                    movement.OrderId?.Value)).ToArray()))
            .RequireAuthorization("inventory.manage").Produces<StockMovementResponse[]>();
    }

    private static IngredientBalanceResponse ToBalance(Ingredient ingredient) => new(ingredient.Id.Value,
        ingredient.Name, ingredient.BaseUnit, ingredient.AvailableQuantity, ingredient.ReorderThreshold, ingredient.IsActive);

    private static SupplierResponse ToSupplier(Supplier supplier) => new(supplier.Id.Value, supplier.Name,
        supplier.Phone, supplier.Email, supplier.Address, supplier.IsActive, supplier.CreatedAt.Value, supplier.UpdatedAt.Value);

    private static ReceiptResponse ToReceipt(PurchaseReceipt receipt) => new(receipt.Id.Value, receipt.SupplierId.Value,
        receipt.ReceiptNumber, receipt.Status.ToString().ToLowerInvariant(), receipt.PostedAt?.Value,
        receipt.Lines.Select(line => new ReceiptLineResponse(line.Id.Value, line.IngredientId.Value,
            line.Quantity, line.UnitCost.Amount)).ToArray(), receipt.CreatedAt.Value, receipt.UpdatedAt.Value);

    private static IResult InvalidId() => ApiErrorResults.From(Error.Validation("inventory.invalid_id", "A nonempty identifier is required."));
}
