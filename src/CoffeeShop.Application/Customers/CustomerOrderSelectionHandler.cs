namespace CoffeeShop.Application.Customers;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public sealed record CustomerOrderSelectionRequest(
    EntityId? CustomerId = null,
    bool IsDelivery = false,
    EntityId? CustomerAddressId = null,
    string? RecipientName = null,
    string? ContactNumber = null,
    string? AddressLine = null);

public sealed record CustomerOrderSelection(
    EntityId? CustomerId,
    string? CustomerName,
    DeliverySelection? Delivery,
    string? AddressLine);

public sealed record CustomerOrderDraft(Order Order, Delivery? Delivery);

// Shared by the customer picker and POS. Revalidate persisted customer/address
// state when creating a draft, rather than trusting a previously displayed row.
public sealed class CustomerOrderSelectionHandler(ICustomerReadRepository repository)
{
    public async Task<Result<CustomerOrderSelection>> PrepareAsync(
        CustomerOrderSelectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CustomerDetail? customer = null;
        if (request.CustomerId is not null)
        {
            customer = await repository.GetCustomerDetailAsync(request.CustomerId, cancellationToken);
            if (customer is null)
            {
                return Result<CustomerOrderSelection>.Failure(Error.NotFound(
                    "customers.customer_not_found", "The selected customer no longer exists. Reload and choose again."));
            }

            if (!customer.IsActive)
            {
                return Result<CustomerOrderSelection>.Failure(Error.Conflict(
                    "customers.inactive_selection", "This customer is inactive. Choose an active customer or continue as a walk-in."));
            }
        }

        if (!request.IsDelivery)
        {
            if (request.CustomerAddressId is not null)
            {
                return Invalid("A saved delivery address can only be selected for a delivery order.");
            }

            return Result<CustomerOrderSelection>.Success(new(customer?.Id, customer?.FullName, null, null));
        }

        var addressLine = request.AddressLine;
        if (request.CustomerAddressId is not null)
        {
            var address = customer?.Addresses.SingleOrDefault(item => item.Id == request.CustomerAddressId);
            if (address is null || !address.IsActive)
            {
                return Result<CustomerOrderSelection>.Failure(Error.Conflict(
                    "customers.address_unavailable", "Choose an active address belonging to the attached customer, or enter an order-only address."));
            }

            addressLine = address.AddressLine;
        }

        try
        {
            // Reuse delivery validation and normalization, including walk-in deliveries.
            var delivery = new Delivery(EntityId.New(), EntityId.New(),
                request.RecipientName ?? "", request.ContactNumber ?? "", addressLine ?? "", request.CustomerAddressId);
            var selection = request.CustomerAddressId is null
                ? DeliverySelection.OrderSpecific(delivery.AddressLine, delivery.RecipientName, delivery.ContactNumber)
                : DeliverySelection.FromCustomerAddress(request.CustomerAddressId, delivery.RecipientName, delivery.ContactNumber);
            return Result<CustomerOrderSelection>.Success(new(customer?.Id, customer?.FullName, selection, delivery.AddressLine));
        }
        catch (ArgumentException exception)
        {
            return Result<CustomerOrderSelection>.Failure(Error.Validation(
                "customers.invalid_delivery", exception.Message,
                [new ErrorDetail(exception.ParamName ?? "delivery", exception.Message)]));
        }
    }

    public async Task<Result<CustomerOrderDraft>> CreateDraftAsync(
        CreateOrderDraftRequest draft,
        CustomerOrderSelectionRequest selection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(selection);
        if ((draft.Channel == "delivery") != selection.IsDelivery)
        {
            return Result<CustomerOrderDraft>.Failure(Error.Validation("orders.delivery_mismatch", "Delivery details must match the order channel."));
        }

        var prepared = await PrepareAsync(selection, cancellationToken);
        if (prepared.IsFailure)
        {
            return Result<CustomerOrderDraft>.Failure(prepared.Error!);
        }

        var details = prepared.Value;
        var delivery = details.Delivery is { } contact
            ? new Delivery(EntityId.New(), draft.Id, contact.RecipientName, contact.ContactNumber,
                details.AddressLine!, contact.CustomerAddressId)
            : null;
        var created = new OrderDraftFactory().Create(draft with
        {
            CustomerId = details.CustomerId,
            Delivery = delivery,
            DeliverySelection = null
        });
        return created.IsSuccess
            ? Result<CustomerOrderDraft>.Success(new(created.Value, delivery))
            : Result<CustomerOrderDraft>.Failure(created.Error!);
    }

    private static Result<CustomerOrderSelection> Invalid(string description) =>
        Result<CustomerOrderSelection>.Failure(Error.Validation("customers.invalid_selection", description));
}
