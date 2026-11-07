namespace CoffeeShop.Api;

using System.Security.Claims;
using System.Text;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Contracts;
using CoffeeShop.Infrastructure.Postgres.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

public static class StaffIdentityEndpoints
{
    public static void AddStaffIdentity(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<StaffIdentityService>();
        builder.Services.AddScoped<StaffAdministrationService>();
        var key = builder.Configuration["Jwt:SigningKey"];
        var issuer = builder.Configuration["Jwt:Issuer"];
        var audience = builder.Configuration["Jwt:Audience"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32 ||
            string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("Configure Jwt:Issuer, Jwt:Audience and a Jwt:SigningKey of at least 32 bytes through local secret configuration.");
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new()
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                NameClaimType = "sub",
                RoleClaimType = ClaimTypes.Role,
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var value = context.Principal?.FindFirstValue("sub");
                    if (!Guid.TryParse(value, out var id) || id == Guid.Empty)
                    {
                        context.Fail("Invalid staff session.");
                        return;
                    }
                    var service = context.HttpContext.RequestServices.GetRequiredService<StaffIdentityService>();
                    var session = await service.SessionAsync(new(id), context.HttpContext.RequestAborted);
                    if (session is null)
                    {
                        context.Fail("Invalid staff session.");
                        return;
                    }
                    var identity = (ClaimsIdentity)context.Principal!.Identity!;
                    foreach (var claim in identity.FindAll("permission").Concat(identity.FindAll(ClaimTypes.Role)).ToArray())
                        identity.RemoveClaim(claim);
                    identity.AddClaims(session.Permissions.Select(permission => new Claim("permission", permission)));
                    identity.AddClaims(session.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
                },
            };
        });
        builder.Services.AddAuthorization(options =>
        {
            foreach (var permission in StaffRoleSeed.Bundles.Values.SelectMany(bundle => bundle).Distinct())
                options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser().RequireClaim("permission", permission));
        });
    }

    public static void MapStaffIdentity(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/login", async (LoginRequest request, StaffIdentityService service, HttpContext http, CancellationToken ct) =>
        {
            var pair = await service.LoginAsync(request, http.Connection.RemoteIpAddress?.ToString(), ct);
            http.Response.Headers.CacheControl = "no-store";
            return pair is null ? Denied() : Results.Ok(pair);
        }).AllowAnonymous().Produces<TokenResponse>().ProducesProblem(401);
        api.MapPost("/auth/refresh", async (RefreshRequest request, StaffIdentityService service, HttpContext http, CancellationToken ct) =>
        {
            var pair = await service.RefreshAsync(request.RefreshToken, http.Connection.RemoteIpAddress?.ToString(), ct);
            http.Response.Headers.CacheControl = "no-store";
            return pair is null ? Denied() : Results.Ok(pair);
        }).AllowAnonymous().Produces<TokenResponse>().ProducesProblem(401);
        api.MapGet("/auth/me", async (ICurrentUser user, StaffIdentityService service, CancellationToken ct) =>
            Results.Ok(await service.SessionAsync(user.GetRequiredId(), ct)))
            .RequireAuthorization().Produces<StaffSession>().ProducesProblem(401);
        api.MapPost("/auth/logout", async (RefreshRequest request, ICurrentUser user, StaffIdentityService service, CancellationToken ct) =>
            await service.LogoutAsync(user.GetRequiredId(), request.RefreshToken, false, ct) ? Results.NoContent() : Denied())
            .RequireAuthorization().Produces(204).ProducesProblem(401);
        api.MapPost("/auth/logout-all", async (ICurrentUser user, StaffIdentityService service, CancellationToken ct) =>
        {
            await service.LogoutAsync(user.GetRequiredId(), null, true, ct);
            return Results.NoContent();
        }).RequireAuthorization().Produces(204).ProducesProblem(401);

        var users = api.MapGroup("/users").RequireAuthorization("users.manage");
        users.MapGet("/", async (StaffAdministrationService service, HttpContext http, string? search,
            bool? isActive, int? limit, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await service.SearchAccountsAsync(search, isActive, limit, ct));
        })
            .Produces<StaffAccountResponse[]>().ProducesProblem(401).ProducesProblem(403);
        users.MapGet("/{id:guid}", async (Guid id, StaffAdministrationService service,
            HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (id == Guid.Empty) return Results.Problem(statusCode: 400, title: "Invalid account identifier");
            var account = await service.GetAccountAsync(id, ct);
            return account is null ? Results.Problem(statusCode: 404, title: "Staff account not found") : Results.Ok(account);
        }).Produces<StaffAccountResponse>().ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);
        users.MapPost("/{id:guid}/deactivate", async (Guid id, DeactivateStaffRequest request,
            StaffAdministrationService service, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var result = await service.DeactivateAsync(id, request.Confirm,
                http.Connection.RemoteIpAddress?.ToString(), ct);
            return result.IsSuccess ? Results.Ok(result.Value) : ApiErrorResults.From(result.Error!);
        }).Produces<StaffAccountResponse>().ProducesProblem(400).ProducesProblem(401).ProducesProblem(403)
            .ProducesProblem(404).ProducesProblem(409);

        var securityEvents = api.MapGroup("/security-events").RequireAuthorization("users.manage");
        securityEvents.MapGet("/", async (StaffAdministrationService service, HttpContext http,
            string? eventType, Guid? userId, int? limit, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await service.GetSecurityEventsAsync(eventType, userId, limit, ct));
        })
            .Produces<SecurityEventResponse[]>().ProducesProblem(401).ProducesProblem(403);
    }

    private static IResult Denied() => Results.Problem(statusCode: 401, title: "Authentication failed", detail: "Invalid credentials or session.");
}
