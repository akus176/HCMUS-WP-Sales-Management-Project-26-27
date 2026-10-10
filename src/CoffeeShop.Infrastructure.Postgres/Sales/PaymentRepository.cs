namespace CoffeeShop.Infrastructure.Postgres.Sales;

using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;
using Microsoft.EntityFrameworkCore;

public sealed class PaymentRepository : IPaymentRepository
{
    private readonly CoffeeShopDbContext context;

    public PaymentRepository(CoffeeShopDbContext context)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Order?> LoadOrderForPaymentUpdateAsync(
        EntityId orderId,
        CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM sales.orders WHERE id = {orderId.Value} FOR UPDATE", cancellationToken);
        return await context.Orders
            .Include(order => order.Payments)
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);
    }

    public Task<Payment?> FindPaymentAsync(
        EntityId paymentId,
        CancellationToken cancellationToken = default) =>
        context.Payments.FirstOrDefaultAsync(payment => payment.Id == paymentId, cancellationToken);

    private void RequireTransaction()
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Payment updates require an explicit database transaction.");
        }
    }
}
