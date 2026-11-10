namespace CoffeeShop.Api;

using System.Net.Http.Json;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;
using CoffeeShop.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

public sealed record StaffPushTarget(string Kind, Guid RecordId);

public sealed record StaffPushNotification(StaffPushTarget Data)
{
    public string Title => "Coffee Shop Staff";
    public string Body => "Open the app to review a staff update.";
}

public enum PushDeliveryStatus { Sent, InvalidDevice, Failed }

public interface IMobilePushProvider
{
    Task<PushDeliveryStatus> SendAsync(string destination, StaffPushNotification notification, CancellationToken cancellationToken);
}

internal sealed class ExpoMobilePushProvider(HttpClient http) : IMobilePushProvider
{
    private sealed record TicketDetails(string? Error);
    private sealed record Ticket(string? Status, TicketDetails? Details);
    private sealed record Envelope(Ticket? Data);

    public async Task<PushDeliveryStatus> SendAsync(string destination, StaffPushNotification notification, CancellationToken cancellationToken)
    {
        if (!destination.EndsWith(']') ||
            (!destination.StartsWith("ExpoPushToken[", StringComparison.Ordinal) &&
             !destination.StartsWith("ExponentPushToken[", StringComparison.Ordinal)))
            return PushDeliveryStatus.InvalidDevice;

        using var response = await http.PostAsJsonAsync("https://exp.host/--/api/v2/push/send", new
        {
            to = destination,
            title = notification.Title,
            body = notification.Body,
            data = notification.Data,
            channelId = "staff-updates"
        }, cancellationToken);
        if (!response.IsSuccessStatusCode) return PushDeliveryStatus.Failed;
        var ticket = (await response.Content.ReadFromJsonAsync<Envelope>(cancellationToken))?.Data;
        if (ticket?.Details?.Error == "DeviceNotRegistered") return PushDeliveryStatus.InvalidDevice;
        return ticket?.Status == "ok" ? PushDeliveryStatus.Sent : PushDeliveryStatus.Failed;
    }
}

public sealed class MobilePushDispatch(
    CoffeeShopDbContext db, InvalidMobileTokenCleanup cleanup, ILogger<MobilePushDispatch> logger)
{
    public async Task DispatchAsync(IMobilePushProvider provider, OperationalEvent item, CancellationToken cancellationToken)
    {
        var id = new EntityId(item.Id);
        switch (item.Type)
        {
            case "discount":
                var discounts = await db.Discounts.AsNoTracking()
                    .Where(discount => discount.OrderId == id && discount.Status == DiscountStatus.Pending)
                    .Select(discount => discount.Id).ToArrayAsync(cancellationToken);
                foreach (var discount in discounts)
                    await SendAsync(provider, "discounts.approve", new("approval", discount.Value), null, cancellationToken);
                break;
            case "delivery":
                var courier = await db.Deliveries.AsNoTracking()
                    .Where(delivery => delivery.OrderId == id && delivery.Status == DeliveryStatus.Assigned)
                    .Select(delivery => delivery.AssignedStaffId).SingleOrDefaultAsync(cancellationToken);
                if (courier is not null)
                    await SendAsync(provider, "delivery.update", new("assignment", item.Id), courier, cancellationToken);
                break;
            case "inventory":
                if (await db.Ingredients.AsNoTracking().AnyAsync(ingredient => ingredient.Id == id && ingredient.IsActive &&
                    ingredient.AvailableQuantity <= ingredient.ReorderThreshold, cancellationToken))
                    await SendAsync(provider, "inventory.manage", new("low-stock", item.Id), null, cancellationToken);
                break;
            case "order":
                if (await db.Orders.AsNoTracking().AnyAsync(order => order.Id == id && order.Status == OrderStatus.Confirmed, cancellationToken))
                    await SendAsync(provider, "orders.fulfill", new("urgent-order", item.Id), null, cancellationToken);
                break;
        }
    }

    private async Task SendAsync(IMobilePushProvider provider, string permission, StaffPushTarget target,
        EntityId? onlyUser, CancellationToken cancellationToken)
    {
        var tokens = await db.MobileDeviceRegistrations.AsNoTracking()
            .Where(device => device.RevokedAt == null && (onlyUser == null || device.UserId == onlyUser))
            .Where(device => db.UserRoles.Any(link => link.UserId == device.UserId && link.User!.IsActive &&
                link.Role!.RolePermissions.Any(rolePermission => rolePermission.Permission!.Code == permission)))
            .Select(device => device.Token).ToArrayAsync(cancellationToken);
        var notification = new StaffPushNotification(target);
        foreach (var token in tokens)
        {
            try
            {
                var result = await provider.SendAsync(token, notification, cancellationToken);
                if (result == PushDeliveryStatus.InvalidDevice) await cleanup.RevokeAsync(token, cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Unable to send a staff push notification");
            }
        }
    }
}
