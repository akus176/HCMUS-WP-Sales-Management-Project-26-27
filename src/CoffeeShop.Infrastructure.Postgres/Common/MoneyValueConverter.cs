namespace CoffeeShop.Infrastructure.Postgres.Common;

using CoffeeShop.Domain.Common;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

public sealed class MoneyValueConverter : ValueConverter<Money, decimal>
{
    public MoneyValueConverter()
        : base(
            money => money.Amount,
            value => new Money(value))
    {
    }
}
