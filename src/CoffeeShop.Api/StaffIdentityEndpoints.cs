namespace CoffeeShop.Api;

using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Contracts;
using CoffeeShop.Infrastructure.Postgres.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

public static class StaffIdentityEndpoints
{
    public static void AddStaffIdentity(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<StaffIdentityService>();
        builder.Services.AddScoped<StaffAdministrationService>();
        builder.Services.AddSingleton(GoogleAdminOptions.Load(builder.Configuration));
        builder.Services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(_ =>
            new ConfigurationManager<OpenIdConnectConfiguration>("https://accounts.google.com/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever()));
        builder.Services.AddHttpClient<GoogleAdminIdentityProvider>(client => client.Timeout = TimeSpan.FromSeconds(10))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
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
                OnMessageReceived = context =>
                {
                    if (context.Request.Path == "/api/v1/operational-events" &&
                        context.Request.Query.TryGetValue("access_token", out var token))
                        context.Token = token;
                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    var value = context.Principal?.FindFirstValue("sub");
                    if (!Guid.TryParse(value, out var id) || id == Guid.Empty)
                    {
                        context.Fail("Invalid staff session.");
                        return;
                    }
                    var service = context.HttpContext.RequestServices.GetRequiredService<StaffIdentityService>();
                    var session = await service.SessionAsync(new(id), context.HttpContext.RequestAborted,
                        context.Principal?.FindFirstValue(StaffIdentityService.GoogleBindingClaim));
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
        api.MapGet("/auth/google/config", (GoogleAdminOptions google, HttpContext http) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            return google.Enabled ? Results.Ok(new GoogleAdminConfigResponse(google.ClientId))
                : Results.Problem(statusCode: 404, title: "Google sign-in is not configured");
        }).AllowAnonymous().Produces<GoogleAdminConfigResponse>().ProducesProblem(404);
        api.MapPost("/auth/google", async (GoogleAdminLoginRequest request, GoogleAdminOptions google,
            GoogleAdminIdentityProvider provider, StaffIdentityService service, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (!google.Enabled) return GoogleUnavailable();
            GoogleAdminLink? link;
            try { link = await provider.VerifyAsync(request, ct); }
            catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or JsonException ||
                exception is OperationCanceledException && !ct.IsCancellationRequested)
            {
                return GoogleUnavailable();
            }
            var pair = await service.GoogleLoginAsync(link, http.Connection.RemoteIpAddress?.ToString(), ct);
            return pair is null ? Denied() : Results.Ok(pair);
        }).AllowAnonymous().Produces<TokenResponse>().ProducesProblem(401).ProducesProblem(503);
        api.MapPost("/auth/refresh", async (RefreshRequest request, StaffIdentityService service, HttpContext http, CancellationToken ct) =>
        {
            var pair = await service.RefreshAsync(request.RefreshToken, http.Connection.RemoteIpAddress?.ToString(), ct);
            http.Response.Headers.CacheControl = "no-store";
            return pair is null ? Denied() : Results.Ok(pair);
        }).AllowAnonymous().Produces<TokenResponse>().ProducesProblem(401);
        api.MapGet("/auth/me", async (ICurrentUser user, StaffIdentityService service, HttpContext http, CancellationToken ct) =>
        {
            var session = await service.SessionAsync(user.GetRequiredId(), ct, http.User.FindFirstValue(StaffIdentityService.GoogleBindingClaim));
            return session is null ? Denied() : Results.Ok(session);
        })
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
        users.MapPost("/", async (CreateStaffRequest request, ICurrentUser actor,
            StaffAdministrationService service, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var result = await service.CreateAsync(request, actor.GetRequiredId(),
                http.Connection.RemoteIpAddress?.ToString(), ct);
            return result.IsSuccess
                ? Results.Created($"/api/v1/users/{result.Value!.Id}", result.Value)
                : ApiErrorResults.From(result.Error!);
        }).Produces<StaffAccountResponse>(201).ProducesProblem(400).ProducesProblem(401)
            .ProducesProblem(403).ProducesProblem(409);
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
    private static IResult GoogleUnavailable() => Results.Problem(statusCode: 503, title: "Google sign-in is unavailable");
}
