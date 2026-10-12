namespace CoffeeShop.Application.Sales;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public interface IPaymentRepository
{
    void AddPayment(Payment payment);

    Task<Order?> LoadOrderForPaymentUpdateAsync(
        EntityId orderId,
        CancellationToken cancellationToken = default);

    Task<Payment?> FindPaymentAsync(
        EntityId paymentId,
        CancellationToken cancellationToken = default);
}
