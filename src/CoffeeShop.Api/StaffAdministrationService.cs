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

public sealed class StaffAdministrationService(CoffeeShopDbContext db, TimeProvider time, GoogleAdminOptions google)
{
    private const int MaximumPageSize = 100;
    private const int AccountAdministrationLockId = 228001;

    public async Task<Result<StaffAccountResponse>> CreateAsync(
        CreateStaffRequest request, EntityId actorId, string? ipAddress, CancellationToken ct)
    {
        var username = request.Username?.Trim();
        var email = request.Email?.Trim().ToLowerInvariant();
        var fullName = request.FullName?.Trim();
        var phone = request.Phone?.Trim();
        var roleCode = request.Role?.Trim();
        if (google.Enabled && roleCode == "Admin")
            return Result<StaffAccountResponse>.Failure(Error.Validation(
                "staff.google_admin_provisioning_required", "Google administrators must be provisioned by the deployment operator."));
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
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({AccountAdministrationLockId})", ct);
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
        return users.Select(user => ToResponse(user)).ToArray();
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

    public async Task<Result<StaffAccountResponse>> UpdateAsync(
        Guid id, UpdateStaffRequest request, EntityId actorId, string? ipAddress, CancellationToken ct)
    {
        var username = request.Username?.Trim();
        var email = request.Email?.Trim().ToLowerInvariant();
        var fullName = request.FullName?.Trim();
        var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        var roleCodes = request.Roles?.Select(role => role?.Trim()).ToArray();
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(username) || username.Length > 50 ||
            string.IsNullOrWhiteSpace(fullName) || fullName.Length > 150 ||
            string.IsNullOrWhiteSpace(email) || email.Length > 255 ||
            !MailAddress.TryCreate(email, out var address) || address.Address != email ||
            phone?.Length > 30 || roleCodes is null || roleCodes.Length is 0 or > 6 ||
            roleCodes.Any(string.IsNullOrWhiteSpace) ||
            roleCodes.Distinct(StringComparer.Ordinal).Count() != roleCodes.Length ||
            roleCodes.Any(role => !StaffRoleSeed.Bundles.ContainsKey(role!)))
            return Result<StaffAccountResponse>.Failure(Error.Validation(
                "staff.invalid_profile", "Enter a valid account profile and one or more unique operational roles."));

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockAdministrationStateAsync(id, ct);
        var user = await db.Users.Include(item => item.UserRoles).ThenInclude(item => item.Role)
            .SingleOrDefaultAsync(item => item.Id == new EntityId(id), ct);
        if (user is null)
            return Result<StaffAccountResponse>.Failure(Error.NotFound("staff.not_found", "The staff account was not found."));

        var normalizedUsername = username.ToLowerInvariant();
        if (await db.Users.AnyAsync(candidate => candidate.Id != user.Id &&
            (candidate.Username.ToLower() == normalizedUsername || candidate.Email == email), ct))
            return Result<StaffAccountResponse>.Failure(Error.Conflict(
                "staff.duplicate_identity", "A staff account already uses that username or email."));

        var currentRoleCodes = user.UserRoles.Select(item => item.Role!.Code).ToHashSet(StringComparer.Ordinal);
        var newRoleCodes = roleCodes.Select(role => role!).ToHashSet(StringComparer.Ordinal);
        var rolesChanged = !currentRoleCodes.SetEquals(newRoleCodes);
        if (google.Enabled && !currentRoleCodes.Contains("Admin") && newRoleCodes.Contains("Admin"))
            return Result<StaffAccountResponse>.Failure(Error.Validation(
                "staff.google_admin_provisioning_required", "Google administrators must be provisioned by the deployment operator."));

        var administratorError = await CheckAdministratorProtectionAsync(
            user, currentRoleCodes, newRoleCodes, email, ct);
        if (administratorError is not null)
            return Result<StaffAccountResponse>.Failure(administratorError);

        var rolesByCode = await db.Roles.Where(role => roleCodes.Contains(role.Code))
            .ToDictionaryAsync(role => role.Code, StringComparer.Ordinal, ct);
        if (rolesByCode.Count != newRoleCodes.Count)
            return Result<StaffAccountResponse>.Failure(Error.Validation(
                "staff.invalid_role", "One or more selected operational roles are unavailable."));

        var profileChanged = user.Username != username || user.Email != email || user.FullName != fullName || user.Phone != phone;
        if (!profileChanged && !rolesChanged)
        {
            await transaction.CommitAsync(ct);
            return Result<StaffAccountResponse>.Success(ToResponse(user));
        }

        var now = new UtcTimestamp(time.GetUtcNow());
        if (profileChanged)
        {
            user.UpdateProfile(username, email, fullName, phone, now);
            db.SecurityAuditEvents.Add(new SecurityAuditEvent(EntityId.New(), "account_updated", now,
                "success", user.Id, ipAddress, JsonSerializer.Serialize(new { actorId = actorId.Value })));
        }
        if (rolesChanged)
        {
            var previousRoles = currentRoleCodes.Order(StringComparer.Ordinal).ToArray();
            db.UserRoles.RemoveRange(user.UserRoles);
            foreach (var roleCode in newRoleCodes.Order(StringComparer.Ordinal))
                db.UserRoles.Add(new UserRole(user.Id, rolesByCode[roleCode].Id, now, actorId));
            db.SecurityAuditEvents.Add(new SecurityAuditEvent(EntityId.New(), "role_changed", now,
                "success", user.Id, ipAddress, JsonSerializer.Serialize(new
                {
                    actorId = actorId.Value,
                    previousRoles,
                    roles = newRoleCodes.Order(StringComparer.Ordinal).ToArray(),
                })));
        }

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
        return Result<StaffAccountResponse>.Success(ToResponse(user, newRoleCodes));
    }

    public async Task<Result<StaffAccountResponse>> ReactivateAsync(
        Guid id, bool confirmed, EntityId actorId, string? ipAddress, CancellationToken ct)
    {
        if (!confirmed)
            return Result<StaffAccountResponse>.Failure(Error.Validation(
                "staff.reactivation_confirmation_required", "Explicit confirmation is required."));
        if (id == Guid.Empty)
            return Result<StaffAccountResponse>.Failure(Error.Validation(
                "staff.invalid_id", "A nonempty account identifier is required."));

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockAdministrationStateAsync(id, ct);
        var user = await db.Users.Include(item => item.UserRoles).ThenInclude(item => item.Role)
            .SingleOrDefaultAsync(item => item.Id == new EntityId(id), ct);
        if (user is null)
            return Result<StaffAccountResponse>.Failure(Error.NotFound("staff.not_found", "The staff account was not found."));
        if (user.IsActive)
            return Result<StaffAccountResponse>.Failure(Error.Conflict("staff.already_active", "The staff account is already active."));
        if (google.Enabled && user.UserRoles.Any(item => item.Role?.Code == "Admin") &&
            google.SessionBinding(user.Id.Value, user.Email) is null)
            return Result<StaffAccountResponse>.Failure(Error.Validation(
                "staff.google_admin_provisioning_required", "Google administrators must be provisioned by the deployment operator."));

        var now = new UtcTimestamp(time.GetUtcNow());
        user.Reactivate(now);
        db.SecurityAuditEvents.Add(new SecurityAuditEvent(EntityId.New(), "account_reactivated", now,
            "success", user.Id, ipAddress, JsonSerializer.Serialize(new { actorId = actorId.Value })));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result<StaffAccountResponse>.Success(ToResponse(user));
    }

    public async Task<Result<StaffAccountResponse>> DeactivateAsync(
        Guid id, bool confirmed, EntityId actorId, string? ipAddress, CancellationToken ct)
    {
        if (!confirmed)
            return Result<StaffAccountResponse>.Failure(Error.Validation(
                "staff.deactivation_confirmation_required", "Explicit confirmation is required."));
        if (id == Guid.Empty)
            return Result<StaffAccountResponse>.Failure(Error.Validation(
                "staff.invalid_id", "A nonempty account identifier is required."));

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockAdministrationStateAsync(id, ct);

        var user = await db.Users.Include(item => item.UserRoles).ThenInclude(item => item.Role)
            .SingleOrDefaultAsync(item => item.Id == new EntityId(id), ct);
        if (user is null)
            return Result<StaffAccountResponse>.Failure(Error.NotFound("staff.not_found", "The staff account was not found."));
        if (!user.IsActive)
            return Result<StaffAccountResponse>.Failure(Error.Conflict("staff.already_inactive", "The staff account is already inactive."));

        var isAdministrator = user.UserRoles.Any(role => role.Role?.Code == "Admin");
        if (isAdministrator)
        {
            var remainingAdmins = await db.Users.Where(candidate => candidate.IsActive && candidate.Id != user.Id &&
                candidate.UserRoles.Any(role => role.Role!.Code == "Admin"))
                .Select(candidate => new { candidate.Id, candidate.Email }).ToArrayAsync(ct);
            if (remainingAdmins.Length == 0 || (google.SessionBinding(user.Id.Value, user.Email) is not null &&
                !remainingAdmins.Any(candidate => google.SessionBinding(candidate.Id.Value, candidate.Email) is not null)))
                return Result<StaffAccountResponse>.Failure(Error.Conflict(
                    "staff.final_administrator", "The final active administrator cannot be deactivated."));
        }

        var now = new UtcTimestamp(time.GetUtcNow());
        user.Deactivate(now);
        var sessions = await db.RefreshTokens.Where(token => token.UserId == user.Id && token.RevokedAt == null)
            .ToArrayAsync(ct);
        foreach (var session in sessions) session.Revoke(now);
        db.SecurityAuditEvents.Add(new SecurityAuditEvent(EntityId.New(), "account_deactivated", now,
            "success", user.Id, ipAddress, JsonSerializer.Serialize(new { actorId = actorId.Value })));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result<StaffAccountResponse>.Success(ToResponse(user));
    }

    private async Task<Error?> CheckAdministratorProtectionAsync(
        User user, HashSet<string> currentRoleCodes, HashSet<string> newRoleCodes, string newEmail, CancellationToken ct)
    {
        if (!user.IsActive) return null;
        var wasAdministrator = currentRoleCodes.Contains("Admin");
        var remainsAdministrator = newRoleCodes.Contains("Admin");
        var wasLinkedAdministrator = wasAdministrator && google.SessionBinding(user.Id.Value, user.Email) is not null;
        var remainsLinkedAdministrator = remainsAdministrator && google.SessionBinding(user.Id.Value, newEmail) is not null;
        if ((!wasAdministrator || remainsAdministrator) && (!wasLinkedAdministrator || remainsLinkedAdministrator))
            return null;

        var otherAdministrators = await db.Users.AsNoTracking()
            .Where(candidate => candidate.IsActive && candidate.Id != user.Id &&
                candidate.UserRoles.Any(role => role.Role!.Code == "Admin"))
            .Select(candidate => new { candidate.Id, candidate.Email })
            .ToArrayAsync(ct);
        if (wasAdministrator && !remainsAdministrator && otherAdministrators.Length == 0)
            return Error.Conflict("staff.final_administrator", "The final active administrator cannot be removed or demoted.");
        if (wasLinkedAdministrator && !remainsLinkedAdministrator && !otherAdministrators.Any(candidate =>
            google.SessionBinding(candidate.Id.Value, candidate.Email) is not null))
            return Error.Conflict("staff.final_administrator", "The final linked active administrator cannot be demoted or unlinked.");
        return null;
    }

    private async Task LockAdministrationStateAsync(Guid id, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({AccountAdministrationLockId})", ct);
        // Serialize account creation, reactivation, deactivation, and role changes while
        // checking the last active administrator and configured Google link invariants.
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
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM identity.users WHERE id = {id} FOR UPDATE", ct);
    }

    private static StaffAccountResponse ToResponse(User user, IEnumerable<string>? roleCodes = null) => new(
        user.Id.Value, user.Username, user.FullName, user.Email, user.Phone, user.IsActive,
        (roleCodes ?? user.UserRoles.Select(role => role.Role!.Code)).Order(StringComparer.Ordinal).ToArray(),
        user.CreatedAt.Value, user.DeactivatedAt?.Value);

    private static int NormalizeLimit(int? limit) => Math.Clamp(limit ?? 50, 1, MaximumPageSize);
}
