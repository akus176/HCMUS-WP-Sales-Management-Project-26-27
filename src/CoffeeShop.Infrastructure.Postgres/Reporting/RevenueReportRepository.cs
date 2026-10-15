namespace CoffeeShop.Infrastructure.Postgres.Reporting;

using CoffeeShop.Application.Reporting;
using Microsoft.EntityFrameworkCore;

public sealed class RevenueReportRepository(CoffeeShopDbContext context) : IRevenueReportRepository
{
    public async Task<IReadOnlyList<RevenueOrderCandidate>> LoadCandidatesAsync(
        RevenuePeriod period,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(period);

        // The start is deliberately not applied in SQL: earlier successful payments can consume part
        // of the order's discounted cap and therefore change the amount recognized inside this period.
        var orders = await context.Orders
            .FromSqlInterpolated($@"
                SELECT orders.*
                FROM sales.orders AS orders
                WHERE orders.status = 'completed'
                  AND orders.completed_at IS NOT NULL
                  AND EXISTS (
                      SELECT 1
                      FROM sales.payments AS payments
                      WHERE payments.order_id = orders.id
                        AND payments.status = 'succeeded'
                        AND payments.paid_at < {period.EndUtcExclusive})")
            .AsNoTracking()
            .Include(order => order.Payments)
            .ToListAsync(cancellationToken);

        var restoredOrderIds = (await context.OrderInventoryRestorations
                .AsNoTracking()
                .Select(restoration => restoration.OrderId)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        return orders
            .Select(order => new RevenueOrderCandidate(order, restoredOrderIds.Contains(order.Id)))
            .ToArray();
    }
}
