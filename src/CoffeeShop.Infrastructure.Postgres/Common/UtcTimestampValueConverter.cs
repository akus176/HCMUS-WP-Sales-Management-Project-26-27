namespace CoffeeShop.Infrastructure.Postgres.Common;

using CoffeeShop.Domain.Common;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

public sealed class UtcTimestampValueConverter : ValueConverter<UtcTimestamp, DateTimeOffset>
{
    public UtcTimestampValueConverter()
        : base(
            timestamp => timestamp.Value,
            value => new UtcTimestamp(value))
    {
    }
}
