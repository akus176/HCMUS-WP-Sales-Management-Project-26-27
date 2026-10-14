namespace CoffeeShop.Application.Sales;

using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.Domain.Sales;

/// <summary>
/// Confirms an order as one database transaction: validates menu availability
/// and recipes, checks and deducts every required ingredient, records immutable
/// consumption movements, occupies the dine-in table, and transitions the order
/// to confirmed. Any shortage rolls the whole operation back.
/// </summary>
public sealed class OrderConfirmationCommandHandler
{
    private readonly IOrderConfirmationRepository repository;
    private readonly IUnitOfWork unitOfWork;
    private readonly ITransactionManager transactionManager;
    private readonly IClock clock;

    public OrderConfirmationCommandHandler(
        IOrderConfirmationRepository repository,
        IUnitOfWork unitOfWork,
        ITransactionManager transactionManager,
        IClock clock)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        this.transactionManager = transactionManager ?? throw new ArgumentNullException(nameof(transactionManager));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<OrderConfirmationResult>> ConfirmAsync(
        ConfirmOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return await transactionManager.ExecuteInTransactionAsync(async () =>
        {
            var order = await repository.LoadOrderForConfirmationAsync(command.OrderId, cancellationToken);
            if (order is null)
            {
                return Result<OrderConfirmationResult>.Failure(Error.NotFound(
                    "orders.order_not_found", $"Order '{command.OrderId}' was not found."));
            }

            if (order.Status == OrderStatus.Confirmed)
            {
                // Safe retry: the confirmation already applied, return the same result.
                return Result<OrderConfirmationResult>.Success(new(order.Id, order.Status, order.ConfirmedAt!));
            }

            if (order.Status != OrderStatus.Draft)
            {
                return Result<OrderConfirmationResult>.Failure(Error.Conflict(
                    "orders.invalid_state", $"Order '{order.OrderNumber}' is {order.Status} and cannot be confirmed."));
            }

            var catalog = await LoadCatalogAsync(order, cancellationToken);
            if (catalog.IsFailure)
            {
                return Result<OrderConfirmationResult>.Failure(catalog.Error!);
            }

            if (order.Channel == "dine_in" && order.TableId is not null)
            {
                var tableError = await OccupyTableAsync(order.TableId, order.Id, cancellationToken);
                if (tableError is not null)
                {
                    return Result<OrderConfirmationResult>.Failure(tableError);
                }
            }

            var requirements = await ResolveIngredientRequirementsAsync(order, cancellationToken);
            if (requirements.IsFailure)
            {
                return Result<OrderConfirmationResult>.Failure(requirements.Error!);
            }

            var consumptionError = await ConsumeIngredientsAsync(order.Id, requirements.Value!, command.ActorId, cancellationToken);
            if (consumptionError is not null)
            {
                return Result<OrderConfirmationResult>.Failure(consumptionError);
            }

            try
            {
                order.TransitionTo(OrderStatus.Confirmed, command.ActorId, clock.UtcNow);
            }
            catch (InvalidOperationException exception)
            {
                return Result<OrderConfirmationResult>.Failure(Error.Conflict(
                    "orders.invalid_transition", exception.Message));
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<OrderConfirmationResult>.Success(new(order.Id, order.Status, order.ConfirmedAt!));
        }, cancellationToken);
    }

    private async Task<Result<OrderCatalogSnapshot>> LoadCatalogAsync(Order order, CancellationToken cancellationToken)
    {
        var lines = order.Lines.ToArray();
        var menuItemIds = lines.Where(line => line.MenuItemId is not null)
            .Select(line => line.MenuItemId!).Distinct().ToArray();
        var variantIds = lines.Where(line => line.SizeVariantId is not null)
            .Select(line => line.SizeVariantId!).Distinct().ToArray();
        var addonIds = lines.SelectMany(line => line.Addons)
            .Where(addon => addon.AddonId is not null)
            .Select(addon => addon.AddonId!).Distinct().ToArray();

        var snapshot = await repository.LoadCatalogForConfirmationAsync(menuItemIds, variantIds, addonIds, cancellationToken);

        var errors = new List<ErrorDetail>();
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var target = $"lines[{index}]";
            if (line.MenuItemId is null || line.SizeVariantId is null)
            {
                errors.Add(new ErrorDetail(target, "The order line does not reference a menu item or size variant."));
                continue;
            }

            if (!snapshot.MenuItems.TryGetValue(line.MenuItemId, out var menuItem))
            {
                errors.Add(new ErrorDetail($"{target}.menuItemId", $"Menu item '{line.MenuItemId}' was not found."));
                continue;
            }

            if (!menuItem.IsActive)
            {
                errors.Add(new ErrorDetail($"{target}.menuItemId", $"Menu item '{menuItem.Name}' is no longer available."));
                continue;
            }

            if (!snapshot.Categories.TryGetValue(menuItem.CategoryId, out var category) || !category.IsActive)
            {
                errors.Add(new ErrorDetail($"{target}.menuItemId", $"The category of menu item '{menuItem.Name}' is no longer available."));
                continue;
            }

            if (!snapshot.SizeVariants.TryGetValue(line.SizeVariantId, out var variant)
                || !variant.IsAvailableForOrdering(menuItem))
            {
                errors.Add(new ErrorDetail($"{target}.sizeVariantId", $"Size '{line.SizeNameSnapshot}' is no longer available for '{menuItem.Name}'."));
                continue;
            }

            foreach (var addon in line.Addons)
            {
                if (addon.AddonId is null || !snapshot.Addons.TryGetValue(addon.AddonId, out var addonDefinition)
                    || !addonDefinition.IsAvailableForOrdering(menuItem,
                        snapshot.CompatibilityLinks.Where(link => link.MenuItemId == menuItem.Id).ToList()))
                {
                    errors.Add(new ErrorDetail(
                        $"{target}.addons",
                        $"Add-on '{addon.AddonNameSnapshot}' is no longer available for '{menuItem.Name}'."));
                }
            }
        }

        return errors.Count > 0
            ? Result<OrderCatalogSnapshot>.Failure(Error.Validation(
                "orders.catalog_unavailable", "The order references unavailable menu items, sizes, or add-ons.", errors))
            : Result<OrderCatalogSnapshot>.Success(snapshot);
    }

    private async Task<Result<IReadOnlyList<RecipeRequirement>>> ResolveIngredientRequirementsAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        var lines = order.Lines.ToArray();
        var variantIds = lines.Where(line => line.SizeVariantId is not null)
            .Select(line => line.SizeVariantId!).Distinct().ToArray();
        var recipes = await repository.LoadRecipesAsync(variantIds, cancellationToken);

        var requirements = new List<RecipeRequirement>();
        foreach (var line in lines)
        {
            if (line.SizeVariantId is null)
            {
                continue;
            }

            // Add-ons have no ingredient recipes in the current catalog contract;
            // only size-specific recipes drive ingredient consumption.
            foreach (var recipeLine in recipes.Where(recipe => recipe.SizeVariantId == line.SizeVariantId))
            {
                requirements.Add(new RecipeRequirement(recipeLine.IngredientId, recipeLine.Quantity * line.Quantity, recipeLine.Unit));
            }
        }

        return Result<IReadOnlyList<RecipeRequirement>>.Success(requirements);
    }

    private async Task<Error?> ConsumeIngredientsAsync(
        EntityId orderId,
        IReadOnlyList<RecipeRequirement> requirements,
        EntityId actorId,
        CancellationToken cancellationToken)
    {
        if (requirements.Count == 0)
        {
            return null;
        }

        var ingredients = await repository.LoadIngredientsForConsumptionAsync(
            requirements.Select(requirement => requirement.IngredientId).Distinct().ToArray(), cancellationToken);
        var byId = ingredients.ToDictionary(ingredient => ingredient.Id);

        var shortages = new List<ErrorDetail>();
        var consumptions = new List<(EntityId IngredientId, decimal BaseQuantity)>();
        foreach (var group in requirements.GroupBy(requirement => requirement.IngredientId))
        {
            var ingredient = byId.GetValueOrDefault(group.Key);
            if (ingredient is null || !ingredient.IsActive)
            {
                shortages.Add(new ErrorDetail($"ingredients[{group.Key}]", "The ingredient is missing or inactive."));
                continue;
            }

            decimal baseTotal = 0;
            var convertible = true;
            foreach (var requirement in group)
            {
                var baseQuantity = ingredient.ConvertToBaseUnit(requirement.Quantity, requirement.Unit);
                if (baseQuantity is null)
                {
                    convertible = false;
                    break;
                }

                baseTotal += baseQuantity.Value;
            }

            if (!convertible)
            {
                shortages.Add(new ErrorDetail(
                    $"ingredients[{ingredient.Name}]",
                    $"Ingredient '{ingredient.Name}' has a recipe unit incompatible with its base unit."));
                continue;
            }

            if (baseTotal > ingredient.AvailableQuantity)
            {
                shortages.Add(new ErrorDetail(
                    $"ingredients[{ingredient.Name}]",
                    $"Ingredient '{ingredient.Name}' needs {baseTotal} {ingredient.BaseUnit} but only {ingredient.AvailableQuantity} {ingredient.BaseUnit} is available."));
                continue;
            }

            consumptions.Add((ingredient.Id, baseTotal));
        }

        if (shortages.Count > 0)
        {
            return Error.BusinessRule(
                "orders.insufficient_stock",
                "The order cannot be confirmed because one or more ingredients are insufficient.",
                shortages);
        }

        foreach (var (ingredientId, baseQuantity) in consumptions)
        {
            byId[ingredientId].Consume(baseQuantity, clock.UtcNow);
            repository.AddStockMovement(new StockMovement(
                EntityId.New(),
                ingredientId,
                StockMovementType.OrderConsumption,
                -baseQuantity,
                actorId,
                "Order confirmation consumption",
                clock.UtcNow,
                orderId: orderId));
        }

        return null;
    }

    private async Task<Error?> OccupyTableAsync(EntityId tableId, EntityId orderId, CancellationToken cancellationToken)
    {
        var table = await repository.LoadTableForConfirmationAsync(tableId, cancellationToken);
        if (table is null)
        {
            return Error.NotFound("orders.table_not_found", $"Table '{tableId}' was not found.");
        }

        try
        {
            if (table.ActiveOrderId != orderId)
            {
                table.Occupy(orderId, partySize: 1);
            }

            return null;
        }
        catch (InvalidOperationException exception)
        {
            return Error.Conflict("orders.table_unavailable", exception.Message);
        }
    }

    private sealed record RecipeRequirement(EntityId IngredientId, decimal Quantity, string Unit);
}
