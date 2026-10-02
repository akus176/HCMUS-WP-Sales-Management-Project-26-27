namespace CoffeeShop.Application.Sales;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public sealed record OrderLineSelection(MenuItem MenuItem, SizeVariant SizeVariant, int Quantity, IReadOnlyCollection<Addon> Addons);

public sealed record CreateOrderDraftRequest(
    EntityId Id,
    string OrderNumber,
    string Channel,
    EntityId CreatedBy,
    UtcTimestamp CreatedAt,
    IReadOnlyCollection<OrderLineSelection> Lines,
    EntityId? CustomerId = null,
    EntityId? TableId = null,
    Delivery? Delivery = null);

public sealed class OrderDraftFactory
{
    public Result<Order> Create(CreateOrderDraftRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var lines = request.Lines.Select(selection => new OrderLine(EntityId.New(), selection.MenuItem, selection.SizeVariant, selection.Quantity, selection.Addons));
            return Result<Order>.Success(new Order(request.Id, request.OrderNumber, request.Channel, request.CreatedBy, request.CreatedAt, lines, request.CustomerId, request.TableId, request.Delivery));
        }
        catch (ArgumentException exception)
        {
            return Result<Order>.Failure(Error.Validation("orders.invalid", exception.Message));
        }
    }
}
