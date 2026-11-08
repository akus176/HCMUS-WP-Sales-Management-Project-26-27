namespace CoffeeShop.Api;

using System.Text.Json;
using System.Threading.Channels;
using CoffeeShop.Domain.Common;
using CoffeeShop.Infrastructure.Postgres;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Npgsql;

public sealed record OperationalEvent(string Type, Guid Id, DateTimeOffset UpdatedAt);

/// <summary>PostgreSQL delivers NOTIFY only after commit; events are invalidation hints, not a durable queue.</summary>
public sealed class OperationalEventListener(
    IConfiguration configuration, IServiceScopeFactory scopes, IHubContext<OperationalEventsHub> hub,
    ILogger<OperationalEventListener> logger) : BackgroundService
{
    private static readonly IReadOnlyDictionary<string, string[]> Permissions = new Dictionary<string, string[]>
    {
        ["order"] = ["orders.create", "orders.confirm", "orders.cancel", "orders.complete", "orders.fulfill"],
        ["discount"] = ["discounts.request", "discounts.approve"],
        ["payment"] = ["payments.record"],
        ["inventory"] = ["inventory.receive", "inventory.adjust", "inventory.manage", "reports.lowstock"],
        ["delivery"] = ["delivery.assign"],
        ["account"] = ["users.manage"],
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = new NpgsqlConnection(configuration.GetConnectionString("CoffeeShop"));
                await connection.OpenAsync(stoppingToken);
                var messages = Channel.CreateUnbounded<string>();
                connection.Notification += (_, e) => messages.Writer.TryWrite(e.Payload);
                await using (var listen = new NpgsqlCommand("LISTEN coffeeshop_operational_events", connection))
                    await listen.ExecuteNonQueryAsync(stoppingToken);
                while (!stoppingToken.IsCancellationRequested)
                {
                    await connection.WaitAsync(stoppingToken);
                    while (messages.Reader.TryRead(out var payload))
                    {
                        try { await DispatchAsync(payload, stoppingToken); }
                        catch (Exception e) when (e is not OperationCanceledException)
                        { logger.LogInformation(e, "Operational event dispatch failed"); }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e)
            {
                logger.LogInformation(e, "Operational event listener disconnected; reconnecting");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task DispatchAsync(string payload, CancellationToken cancellationToken)
    {
        var item = JsonSerializer.Deserialize<OperationalEvent>(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (item is null || item.Id == Guid.Empty || !Permissions.TryGetValue(item.Type, out var permitted)) return;
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        // Membership is re-evaluated at dispatch time; stale hub groups cannot retain access.
        var recipients = await db.UserRoles.AsNoTracking()
            .Where(link => link.User!.IsActive && link.Role!.RolePermissions.Any(rp => permitted.Contains(rp.Permission!.Code)))
            .Select(link => link.UserId).Distinct().ToListAsync(cancellationToken);
        if (item.Type == "delivery")
        {
            var orderId = new EntityId(item.Id);
            var courier = await db.Deliveries.AsNoTracking().Where(d => d.OrderId == orderId && d.AssignedStaffId != null)
                .Select(d => d.AssignedStaffId).SingleOrDefaultAsync(cancellationToken);
            if (courier is not null && await db.UserRoles.AsNoTracking().AnyAsync(link => link.UserId == courier && link.User!.IsActive &&
                link.Role!.RolePermissions.Any(rp => rp.Permission!.Code == "delivery.update"), cancellationToken))
                recipients.Add(courier);
        }
        if (item.Type == "account")
        {
            var account = new EntityId(item.Id);
            if (await db.Users.AsNoTracking().AnyAsync(user => user.Id == account && user.IsActive, cancellationToken)) recipients.Add(account);
        }
        foreach (var recipient in recipients.Distinct())
            await hub.Clients.Group(OperationalEventsHub.UserGroup(recipient.Value)).SendAsync("OperationalEvent", item, cancellationToken);
    }
}
