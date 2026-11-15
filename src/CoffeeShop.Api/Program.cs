using CoffeeShop.Api;
using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Application.Customers;
using CoffeeShop.Application.Sales;
using CoffeeShop.Contracts;
using CoffeeShop.Domain.Common;
using CoffeeShop.Infrastructure.Postgres;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("CoffeeShop");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Set ConnectionStrings__CoffeeShop before starting the API.");
}

builder.Services.AddCoffeeShopPostgres(connectionString);
builder.Services.AddScoped<IClock, SystemClock>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();
builder.Services.AddHostedService<OperationalEventListener>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<InvalidMobileTokenCleanup>();
builder.Services.AddScoped<MobilePushDispatch>();
if (builder.Configuration.GetValue<bool>("StaffPush:EnableExpo"))
    builder.Services.AddHttpClient<IMobilePushProvider, ExpoMobilePushProvider>(
        client => client.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddScoped<CatalogQueryHandler>();
builder.Services.AddScoped<CatalogCommandHandler>();
builder.Services.AddScoped<RecipeCommandHandler>();
builder.Services.AddScoped<CustomerOrderSelectionHandler>();
builder.Services.AddScoped<CustomerCommandHandler>();
builder.Services.AddScoped<CustomerQueryHandler>();
builder.Services.AddScoped<OrderConfirmationCommandHandler>();
builder.Services.AddScoped<DiscountCommandHandler>();
builder.Services.AddScoped<PosOrderHandler>();
builder.Services.AddScoped<PaymentCommandHandler>();
builder.Services.AddScoped<DeliveryCommandHandler>();
builder.Services.AddScoped<AssistantToolGateway>();
builder.Services.AddScoped<IAssistantToolGateway>(provider => provider.GetRequiredService<AssistantToolGateway>());
builder.Services.AddScoped<ManagerAssistantService>();
switch (builder.Configuration["Assistant:Provider"]?.ToLowerInvariant())
{
    case "openai":
        builder.Services.AddHttpClient<IManagerAssistantProvider, OpenAiAssistantProvider>(client =>
            client.Timeout = TimeSpan.FromSeconds(15));
        break;
    case "deterministic":
        builder.Services.AddSingleton<IManagerAssistantProvider, DeterministicAssistantProvider>();
        break;
    default:
        builder.Services.AddSingleton<IManagerAssistantProvider, UnavailableAssistantProvider>();
        break;
}
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Servers = [];
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
        };
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, _) =>
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = [],
            });
        }
        return Task.CompletedTask;
    });
});
builder.AddStaffIdentity();
builder.Services.AddHealthChecks()
    .AddCheck("postgres", new PostgresHealthCheck(connectionString), tags: ["ready"]);

var app = builder.Build();
if (args.Contains("--migrate"))
{
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        await db.Database.MigrateAsync();
    }
    await app.DisposeAsync();
    return;
}
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
app.MapHub<OperationalEventsHub>("/api/v1/operational-events", options => options.CloseOnAuthenticationExpiration = true).RequireAuthorization();

var api = app.MapGroup("/api/v1");
api.MapStaffIdentity();
api.MapMobileDevices();
api.MapInventoryEndpoints();
api.MapCatalogEndpoints();
api.MapSalesEndpoints();
api.MapDeliveryEndpoints();
api.MapCustomerEndpoints();
api.MapReportEndpoints();
api.MapAssistantEndpoints();
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
.ProducesProblem(StatusCodes.Status401Unauthorized)
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
.ProducesProblem(StatusCodes.Status401Unauthorized)
.ProducesProblem(StatusCodes.Status403Forbidden)
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
.ProducesProblem(StatusCodes.Status401Unauthorized)
.ProducesProblem(StatusCodes.Status403Forbidden)
.ProducesProblem(StatusCodes.Status404NotFound)
.ProducesProblem(StatusCodes.Status409Conflict)
.RequireAuthorization("menu.manage");

api.MapPut("/categories/{id:guid}", async (
    Guid id, CreateCategoryRequest request, CatalogCommandHandler handler,
    CancellationToken cancellationToken) =>
{
    if (id == Guid.Empty)
        return ApiErrorResults.From(Error.Validation("catalog.invalid_id", "Category identifier is required."));
    var result = await handler.UpdateCategoryAsync(
        new UpdateCategoryCommand(new EntityId(id), request.Name ?? string.Empty), cancellationToken);
    return result.IsSuccess ? Results.NoContent() : ApiErrorResults.From(result.Error!);
})
.Produces(StatusCodes.Status204NoContent)
.ProducesProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status401Unauthorized)
.ProducesProblem(StatusCodes.Status403Forbidden)
.ProducesProblem(StatusCodes.Status404NotFound)
.ProducesProblem(StatusCodes.Status409Conflict)
.RequireAuthorization("menu.manage");

app.Run();

public partial class Program;
