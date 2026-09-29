namespace CoffeeShop.Infrastructure.Postgres.Common;

using CoffeeShop.Domain.Common;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

public sealed class EntityIdValueConverter : ValueConverter<EntityId, Guid>
{
    public EntityIdValueConverter()
        : base(
            id => id.Value,
            value => new EntityId(value))
    {
    }
}
