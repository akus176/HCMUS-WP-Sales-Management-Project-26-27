namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Domain.Sales;
using CoffeeShop.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

public class SalesMappingTests
{
    private static IModel CreateModel()
    {
        var options = new DbContextOptionsBuilder<CoffeeShopDbContext>()
            .UseNpgsql("Host=localhost;Database=coffeeshop_test")
            .Options;

        using var context = new CoffeeShopDbContext(options);
        return context.GetService<IDesignTimeModel>().Model;
    }

    [Theory]
    [InlineData(typeof(DiningTable), "tables")]
    [InlineData(typeof(Order), "orders")]
    [InlineData(typeof(OrderStatusHistory), "order_status_history")]
    [InlineData(typeof(OrderLine), "order_lines")]
    [InlineData(typeof(OrderLineAddon), "order_line_addons")]
    [InlineData(typeof(Discount), "discounts")]
    [InlineData(typeof(Payment), "payments")]
    [InlineData(typeof(Delivery), "deliveries")]
    [InlineData(typeof(Reservation), "reservations")]
    public void SalesEntities_MapToExplicitSalesTablesWithUuidKeys(Type type, string table)
    {
        var entity = CreateModel().FindEntityType(type)!;
        var key = Assert.Single(entity.FindPrimaryKey()!.Properties);

        Assert.Equal(table, entity.GetTableName());
        Assert.Equal("sales", entity.GetSchema());
        Assert.Equal("id", key.GetColumnName());
        Assert.Equal("uuid", key.GetColumnType());
        Assert.Equal("gen_random_uuid()", key.GetDefaultValueSql());
    }

    [Theory]
    [InlineData(nameof(Order.Subtotal))]
    [InlineData(nameof(Order.DiscountTotal))]
    [InlineData(nameof(Order.Total))]
    [InlineData(nameof(Order.PaidAmount))]
    [InlineData(nameof(Order.LoyaltyDiscountAmount))]
    public void OrderMoneyColumns_UseTheSharedMoneyPrecision(string propertyName)
    {
        var property = CreateModel().FindEntityType(typeof(Order))!.FindProperty(propertyName)!;

        Assert.Equal(12, property.GetPrecision());
        Assert.Equal(2, property.GetScale());
        Assert.Equal("numeric(12,2)", property.GetColumnType());
        Assert.False(property.IsNullable);
    }

    [Fact]
    public void SnapshotsAndSalesIndexes_PreserveHistoryAndLookupPerformance()
    {
        var model = CreateModel();
        var order = model.FindEntityType(typeof(Order))!;
        var line = model.FindEntityType(typeof(OrderLine))!;
        var delivery = model.FindEntityType(typeof(Delivery))!;
        var reservation = model.FindEntityType(typeof(Reservation))!;

        Assert.True(Assert.Single(order.GetIndexes(), index => index.Properties.Select(property => property.Name).SequenceEqual([nameof(Order.OrderNumber)])).IsUnique);
        Assert.Equal(120, line.FindProperty(nameof(OrderLine.ItemNameSnapshot))!.GetMaxLength());
        Assert.Equal(40, line.FindProperty(nameof(OrderLine.SizeNameSnapshot))!.GetMaxLength());
        Assert.True(Assert.Single(delivery.GetIndexes(), index => index.Properties.Select(property => property.Name).SequenceEqual([nameof(Delivery.OrderId)])).IsUnique);
        Assert.Contains(reservation.GetIndexes(), index => index.Properties.Select(property => property.Name).SequenceEqual([nameof(Reservation.TableId), nameof(Reservation.ReservedFor)]));
        var reservationSlotIndex = Assert.Single(reservation.GetIndexes(), index => index.Properties.Select(property => property.Name).SequenceEqual([nameof(Reservation.TableId), nameof(Reservation.ReservedFor)]));
        Assert.True(reservationSlotIndex.IsUnique);
        Assert.Equal("status NOT IN ('cancelled', 'completed')", reservationSlotIndex.GetFilter());
    }

    [Fact]
    public void CriticalRelationships_UseExplicitDeleteBehavior()
    {
        var model = CreateModel();
        var orderLine = model.FindEntityType(typeof(OrderLine))!;
        var lineOrder = Assert.Single(orderLine.GetForeignKeys(), foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Order));
        var delivery = model.FindEntityType(typeof(Delivery))!;
        var deliveryOrder = Assert.Single(delivery.GetForeignKeys(), foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Order));
        var reservation = model.FindEntityType(typeof(Reservation))!;
        var reservationTable = Assert.Single(reservation.GetForeignKeys(), foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(DiningTable));

        Assert.Equal(DeleteBehavior.Cascade, lineOrder.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, deliveryOrder.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, reservationTable.DeleteBehavior);
    }

    [Fact]
    public void StatusAndMonetaryConstraints_ArePresentInTheGeneratedModel()
    {
        var model = CreateModel();

        Assert.Contains(model.FindEntityType(typeof(Order))!.GetCheckConstraints(), constraint => constraint.Name == "CK_orders_status");
        Assert.Contains(model.FindEntityType(typeof(OrderLine))!.GetCheckConstraints(), constraint => constraint.Name == "CK_order_lines_quantity");
        Assert.Contains(model.FindEntityType(typeof(Payment))!.GetCheckConstraints(), constraint => constraint.Name == "CK_payments_amount");
        Assert.Contains(model.FindEntityType(typeof(Payment))!.GetCheckConstraints(), constraint => constraint.Name == "CK_payments_required_reference");
        Assert.Contains(model.FindEntityType(typeof(Delivery))!.GetCheckConstraints(), constraint => constraint.Name == "CK_deliveries_status");
        Assert.Contains(model.FindEntityType(typeof(Reservation))!.GetCheckConstraints(), constraint => constraint.Name == "CK_reservations_party_size");
    }

    [Fact]
    public void Payments_UseOrderNavigationSharedMoneyPrecisionAndBoundedReference()
    {
        var payment = CreateModel().FindEntityType(typeof(Payment))!;
        var amount = payment.FindProperty(nameof(Payment.Amount))!;
        var orderRelationship = Assert.Single(
            payment.GetForeignKeys(),
            foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Order));

        Assert.Equal(12, amount.GetPrecision());
        Assert.Equal(2, amount.GetScale());
        Assert.Equal(100, payment.FindProperty(nameof(Payment.Reference))!.GetMaxLength());
        Assert.Equal(nameof(Order.Payments), orderRelationship.PrincipalToDependent!.Name);
        Assert.Equal(DeleteBehavior.Cascade, orderRelationship.DeleteBehavior);
    }

    [Fact]
    public void Tables_UseOptimisticConcurrencyAndPartialActiveOrderUniqueness()
    {
        var table = CreateModel().FindEntityType(typeof(DiningTable))!;
        var version = table.FindProperty(nameof(DiningTable.Version))!;
        var activeOrderIndex = Assert.Single(table.GetIndexes(), index => index.Properties.Select(property => property.Name).SequenceEqual([nameof(DiningTable.ActiveOrderId)]));

        Assert.True(version.IsConcurrencyToken);
        Assert.Equal("xmin", version.GetColumnName());
        Assert.True(activeOrderIndex.IsUnique);
        Assert.Equal("active_order_id IS NOT NULL", activeOrderIndex.GetFilter());
    }
}
