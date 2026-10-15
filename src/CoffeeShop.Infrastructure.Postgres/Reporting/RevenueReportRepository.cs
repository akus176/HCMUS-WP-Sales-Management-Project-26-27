namespace CoffeeShop.Infrastructure.Postgres.Reporting;

using CoffeeShop.Application.Reporting;
using CoffeeShop.Domain.Common;
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
        var candidateOrderIds = (await context.Database
            .SqlQuery<Guid>($@"
                SELECT orders.id AS ""Value""
                FROM sales.orders AS orders
                WHERE orders.status = 'completed'
                  AND orders.completed_at IS NOT NULL
                  AND EXISTS (
                      SELECT 1
                      FROM sales.payments AS payments
                      WHERE payments.order_id = orders.id
                        AND payments.status = 'succeeded'
                        AND payments.paid_at < {period.EndUtcExclusive})")
            .ToListAsync(cancellationToken))
            .Select(value => new EntityId(value))
            .ToArray();

        // Materialize entities through the mapped table rather than FromSql. Orders use PostgreSQL's
        // xmin concurrency token, which is not exposed by derived-table projections on PostgreSQL 18.
        var orders = await context.Orders
            .AsNoTracking()
            .Where(order => candidateOrderIds.Contains(order.Id))
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
