namespace CoffeeShop.Api;

using System.Net.Mail;
using System.Text.Json;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Contracts;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Identity;
using CoffeeShop.Infrastructure.Postgres;
using CoffeeShop.Infrastructure.Postgres.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

public sealed class StaffAdministrationService(CoffeeShopDbContext db, TimeProvider time)
{
    private const int MaximumPageSize = 100;

    public async Task<Result<StaffAccountResponse>> CreateAsync(
        CreateStaffRequest request, EntityId actorId, string? ipAddress, CancellationToken ct)
    {
        var username = request.Username?.Trim();
        var email = request.Email?.Trim().ToLowerInvariant();
        var fullName = request.FullName?.Trim();
        var phone = request.Phone?.Trim();
        var roleCode = request.Role?.Trim();
        if (string.IsNullOrWhiteSpace(username) || username.Length > 50 ||
            string.IsNullOrWhiteSpace(fullName) || fullName.Length > 150 ||
            string.IsNullOrWhiteSpace(email) || email.Length > 255 ||
            !MailAddress.TryCreate(email, out var address) || address.Address != email ||
            phone?.Length > 30 || request.Password is null || request.Password.Length is < 12 or > 1024 ||
            roleCode is null || !StaffRoleSeed.Bundles.ContainsKey(roleCode))
            return Result<StaffAccountResponse>.Failure(Error.Validation(
                "staff.invalid_profile", "Enter a valid name, username, email, phone, operational role and password of 12 to 1024 characters."));

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Serialize account creation so case-insensitive identity checks also cover concurrent requests.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(228001)", ct);
        var normalizedUsername = username.ToLowerInvariant();
        if (await db.Users.AnyAsync(user => user.Username.ToLower() == normalizedUsername || user.Email == email, ct))
            return Result<StaffAccountResponse>.Failure(Error.Conflict(
                "staff.duplicate_identity", "A staff account already uses that username or email."));

        var role = await db.Roles.SingleOrDefaultAsync(item => item.Code == roleCode, ct);
        if (role is null)
            return Result<StaffAccountResponse>.Failure(Error.Validation(
                "staff.invalid_role", "The selected operational role is unavailable."));

        var now = new UtcTimestamp(time.GetUtcNow());
        var user = new User(EntityId.New(), username, email, "pending", fullName, phone, now, now);
        db.Entry(user).Property(item => item.PasswordHash).CurrentValue = StaffIdentityService.HashPassword(user, request.Password);
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole(user.Id, role.Id, now, actorId));
        db.SecurityAuditEvents.Add(new SecurityAuditEvent(EntityId.New(), "account_created", now,
            "success", user.Id, ipAddress, JsonSerializer.Serialize(new { actorId = actorId.Value })));
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_users_username" or "IX_users_email" })
        {
            return Result<StaffAccountResponse>.Failure(Error.Conflict(
                "staff.duplicate_identity", "A staff account already uses that username or email."));
        }
        return Result<StaffAccountResponse>.Success(ToResponse(user));
    }

    public async Task<StaffAccountResponse[]> SearchAccountsAsync(
        string? search, bool? isActive, int? limit, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking().AsQueryable();
        if (isActive is not null) query = query.Where(user => user.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            if (term.Length > 150)
                return [];
            query = query.Where(user => EF.Functions.ILike(user.Username, $"%{term}%") ||
                EF.Functions.ILike(user.FullName, $"%{term}%") ||
                EF.Functions.ILike(user.Email, $"%{term}%"));
        }

        var users = await query.Include(user => user.UserRoles).ThenInclude(userRole => userRole.Role)
            .OrderBy(user => user.Username).Take(NormalizeLimit(limit)).ToArrayAsync(ct);
        return users.Select(ToResponse).ToArray();
    }

    public async Task<StaffAccountResponse?> GetAccountAsync(Guid id, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Include(item => item.UserRoles).ThenInclude(item => item.Role)
            .SingleOrDefaultAsync(item => item.Id == new EntityId(id), ct);
        return user is null ? null : ToResponse(user);
    }

    public async Task<SecurityEventResponse[]> GetSecurityEventsAsync(
        string? eventType, Guid? userId, int? limit, CancellationToken ct)
    {
        var query = db.SecurityAuditEvents.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var term = eventType.Trim();
            query = query.Where(item => item.EventType == term);
        }
        if (userId is not null) query = query.Where(item => item.UserId == new EntityId(userId.Value));

        var events = await query.OrderByDescending(item => item.OccurredAt)
            .Take(NormalizeLimit(limit)).Select(item => new
            {
                item.Id,
                item.EventType,
                item.OccurredAt,
                item.Outcome,
                item.UserId,
                item.IpAddress,
            }).ToArrayAsync(ct);
        return events.Select(item => new SecurityEventResponse(
            item.Id.Value, item.EventType, item.OccurredAt.Value, item.Outcome,
            item.UserId?.Value, item.IpAddress)).ToArray();
    }

    public async Task<Result<StaffAccountResponse>> DeactivateAsync(
        Guid id, bool confirmed, string? ipAddress, CancellationToken ct)
    {
        if (!confirmed)
            return Result<StaffAccountResponse>.Failure(Error.Validation(
                "staff.deactivation_confirmation_required", "Explicit confirmation is required."));
        if (id == Guid.Empty)
            return Result<StaffAccountResponse>.Failure(Error.Validation(
                "staff.invalid_id", "A nonempty account identifier is required."));

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Lock all active administrators in one deterministic order. Concurrent requests
        // to deactivate admins then serialize before checking the final-admin invariant.
        await db.Database.ExecuteSqlRawAsync("""
            SELECT u.id
            FROM identity.users AS u
            WHERE u.is_active = TRUE AND EXISTS (
                SELECT 1 FROM identity.user_roles AS ur
                JOIN identity.roles AS r ON r.id = ur.role_id
                WHERE ur.user_id = u.id AND r.code = 'Admin')
            ORDER BY u.id
            FOR UPDATE OF u
            """, ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM identity.users WHERE id = {id} FOR UPDATE", ct);

        var user = await db.Users.Include(item => item.UserRoles).ThenInclude(item => item.Role)
            .SingleOrDefaultAsync(item => item.Id == new EntityId(id), ct);
        if (user is null)
            return Result<StaffAccountResponse>.Failure(Error.NotFound("staff.not_found", "The staff account was not found."));
        if (!user.IsActive)
            return Result<StaffAccountResponse>.Failure(Error.Conflict("staff.already_inactive", "The staff account is already inactive."));

        var isAdministrator = user.UserRoles.Any(role => role.Role?.Code == "Admin");
        if (isAdministrator)
        {
            var activeAdministrators = await db.Users.CountAsync(candidate => candidate.IsActive &&
                candidate.UserRoles.Any(role => role.Role!.Code == "Admin"), ct);
            if (activeAdministrators <= 1)
                return Result<StaffAccountResponse>.Failure(Error.Conflict(
                    "staff.final_administrator", "The final active administrator cannot be deactivated."));
        }

        var now = new UtcTimestamp(time.GetUtcNow());
        user.Deactivate(now);
        var sessions = await db.RefreshTokens.Where(token => token.UserId == user.Id && token.RevokedAt == null)
            .ToArrayAsync(ct);
        foreach (var session in sessions) session.Revoke(now);
        db.SecurityAuditEvents.Add(new SecurityAuditEvent(EntityId.New(), "account_deactivated", now,
            "success", user.Id, ipAddress));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result<StaffAccountResponse>.Success(ToResponse(user));
    }

    private static StaffAccountResponse ToResponse(User user) => new(
        user.Id.Value, user.Username, user.FullName, user.Email, user.Phone, user.IsActive,
        user.UserRoles.Select(role => role.Role!.Code).Order(StringComparer.Ordinal).ToArray(),
        user.CreatedAt.Value, user.DeactivatedAt?.Value);

    private static int NormalizeLimit(int? limit) => Math.Clamp(limit ?? 50, 1, MaximumPageSize);
}
