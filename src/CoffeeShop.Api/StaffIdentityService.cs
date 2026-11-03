namespace CoffeeShop.Api;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CoffeeShop.Contracts;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Identity;
using CoffeeShop.Infrastructure.Postgres;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

public sealed class StaffIdentityService(CoffeeShopDbContext db, IConfiguration configuration, TimeProvider time)
{
    private static readonly PasswordHasher<User> Hasher = new();
    private static readonly string DummyHash = Hasher.HashPassword(null!, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    public static string HashPassword(User user, string password) => Hasher.HashPassword(user, password);
    public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public async Task<StaffSession?> SessionAsync(EntityId id, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id && u.IsActive, ct);
        if (user is null) return null;
        var roles = await db.UserRoles.Where(ur => ur.UserId == id).Select(ur => ur.Role!.Code).ToArrayAsync(ct);
        var permissions = await db.UserRoles.Where(ur => ur.UserId == id)
            .SelectMany(ur => ur.Role!.RolePermissions.Select(rp => rp.Permission!.Code)).Distinct().ToArrayAsync(ct);
        return new(user.Id.Value, user.FullName, roles.Order().ToArray(), permissions.Order().ToArray());
    }

    public async Task<TokenResponse?> LoginAsync(LoginRequest request, string? ip, CancellationToken ct)
    {
        var username = request.Username?.Trim();
        var password = request.Password ?? string.Empty;
        // Bound inputs before hashing and never put submitted credentials in errors/audits.
        if (username is null || username.Length > 50 || password.Length > 1024)
        {
            Audit("login_failed", "denied", null, ip);
            await db.SaveChangesAsync(ct);
            return null;
        }
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Username == username, ct);
        if (user is not null)
        {
            await LockUserAsync(user.Id, ct);
            await db.Entry(user).ReloadAsync(ct);
        }
        PasswordVerificationResult verification;
        try { verification = Hasher.VerifyHashedPassword(user!, user?.PasswordHash ?? DummyHash, password); }
        catch (FormatException) { verification = PasswordVerificationResult.Failed; }
        if (user is null || !user.IsActive || verification == PasswordVerificationResult.Failed)
        {
            Audit("login_failed", "denied", user?.Id, ip);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return null;
        }
        var pair = await IssueAsync(user.Id, Guid.NewGuid(), ip, ct);
        Audit("login_success", "success", user.Id, ip);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return pair;
    }

    public async Task<TokenResponse?> RefreshAsync(string? token, string? ip, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 256) return null;
        var hash = HashToken(token);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var candidate = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (candidate is null) return null;
        await LockUserAsync(candidate.UserId, ct);
        var stored = await db.RefreshTokens.SingleAsync(t => t.TokenHash == hash, ct);
        var user = await db.Users.SingleAsync(u => u.Id == stored.UserId, ct);
        var now = new UtcTimestamp(time.GetUtcNow());
        TokenResponse? pair = null;
        if (stored.RevokedAt is not null)
        {
            await RevokeAsync(stored.UserId, stored.FamilyId, now, ct);
            Audit("token_reuse_detected", "denied", stored.UserId, ip);
        }
        else if (!user.IsActive || stored.ExpiresAt.Value <= now.Value)
        {
            if (!user.IsActive) await RevokeAsync(user.Id, null, now, ct);
            else stored.Revoke(now);
        }
        else
        {
            pair = await IssueAsync(stored.UserId, stored.FamilyId, ip, ct);
            var replacement = db.ChangeTracker.Entries<RefreshToken>()
                .Single(entry => entry.State == EntityState.Added).Entity;
            stored.Revoke(now, replacement.Id);
            Audit("token_refresh", "success", stored.UserId, ip);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return pair;
    }

    public async Task<bool> LogoutAsync(EntityId userId, string? token, bool all, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockUserAsync(userId, ct);
        Guid? family = null;
        if (!all)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 256) return false;
            var hash = HashToken(token);
            var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.UserId == userId && t.TokenHash == hash, ct);
            if (stored is null) return false;
            family = stored.FamilyId;
        }
        await RevokeAsync(userId, family, new UtcTimestamp(time.GetUtcNow()), ct);
        Audit("logout", "success", userId, null);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    private async Task LockUserAsync(EntityId userId, CancellationToken ct) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM identity.users WHERE id = {userId.Value} FOR UPDATE", ct);

    private async Task RevokeAsync(EntityId userId, Guid? family, UtcTimestamp now, CancellationToken ct)
    {
        var tokens = await db.RefreshTokens.Where(t => t.UserId == userId && (family == null || t.FamilyId == family)).ToListAsync(ct);
        foreach (var token in tokens) token.Revoke(now);
    }

    private async Task<TokenResponse> IssueAsync(EntityId userId, Guid family, string? ip, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var expires = now.AddMinutes(10);
        var session = (await SessionAsync(userId, ct))!;
        var claims = new List<Claim> { new("sub", userId.ToString()), new("jti", Guid.NewGuid().ToString()) };
        claims.AddRange(session.Roles.Select(role => new Claim("role", role)));
        // Permissions are resolved from the database on each authenticated request.
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"], claims,
            now.UtcDateTime, expires.UtcDateTime, credentials);
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        db.RefreshTokens.Add(new RefreshToken(EntityId.New(), userId, HashToken(refresh), family,
            new UtcTimestamp(now), new UtcTimestamp(now.AddDays(14)), createdByIp: ip));
        return new(new JwtSecurityTokenHandler().WriteToken(jwt), refresh, expires, session);
    }

    private void Audit(string type, string outcome, EntityId? userId, string? ip) =>
        db.SecurityAuditEvents.Add(new SecurityAuditEvent(EntityId.New(), type, new UtcTimestamp(time.GetUtcNow()), outcome, userId, ip));
}
