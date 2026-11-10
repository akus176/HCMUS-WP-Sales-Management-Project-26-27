namespace CoffeeShop.Api;

using CoffeeShop.Application.Common.Security;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Identity;
using CoffeeShop.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Npgsql;

public sealed record MobileDeviceRequest(string? Platform, string? Token);
public sealed record MobileDeviceResponse(Guid Id);

public static class MobileDeviceEndpoints
{
    public static void MapMobileDevices(this RouteGroupBuilder api)
    {
        var devices = api.MapGroup("/mobile-devices").RequireAuthorization();
        devices.MapPost("/", async (MobileDeviceRequest request, ICurrentUser user, CoffeeShopDbContext db, CancellationToken ct) =>
        {
            if (!Valid(request)) return Invalid();
            var device = new MobileDeviceRegistration(EntityId.New(), user.GetRequiredId(), request.Platform!, request.Token!, Now());
            db.MobileDeviceRegistrations.Add(device);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException e) when (IsDuplicate(e))
            {
                // A client may have lost the response or its locally stored registration ID.
                var existing = await db.MobileDeviceRegistrations.AsNoTracking().SingleOrDefaultAsync(
                    registration => registration.Token == device.Token && registration.RevokedAt == null, ct);
                if (existing is null || existing.UserId != device.UserId) return Conflict();
                device = existing;
            }
            return Results.Created($"/api/v1/mobile-devices/{device.Id.Value}", new MobileDeviceResponse(device.Id.Value));
        }).Produces<MobileDeviceResponse>(201).ProducesProblem(400).ProducesProblem(401).ProducesProblem(409);

        devices.MapPut("/{id:guid}", async (Guid id, MobileDeviceRequest request, ICurrentUser user, CoffeeShopDbContext db, CancellationToken ct) =>
        {
            if (!Valid(request)) return Invalid();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var device = await db.MobileDeviceRegistrations.FromSqlInterpolated($"SELECT * FROM identity.mobile_device_registrations WHERE id = {id} FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            if (device is null || device.UserId != user.GetRequiredId() || device.RevokedAt is not null) return Results.NotFound();
            device.Rotate(request.Platform!, request.Token!, Now());
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException e) when (IsDuplicate(e)) { return Conflict(); }
            await transaction.CommitAsync(ct);
            return Results.NoContent();
        }).Produces(204).ProducesProblem(400).ProducesProblem(401).Produces(404).ProducesProblem(409);

        devices.MapDelete("/{id:guid}", async (Guid id, ICurrentUser user, CoffeeShopDbContext db, CancellationToken ct) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var device = await db.MobileDeviceRegistrations.FromSqlInterpolated($"SELECT * FROM identity.mobile_device_registrations WHERE id = {id} FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            if (device is null || device.UserId != user.GetRequiredId()) return Results.NotFound();
            device.Revoke(Now());
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.NoContent();
        }).Produces(204).ProducesProblem(401).Produces(404);
    }

    private static bool Valid(MobileDeviceRequest request) =>
        request.Platform?.Trim() is "android" or "ios" && !string.IsNullOrWhiteSpace(request.Token) && request.Token.Trim().Length <= 512;
    private static UtcTimestamp Now() => new(DateTimeOffset.UtcNow);
    private static bool IsDuplicate(DbUpdateException e) => e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    private static IResult Invalid() => Results.Problem(statusCode: 400, title: "Invalid device registration");
    private static IResult Conflict() => Results.Problem(statusCode: 409, title: "Device token is already registered");
}
