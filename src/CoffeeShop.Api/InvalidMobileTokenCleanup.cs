namespace CoffeeShop.Api;

using CoffeeShop.Domain.Common;
using CoffeeShop.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

/// <summary>Revokes only the currently active registration for a token rejected by a push provider.</summary>
public sealed class InvalidMobileTokenCleanup(CoffeeShopDbContext db)
{
    public async Task<bool> RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var registration = await db.MobileDeviceRegistrations.SingleOrDefaultAsync(
            device => device.Token == token && device.RevokedAt == null, cancellationToken);
        if (registration is null) return false;

        // Recheck the token under a row lock so a delayed provider response cannot revoke a rotated token.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var id = registration.Id.Value;
        db.Entry(registration).State = EntityState.Detached;
        registration = await db.MobileDeviceRegistrations.FromSqlInterpolated(
            $"SELECT * FROM identity.mobile_device_registrations WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (registration is null || registration.RevokedAt is not null || registration.Token != token) return false;
        registration.Revoke(new UtcTimestamp(DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
