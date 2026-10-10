namespace CoffeeShop.Application.Tests.Testing;

using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public sealed class FakePaymentRepository : IPaymentRepository
{
    public Dictionary<EntityId, Order> Orders { get; } = [];

    public Dictionary<EntityId, Payment> OtherPayments { get; } = [];

    public int OrderLockCount { get; private set; }

    public Task<Order?> LoadOrderForPaymentUpdateAsync(
        EntityId orderId,
        CancellationToken cancellationToken = default)
    {
        OrderLockCount++;
        return Task.FromResult(Orders.GetValueOrDefault(orderId));
    }

    public Task<Payment?> FindPaymentAsync(
        EntityId paymentId,
        CancellationToken cancellationToken = default)
    {
        var payment = Orders.Values.SelectMany(order => order.Payments)
            .FirstOrDefault(item => item.Id == paymentId)
            ?? OtherPayments.GetValueOrDefault(paymentId);
        return Task.FromResult(payment);
    }
}
