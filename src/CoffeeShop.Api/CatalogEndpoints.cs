namespace CoffeeShop.Api;

using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Contracts;
using CoffeeShop.Domain.Common;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this RouteGroupBuilder api)
    {
        var items = api.MapGroup("/menu-items").RequireAuthorization();
        items.MapGet("/", async (CatalogQueryHandler handler, string? search, Guid? categoryId,
            bool? isActive, int? limit, CancellationToken ct) =>
        {
            if (categoryId == Guid.Empty) return InvalidId();
            var result = await handler.SearchMenuItemsAsync(new(search,
                categoryId is null ? null : new(categoryId.Value), isActive, limit ?? 50), ct);
            return result.IsSuccess ? Results.Ok(result.Value.Select(ToItem).ToArray()) : ApiErrorResults.From(result.Error!);
        }).Produces<MenuItemResponse[]>().ProducesProblem(400);
        items.MapGet("/{id:guid}", async (Guid id, CatalogQueryHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var result = await handler.GetMenuItemDetailAsync(new(id), ct);
            return result.IsSuccess ? Results.Ok(ToItem(result.Value)) : ApiErrorResults.From(result.Error!);
        }).Produces<MenuItemResponse>().ProducesProblem(404);
        items.MapPost("/", async (MenuItemRequest request, CatalogCommandHandler command,
            CatalogQueryHandler query, CancellationToken ct) =>
        {
            if (request.CategoryId == Guid.Empty) return InvalidId();
            var result = await command.CreateMenuItemAsync(new(new(request.CategoryId), request.Name ?? string.Empty,
                request.Description, (request.InitialVariants ?? []).Select(variant =>
                    new CreateSizeVariantInput(variant.SizeName ?? string.Empty, variant.Price)).ToArray()), ct);
            if (result.IsFailure) return ApiErrorResults.From(result.Error!);
            var detail = await query.GetMenuItemDetailAsync(result.Value, ct);
            return Results.Json(ToItem(detail.Value), statusCode: 201);
        }).RequireAuthorization("menu.manage").Produces<MenuItemResponse>(201).ProducesProblem(400).ProducesProblem(404);
        items.MapPut("/{id:guid}", async (Guid id, MenuItemRequest request, CatalogCommandHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty || request.CategoryId == Guid.Empty) return InvalidId();
            var result = await handler.UpdateMenuItemAsync(new(new(id), new(request.CategoryId), request.Name ?? string.Empty,
                request.Description), ct);
            return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("menu.manage").Produces(204).ProducesProblem(400).ProducesProblem(404);
        items.MapPost("/{id:guid}/deactivate", async (Guid id, CatalogCommandHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var result = await handler.DeactivateMenuItemAsync(new(new(id)), ct);
            return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("menu.manage").Produces(204).ProducesProblem(404).ProducesProblem(409);
        items.MapPost("/{id:guid}/variants", async (Guid id, SizeVariantRequest request,
            CatalogCommandHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var result = await handler.CreateSizeVariantAsync(new(new(id), request.SizeName ?? string.Empty, request.Price), ct);
            return result.IsSuccess ? Results.Json(new { id = result.Value.Value }, statusCode: 201)
                : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("menu.manage").Produces(201).ProducesProblem(400).ProducesProblem(404);
        items.MapPut("/{id:guid}/variants/{variantId:guid}", async (Guid id, Guid variantId,
            SizeVariantRequest request, CatalogCommandHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty || variantId == Guid.Empty) return InvalidId();
            var result = await handler.UpdateSizeVariantAsync(new(new(id), new(variantId), request.SizeName ?? string.Empty,
                request.Price), ct);
            return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("menu.manage").Produces(204).ProducesProblem(400).ProducesProblem(404);
        items.MapPost("/{id:guid}/variants/{variantId:guid}/deactivate", async (Guid id, Guid variantId,
            CatalogCommandHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty || variantId == Guid.Empty) return InvalidId();
            var result = await handler.DeactivateSizeVariantAsync(new(new(variantId)), ct);
            return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("menu.manage").Produces(204).ProducesProblem(404).ProducesProblem(409);
        api.MapPost("/size-variants/{variantId:guid}/deactivate", async (Guid variantId,
            CatalogCommandHandler handler, CancellationToken ct) =>
        {
            if (variantId == Guid.Empty) return InvalidId();
            var result = await handler.DeactivateSizeVariantAsync(new(new(variantId)), ct);
            return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("menu.manage").Produces(204).ProducesProblem(404).ProducesProblem(409);
        items.MapPut("/{id:guid}/addons", async (Guid id, SetCompatibleAddonsRequest request,
            CatalogCommandHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty || request.AddonIds is null || request.AddonIds.Contains(Guid.Empty)) return InvalidId();
            var result = await handler.SetMenuItemAddonsAsync(new(new(id), request.AddonIds.Select(value => new EntityId(value)).ToArray()), ct);
            return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("menu.manage").Produces(204).ProducesProblem(400).ProducesProblem(404);

        api.MapGet("/recipes/{variantId:guid}", async (Guid variantId, CatalogQueryHandler handler, CancellationToken ct) =>
        {
            if (variantId == Guid.Empty) return InvalidId();
            var result = await handler.GetRecipeAsync(new(variantId), ct);
            return result.IsSuccess ? Results.Ok(ToRecipe(result.Value)) : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization().Produces<RecipeResponse>().ProducesProblem(404);
        api.MapPut("/recipes/{variantId:guid}", async (Guid variantId, ReplaceRecipeRequest request,
            RecipeCommandHandler handler, CancellationToken ct) =>
        {
            if (variantId == Guid.Empty || request.Lines?.Any(line => line.IngredientId == Guid.Empty) == true) return InvalidId();
            var result = await handler.ReplaceRecipeAsync(new(new(variantId), (request.Lines ?? [])
                .Select(line => new RecipeLineInput(new(line.IngredientId), line.Quantity, line.Unit ?? string.Empty)).ToArray()), ct);
            return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("menu.manage").Produces(204).ProducesProblem(400).ProducesProblem(404);

        api.MapGet("/addons", async (CatalogQueryHandler handler, CancellationToken ct) =>
        {
            var result = await handler.ListAddonsAsync(ct);
            return Results.Ok(result.Value.Select(ToAddon).ToArray());
        }).RequireAuthorization().Produces<AddonResponse[]>();
        api.MapPost("/addons", async (AddonRequest request, CatalogCommandHandler handler, CancellationToken ct) =>
        {
            var result = await handler.CreateAddonAsync(new(request.Name ?? string.Empty, request.Price), ct);
            return result.IsSuccess ? Results.Json(new { id = result.Value.Value }, statusCode: 201)
                : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("menu.manage").Produces(201).ProducesProblem(400);
        api.MapPut("/addons/{id:guid}", async (Guid id, AddonRequest request, CatalogCommandHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var result = await handler.UpdateAddonAsync(new(new(id), request.Name ?? string.Empty, request.Price), ct);
            return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("menu.manage").Produces(204).ProducesProblem(400).ProducesProblem(404);
        api.MapPost("/addons/{id:guid}/deactivate", async (Guid id, CatalogCommandHandler handler, CancellationToken ct) =>
        {
            if (id == Guid.Empty) return InvalidId();
            var result = await handler.DeactivateAddonAsync(new(new(id)), ct);
            return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
        }).RequireAuthorization("menu.manage").Produces(204).ProducesProblem(404).ProducesProblem(409);
    }

    private static MenuItemResponse ToItem(MenuItemSummary item) => new(item.Id.Value, item.CategoryId.Value,
        item.CategoryName, item.Name, item.Description, item.IsActive,
        item.SizeVariants.Select(variant => new SizeVariantResponse(variant.Id.Value, variant.SizeName,
            variant.Price, variant.IsActive)).ToArray(), item.CompatibleAddons.Select(ToAddon).ToArray());

    private static MenuItemResponse ToItem(MenuItemDetail item) => new(item.Id.Value, item.CategoryId.Value,
        item.CategoryName, item.Name, item.Description, item.IsActive,
        item.SizeVariants.Select(variant => new SizeVariantResponse(variant.Id.Value, variant.SizeName,
            variant.Price, variant.IsActive, variant.Recipe.Select(ToLine).ToArray())).ToArray(),
        item.CompatibleAddons.Select(ToAddon).ToArray());

    private static RecipeResponse ToRecipe(RecipeDetail recipe) => new(recipe.SizeVariantId.Value,
        recipe.MenuItemId.Value, recipe.SizeName, recipe.Lines.Select(ToLine).ToArray());

    private static RecipeLineResponse ToLine(RecipeLineDetail line) => new(line.Id.Value, line.IngredientId.Value,
        line.IngredientName, line.Quantity, line.Unit);

    private static AddonResponse ToAddon(AddonSummary addon) => new(addon.Id.Value, addon.Name, addon.Price, addon.IsActive);

    private static IResult InvalidId() => ApiErrorResults.From(Error.Validation("catalog.invalid_id", "A nonempty identifier is required."));
}
