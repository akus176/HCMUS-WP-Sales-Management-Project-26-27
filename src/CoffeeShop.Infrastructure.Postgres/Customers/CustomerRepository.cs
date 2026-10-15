namespace CoffeeShop.Infrastructure.Postgres.Customers;

using CoffeeShop.Application.Customers;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Customers;
using CoffeeShop.Domain.Sales;
using Microsoft.EntityFrameworkCore;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly CoffeeShopDbContext context;

    public CustomerRepository(CoffeeShopDbContext context)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<Customer?> FindCustomerAsync(EntityId id, CancellationToken cancellationToken = default) =>
        context.Customers.Include(customer => customer.Addresses)
            .FirstOrDefaultAsync(customer => customer.Id == id, cancellationToken);

    public Task<bool> PhoneExistsAsync(
        string normalizedPhone,
        EntityId? excludingCustomerId = null,
        CancellationToken cancellationToken = default) =>
        context.Customers.AnyAsync(customer => customer.Phone == normalizedPhone
            && (excludingCustomerId == null || customer.Id != excludingCustomerId), cancellationToken);

    public async Task<Order?> LoadOrderForLoyaltyUpdateAsync(
        EntityId orderId,
        CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM sales.orders WHERE id = {orderId.Value} FOR UPDATE", cancellationToken);
        return await context.Orders.FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);
    }

    public async Task<Customer?> LoadCustomerForLoyaltyUpdateAsync(
        EntityId customerId,
        CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM customers.customers WHERE id = {customerId.Value} FOR UPDATE", cancellationToken);
        var customer = await context.Customers.Include(customer => customer.LoyaltyTransactions)
            .FirstOrDefaultAsync(customer => customer.Id == customerId, cancellationToken);
        if (customer is not null)
        {
            await context.Entry(customer).ReloadAsync(cancellationToken);
        }

        return customer;
    }

    public async Task<LoyaltySettings?> LoadLoyaltySettingsForUpdateAsync(
        CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        await context.Database.ExecuteSqlRawAsync(
            "LOCK TABLE customers.loyalty_settings IN SHARE ROW EXCLUSIVE MODE", cancellationToken);
        return await context.LoyaltySettings.SingleOrDefaultAsync(cancellationToken);
    }

    public void AddCustomer(Customer customer) => context.Customers.Add(customer);

    public void AddLoyaltySettings(LoyaltySettings settings) => context.LoyaltySettings.Add(settings);

    private void RequireTransaction()
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Loyalty updates require an explicit database transaction.");
        }
    }
}
