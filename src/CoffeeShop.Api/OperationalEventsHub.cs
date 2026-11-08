namespace CoffeeShop.Api;

using CoffeeShop.Domain.Common;
using CoffeeShop.Infrastructure.Postgres;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[Authorize]
public sealed class OperationalEventsHub(CoffeeShopDbContext db) : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (!Guid.TryParse(Context.User?.FindFirstValue("sub"), out var id) || id == Guid.Empty)
        {
            Context.Abort();
            return;
        }
        var userId = new EntityId(id);
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(id));
        foreach (var permission in Context.User!.FindAll("permission").Select(claim => claim.Value).Distinct())
            await Groups.AddToGroupAsync(Context.ConnectionId, PermissionGroup(permission));
        if (Context.User.HasClaim("permission", "delivery.update"))
        {
            var assignments = await db.Deliveries.AsNoTracking().Where(delivery => delivery.AssignedStaffId == userId)
                .Select(delivery => delivery.OrderId).ToArrayAsync(Context.ConnectionAborted);
            foreach (var assignment in assignments)
                await Groups.AddToGroupAsync(Context.ConnectionId, AssignmentGroup(assignment.Value));
        }
        await base.OnConnectedAsync();
    }

    internal static string UserGroup(Guid id) => $"user:{id:D}";
    internal static string PermissionGroup(string permission) => $"permission:{permission}";
    internal static string AssignmentGroup(Guid id) => $"assignment:{id:D}";
}
