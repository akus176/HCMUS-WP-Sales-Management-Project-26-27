namespace CoffeeShop.Application.Catalog;

using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;

public sealed class RecipeCommandHandler
{
    private readonly ICatalogRepository repository;
    private readonly IUnitOfWork unitOfWork;
    private readonly ITransactionManager transactionManager;

    public RecipeCommandHandler(
        ICatalogRepository repository,
        IUnitOfWork unitOfWork,
        ITransactionManager transactionManager)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        this.transactionManager = transactionManager ?? throw new ArgumentNullException(nameof(transactionManager));
    }

    public async Task<Result> ReplaceRecipeAsync(
        ReplaceRecipeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Lines is null || command.Lines.Count == 0)
        {
            return Result.Failure(Error.Validation(
                "catalog.recipe_lines_required",
                "A recipe requires at least one ingredient line.",
                [new ErrorDetail("lines", "Add at least one ingredient line.")]));
        }

        var variant = await repository.FindSizeVariantAsync(command.SizeVariantId, cancellationToken);
        if (variant is null)
        {
            return Result.Failure(Error.NotFound(
                "catalog.variant_not_found",
                "The selected size variant was not found."));
        }

        var errors = new List<ErrorDetail>();
        var recipe = new List<RecipeIngredient>();
        var ingredientIds = new HashSet<EntityId>();

        var index = 0;
        foreach (var input in command.Lines)
        {
            var target = $"lines[{index}]";
            if (input is null)
            {
                errors.Add(new ErrorDetail(target, "Recipe line cannot be null."));
                index++;
                continue;
            }

            if (!ingredientIds.Add(input.IngredientId))
            {
                errors.Add(new ErrorDetail(
                    $"{target}.ingredientId",
                    "An ingredient can appear only once in a size-specific recipe."));
                index++;
                continue;
            }

            var ingredient = await repository.FindIngredientAsync(input.IngredientId, cancellationToken);
            if (ingredient is null)
            {
                errors.Add(new ErrorDetail(
                    $"{target}.ingredientId",
                    $"Ingredient '{input.IngredientId}' was not found."));
                index++;
                continue;
            }

            try
            {
                recipe.Add(new RecipeIngredient(
                    EntityId.New(),
                    variant,
                    ingredient,
                    input.Quantity,
                    input.Unit));
            }
            catch (ArgumentException exception)
            {
                var field = exception.ParamName switch
                {
                    "ingredient" => "ingredientId",
                    "quantity" => "quantity",
                    "unit" => "unit",
                    _ => "line"
                };
                errors.Add(new ErrorDetail($"{target}.{field}", exception.Message));
            }

            index++;
        }

        if (errors.Count > 0)
        {
            return Result.Failure(Error.Validation(
                "catalog.recipe_invalid",
                "The recipe contains one or more invalid ingredient lines.",
                errors));
        }

        await transactionManager.ExecuteInTransactionAsync(async () =>
        {
            repository.ReplaceRecipe(variant.Id, recipe);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        return Result.Success();
    }
}
