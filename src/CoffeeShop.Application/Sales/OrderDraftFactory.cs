namespace CoffeeShop.Application.Sales;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public sealed record OrderLineSelection(MenuItem MenuItem, SizeVariant SizeVariant, int Quantity, IReadOnlyCollection<Addon> Addons, IReadOnlyCollection<MenuItemAddon>? Compatibility = null);

public sealed record CreateOrderDraftRequest(
    EntityId Id,
    string OrderNumber,
    string Channel,
    EntityId CreatedBy,
    UtcTimestamp CreatedAt,
    IReadOnlyCollection<OrderLineSelection> Lines,
    EntityId? CustomerId = null,
    EntityId? TableId = null,
    Delivery? Delivery = null,
    DeliverySelection? DeliverySelection = null);

public sealed class OrderDraftFactory
{
    private readonly ICustomerDeliveryAddressReader? _addressReader;

    public OrderDraftFactory(ICustomerDeliveryAddressReader? addressReader = null)
    {
        _addressReader = addressReader;
    }

    public Result<Order> Create(CreateOrderDraftRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            Delivery? delivery = null;

            if (request.Channel == "delivery")
            {
                if (request.DeliverySelection is not null)
                {
                    if (request.DeliverySelection.IsReusable)
                    {
                        if (request.CustomerId is null)
                        {
                            return Result<Order>.Failure(Error.Validation("orders.invalid", "A delivery order referencing a customer address must have an attached customer."));
                        }

                        if (_addressReader is null)
                        {
                            return Result<Order>.Failure(Error.Validation("orders.invalid", "Address reader is required to resolve customer address."));
                        }

                        var address = _addressReader.GetAddressById(request.DeliverySelection.CustomerAddressId!);
                        if (address is null)
                        {
                            return Result<Order>.Failure(Error.Validation("orders.invalid", $"Delivery address '{request.DeliverySelection.CustomerAddressId}' was not found."));
                        }

                        if (address.CustomerId != request.CustomerId)
                        {
                            return Result<Order>.Failure(Error.Validation("orders.invalid", "Address ownership cannot cross customer boundaries."));
                        }

                        if (!address.IsActive)
                        {
                            return Result<Order>.Failure(Error.Validation("orders.invalid", "Cannot select a deactivated delivery address."));
                        }

                        delivery = Delivery.CreateFromCustomerAddress(
                            EntityId.New(),
                            request.Id,
                            request.DeliverySelection.RecipientName,
                            request.DeliverySelection.ContactNumber,
                            address.AddressId,
                            address.AddressLine);
                    }
                    else
                    {
                        delivery = Delivery.CreateOrderSpecific(
                            EntityId.New(),
                            request.Id,
                            request.DeliverySelection.RecipientName,
                            request.DeliverySelection.ContactNumber,
                            request.DeliverySelection.AddressLine ?? string.Empty);
                    }
                }
                else if (request.Delivery is not null)
                {
                    delivery = request.Delivery;

                    if (delivery.CustomerAddressId is not null)
                    {
                        if (request.CustomerId is null)
                        {
                            return Result<Order>.Failure(Error.Validation("orders.invalid", "Delivery orders referencing a customer address must have an attached customer."));
                        }

                        if (_addressReader is not null)
                        {
                            var address = _addressReader.GetAddressById(delivery.CustomerAddressId);
                            if (address is null)
                            {
                                return Result<Order>.Failure(Error.Validation("orders.invalid", $"Delivery address '{delivery.CustomerAddressId}' was not found."));
                            }

                            if (address.CustomerId != request.CustomerId)
                            {
                                return Result<Order>.Failure(Error.Validation("orders.invalid", "Address ownership cannot cross customer boundaries."));
                            }

                            if (!address.IsActive)
                            {
                                return Result<Order>.Failure(Error.Validation("orders.invalid", "Cannot select a deactivated delivery address."));
                            }
                        }
                    }
                }
            }
            else
            {
                if (request.Delivery is not null || request.DeliverySelection is not null)
                {
                    return Result<Order>.Failure(Error.Validation("orders.invalid", "Only delivery orders can contain delivery details."));
                }
            }

            var lines = request.Lines.Select(selection => new OrderLine(EntityId.New(), selection.MenuItem, selection.SizeVariant, selection.Quantity, selection.Addons, selection.Compatibility));
            return Result<Order>.Success(new Order(request.Id, request.OrderNumber, request.Channel, request.CreatedBy, request.CreatedAt, lines, request.CustomerId, request.TableId, delivery));
        }
        catch (ArgumentException exception)
        {
            return Result<Order>.Failure(Error.Validation("orders.invalid", exception.Message));
        }
    }
}

