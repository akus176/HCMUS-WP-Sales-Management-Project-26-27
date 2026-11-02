using CoffeeShop.Api;
using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Contracts;
using CoffeeShop.Infrastructure.Postgres;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Diagnostics.HealthChecks;

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
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddAuthorization(options => options.AddPolicy("menu.manage",
    policy => policy.RequireClaim("permission", "menu.manage")));
builder.Services.AddHealthChecks()
    .AddCheck("postgres", new PostgresHealthCheck(connectionString), tags: ["ready"]);

var app = builder.Build();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") });

var api = app.MapGroup("/api/v1");
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

app.Run();

public partial class Program;
