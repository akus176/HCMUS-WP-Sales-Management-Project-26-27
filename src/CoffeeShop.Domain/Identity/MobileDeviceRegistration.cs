namespace CoffeeShop.Domain.Identity;

using CoffeeShop.Domain.Common;

public class MobileDeviceRegistration
{
    protected MobileDeviceRegistration() { }

    public MobileDeviceRegistration(EntityId id, EntityId userId, string platform, string token, UtcTimestamp lastSeenAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentNullException.ThrowIfNull(lastSeenAt);
        Id = id;
        UserId = userId;
        Platform = platform.Trim();
        Token = token.Trim();
        LastSeenAt = lastSeenAt;
    }

    public EntityId Id { get; protected set; } = null!;
    public EntityId UserId { get; protected set; } = null!;
    public User? User { get; protected set; }
    public string Platform { get; protected set; } = string.Empty;
    public string Token { get; protected set; } = string.Empty;
    public UtcTimestamp LastSeenAt { get; protected set; } = null!;
    public UtcTimestamp? RevokedAt { get; protected set; }

    public void Revoke(UtcTimestamp now) => RevokedAt ??= now;
}
