namespace CoffeeShop.Application.Tests.Catalog;

using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Tests.Testing;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

public sealed class RecipeCommandHandlerTests
{
    private static readonly UtcTimestamp Now = new(
        new DateTimeOffset(2026, 10, 5, 2, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task ReplaceRecipe_WithValidLines_ReplacesRecipeInsideTransaction()
    {
        var context = CreateContext();
        var variant = AddVariant(context.Repository);
        var beans = AddIngredient(context.Repository, "Coffee beans", "g");
        var milk = AddIngredient(context.Repository, "Milk", "ml");

        var result = await context.Handler.ReplaceRecipeAsync(new ReplaceRecipeCommand(
            variant.Id,
            [new(beans.Id, 18m, "g"), new(milk.Id, 250m, "ml")]));

        Assert.True(result.IsSuccess);
        var recipe = context.Repository.Recipes[variant.Id];
        Assert.Equal(2, recipe.Count);
        Assert.Contains(recipe, line => line.IngredientId == beans.Id && line.Quantity == 18m);
        Assert.Contains(recipe, line => line.IngredientId == milk.Id && line.Quantity == 250m);
        Assert.Equal(1, context.TransactionManager.ExecutionCount);
        Assert.Equal(1, context.TransactionManager.CommittedCount);
        Assert.Equal(1, context.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ReplaceRecipe_AllowsEditingAnInactiveSizeVariant()
    {
        var context = CreateContext();
        var variant = AddVariant(context.Repository);
        variant.Deactivate();
        var ingredient = AddIngredient(context.Repository, "Coffee beans", "g");

        var result = await context.Handler.ReplaceRecipeAsync(new ReplaceRecipeCommand(
            variant.Id,
            [new(ingredient.Id, 18m, "g")]));

        Assert.True(result.IsSuccess);
        Assert.Single(context.Repository.Recipes[variant.Id]);
    }

    [Fact]
    public async Task ReplaceRecipe_WithInvalidLines_ReturnsAllValidationDetailsWithoutMutation()
    {
        var context = CreateContext();
        var variant = AddVariant(context.Repository);
        var inactive = AddIngredient(context.Repository, "Inactive", "g", isActive: false);
        var zeroQuantity = AddIngredient(context.Repository, "Zero", "g");
        var incompatible = AddIngredient(context.Repository, "Milk", "ml");

        var result = await context.Handler.ReplaceRecipeAsync(new ReplaceRecipeCommand(
            variant.Id,
            [
                new(EntityId.New(), 1m, "g"),
                new(inactive.Id, 1m, "g"),
                new(zeroQuantity.Id, 0m, "g"),
                new(incompatible.Id, 1m, "g")
            ]));

        AssertError(result, ErrorType.Validation, "catalog.recipe_invalid");
        Assert.Equal(4, result.Error!.Details.Count);
        Assert.Contains(result.Error.Details, detail => detail.Target == "lines[0].ingredientId");
        Assert.Contains(result.Error.Details, detail => detail.Target == "lines[1].ingredientId");
        Assert.Contains(result.Error.Details, detail => detail.Target == "lines[2].quantity");
        Assert.Contains(result.Error.Details, detail => detail.Target == "lines[3].unit");
        Assert.Empty(context.Repository.Recipes);
        Assert.Equal(1, context.TransactionManager.ExecutionCount);
        Assert.Equal(0, context.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ReplaceRecipe_WithDuplicateIngredient_ReturnsValidationWithoutMutation()
    {
        var context = CreateContext();
        var variant = AddVariant(context.Repository);
        var ingredient = AddIngredient(context.Repository, "Coffee beans", "g");

        var result = await context.Handler.ReplaceRecipeAsync(new ReplaceRecipeCommand(
            variant.Id,
            [new(ingredient.Id, 10m, "g"), new(ingredient.Id, 12m, "g")]));

        AssertError(result, ErrorType.Validation, "catalog.recipe_invalid");
        Assert.Contains(result.Error!.Details, detail => detail.Target == "lines[1].ingredientId");
        Assert.Empty(context.Repository.Recipes);
        Assert.Equal(1, context.TransactionManager.ExecutionCount);
    }

    [Fact]
    public async Task ReplaceRecipe_WithEmptyRecipe_ReturnsValidation()
    {
        var context = CreateContext();

        var result = await context.Handler.ReplaceRecipeAsync(new ReplaceRecipeCommand(EntityId.New(), []));

        AssertError(result, ErrorType.Validation, "catalog.recipe_lines_required");
    }

    [Fact]
    public async Task ReplaceRecipe_WithMissingVariant_ReturnsNotFound()
    {
        var context = CreateContext();
        var ingredient = AddIngredient(context.Repository, "Coffee beans", "g");

        var result = await context.Handler.ReplaceRecipeAsync(new ReplaceRecipeCommand(
            EntityId.New(),
            [new(ingredient.Id, 18m, "g")]));

        AssertError(result, ErrorType.NotFound, "catalog.variant_not_found");
        Assert.Equal(0, context.UnitOfWork.SaveChangesCallCount);
    }

    private static RecipeContext CreateContext()
    {
        var repository = new FakeCatalogRepository();
        var unitOfWork = new FakeUnitOfWork();
        var transactionManager = new TestTransactionManager();
        return new RecipeContext(
            new RecipeCommandHandler(repository, unitOfWork, transactionManager),
            repository,
            unitOfWork,
            transactionManager);
    }

    private static SizeVariant AddVariant(FakeCatalogRepository repository)
    {
        var variant = new SizeVariant(EntityId.New(), EntityId.New(), "Large", new Money(45000m));
        repository.SizeVariants.Add(variant.Id, variant);
        return variant;
    }

    private static Ingredient AddIngredient(
        FakeCatalogRepository repository,
        string name,
        string baseUnit,
        bool isActive = true)
    {
        var ingredient = new Ingredient(
            EntityId.New(),
            name,
            baseUnit,
            Now,
            Now,
            isActive: isActive);
        repository.Ingredients.Add(ingredient.Id, ingredient);
        return ingredient;
    }

    private static void AssertError(Result result, ErrorType type, string code)
    {
        Assert.True(result.IsFailure);
        Assert.NotNull(result.Error);
        Assert.Equal(type, result.Error.Type);
        Assert.Equal(code, result.Error.Code);
    }

    private sealed record RecipeContext(
        RecipeCommandHandler Handler,
        FakeCatalogRepository Repository,
        FakeUnitOfWork UnitOfWork,
        TestTransactionManager TransactionManager);
}
