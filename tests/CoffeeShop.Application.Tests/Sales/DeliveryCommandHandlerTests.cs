namespace CoffeeShop.Application.Tests.Sales;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Sales;
using CoffeeShop.Application.Tests.Testing;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public sealed class DeliveryCommandHandlerTests
{
    private static readonly UtcTimestamp Now = new(DateTimeOffset.UtcNow);

    [Theory]
    [InlineData("assign")]
    [InlineData("start")]
    [InlineData("complete")]
    [InlineData("fail")]
    public async Task CancelledOrder_RejectsEveryDeliveryOperationWithoutSaving(string operation)
    {
        var order = new StoredOrder(OrderStatus.Cancelled);
        var delivery = Delivery.CreateOrderSpecific(EntityId.New(), order.Id, "Alice", "0901234567", "123 Main Street");
        var actorId = EntityId.New();
        var repository = new DeliveryRepository(order, delivery);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeliveryCommandHandler(repository, unitOfWork, new TestClock(Now.Value), new TestTransactionManager());

        var result = operation switch
        {
            "assign" => await handler.AssignAsync(new(order.Id, actorId, actorId)),
            "start" => await handler.StartAsync(new(order.Id, actorId)),
            "complete" => await handler.CompleteAsync(new(order.Id, actorId)),
            _ => await handler.FailAsync(new(order.Id, "Failed", actorId))
        };

        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
        Assert.Empty(delivery.StatusHistory);
        Assert.Equal(0, repository.DeliveryReadCount);
        Assert.Equal(1, repository.OrderLockCount);
    }

    [Fact]
    public async Task ActiveOrder_CanAssignStartAndCompleteWithHistory()
    {
        var order = new StoredOrder(OrderStatus.Ready);
        var delivery = Delivery.CreateOrderSpecific(EntityId.New(), order.Id, "Alice", "0901234567", "123 Main Street");
        var actorId = EntityId.New();
        var repository = new DeliveryRepository(order, delivery);
        var handler = new DeliveryCommandHandler(repository, new FakeUnitOfWork(), new TestClock(Now.Value), new TestTransactionManager());

        Assert.True((await handler.AssignAsync(new(order.Id, actorId, actorId))).IsSuccess);
        Assert.True((await handler.StartAsync(new(order.Id, actorId))).IsSuccess);
        Assert.True((await handler.CompleteAsync(new(order.Id, actorId))).IsSuccess);
        Assert.Equal(DeliveryStatus.Delivered, delivery.Status);
        Assert.Equal(3, delivery.StatusHistory.Count);
        Assert.Equal(3, repository.OrderLockCount);
    }

    private sealed class StoredOrder : Order
    {
        public StoredOrder(string status)
        {
            Id = EntityId.New();
            Status = status;
            Channel = "delivery";
        }
    }

    private sealed class DeliveryRepository(Order order, Delivery delivery) : IDeliveryRepository
    {
        public int OrderLockCount { get; private set; }
        public int DeliveryReadCount { get; private set; }
        public Task<Order?> LoadOrderForUpdateAsync(EntityId orderId, CancellationToken cancellationToken = default)
        {
            OrderLockCount++;
            return Task.FromResult<Order?>(order.Id == orderId ? order : null);
        }

        public Task<Delivery?> FindDeliveryForOrderAsync(EntityId orderId, CancellationToken cancellationToken = default)
        {
            DeliveryReadCount++;
            return Task.FromResult<Delivery?>(delivery.OrderId == orderId ? delivery : null);
        }

        public Task<bool> IsEligibleDeliveryStaffAsync(EntityId staffId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public void AddStatusHistory(DeliveryStatusHistory history) { }
    }
}
