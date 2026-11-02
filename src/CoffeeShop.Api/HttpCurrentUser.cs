namespace CoffeeShop.Api;

using System.Security.Claims;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Domain.Common;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public EntityId? Id
    {
        get
        {
            var value = Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Principal?.FindFirstValue("sub");
            return Guid.TryParse(value, out var id) && id != Guid.Empty ? new EntityId(id) : null;
        }
    }

    public string? Username => Principal?.Identity?.Name;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true && Id is not null;

    public IReadOnlyCollection<string> Roles => Principal?.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray() ?? [];

    public IReadOnlyCollection<string> Permissions => Principal?.FindAll("permission").Select(claim => claim.Value).ToArray() ?? [];

    public bool HasPermission(string permission) => IsAuthenticated && Permissions.Contains(permission, StringComparer.Ordinal);

    public bool IsInRole(string role) => IsAuthenticated && Roles.Contains(role, StringComparer.Ordinal);

    public EntityId GetRequiredId() => Id ?? throw new UnauthorizedAccessException("Current user is not authenticated.");
}
