using CoffeeShop.Api;
using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Contracts;
using CoffeeShop.Domain.Common;
using CoffeeShop.Infrastructure.Postgres;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("CoffeeShop");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Set ConnectionStrings__CoffeeShop before starting the API.");
}

builder.Services.AddCoffeeShopPostgres(connectionString);
builder.Services.AddScoped<IClock, SystemClock>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<CatalogQueryHandler>();
builder.Services.AddScoped<CatalogCommandHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Servers = [];
    return Task.CompletedTask;
}));
builder.AddStaffIdentity();
builder.Services.AddHealthChecks()
    .AddCheck("postgres", new PostgresHealthCheck(connectionString), tags: ["ready"]);

var app = builder.Build();
if (args.Contains("--seed-staff-roles") || args.Contains("--provision-staff"))
{
    await StaffProvisioning.RunAsync(app.Services, builder.Configuration, args.Contains("--provision-staff"));
    await app.DisposeAsync();
    return;
}
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") });

var api = app.MapGroup("/api/v1");
api.MapStaffIdentity();
api.MapGet("/categories", async (
    CatalogQueryHandler handler, string? search, bool? isActive, int? limit, CancellationToken cancellationToken) =>
{
    var result = await handler.SearchCategoriesAsync(
        new CategorySearchCriteria(search, isActive, limit ?? 50), cancellationToken);
    return result.IsSuccess
        ? Results.Ok(result.Value.Select(category => new CategoryResponse(
            category.Id.Value, category.Name, category.DisplayOrder, category.IsActive)))
        : ApiErrorResults.From(result.Error!);
})
.Produces<CategoryResponse[]>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status400BadRequest)
.RequireAuthorization();

api.MapPost("/categories", async (
    CreateCategoryRequest request, CatalogCommandHandler handler, CancellationToken cancellationToken) =>
{
    var result = await handler.CreateCategoryAsync(
        new CreateCategoryCommand(request.Name ?? string.Empty, request.DisplayOrder), cancellationToken);
    return result.IsSuccess
        ? Results.Json(new CategoryResponse(result.Value.Value, request.Name?.Trim() ?? string.Empty, request.DisplayOrder, true),
            statusCode: StatusCodes.Status201Created)
        : ApiErrorResults.From(result.Error!);
})
.Produces<CategoryResponse>(StatusCodes.Status201Created)
.ProducesProblem(StatusCodes.Status400BadRequest)
.RequireAuthorization("menu.manage");

api.MapPost("/categories/{id:guid}/deactivate", async (
    Guid id, CatalogCommandHandler handler, CancellationToken cancellationToken) =>
{
    if (id == Guid.Empty)
        return ApiErrorResults.From(Error.Validation("catalog.invalid_id", "Category identifier is required."));
    var result = await handler.DeactivateCategoryAsync(
        new DeactivateCategoryCommand(new EntityId(id)), cancellationToken);
    return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
})
.Produces(StatusCodes.Status204NoContent)
.ProducesProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status404NotFound)
.ProducesProblem(StatusCodes.Status409Conflict)
.RequireAuthorization("menu.manage");

app.Run();

public partial class Program;
