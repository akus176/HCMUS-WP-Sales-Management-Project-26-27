namespace CoffeeShop.Application.Customers;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Customers;

// Operates on resolved domain objects; loading and saving belong to customer use cases.
public static class CustomerLifecycle
{
    public static Result<Customer> CreateCustomer(
        EntityId id,
        string fullName,
        string phone,
        UtcTimestamp createdAt,
        string? email = null,
        bool isActive = true,
        Func<string, bool>? isPhoneTaken = null)
    {
        if (isPhoneTaken is not null && !string.IsNullOrWhiteSpace(phone) && isPhoneTaken(phone.Trim()))
        {
            return Result<Customer>.Failure(Error.Conflict(
                "customers.phone_conflict",
                $"A customer with phone number '{phone.Trim()}' already exists.",
                [new ErrorDetail("phone", phone.Trim())]));
        }

        return Execute(() => new Customer(id, fullName, phone, createdAt, createdAt, email, isActive));
    }

    public static Result UpdateCustomer(
        Customer customer,
        string fullName,
        string phone,
        string? email,
        UtcTimestamp changedAt,
        Func<string, EntityId, bool>? isPhoneTakenByAnother = null)
    {
        ArgumentNullException.ThrowIfNull(customer);

        if (isPhoneTakenByAnother is not null && !string.IsNullOrWhiteSpace(phone) && isPhoneTakenByAnother(phone.Trim(), customer.Id))
        {
            return Result.Failure(Error.Conflict(
                "customers.phone_conflict",
                $"A customer with phone number '{phone.Trim()}' already exists.",
                [new ErrorDetail("phone", phone.Trim())]));
        }

        return Execute(() => customer.Update(fullName, phone, email, changedAt));
    }

    public static Result DeactivateCustomer(Customer customer, UtcTimestamp changedAt)
    {
        ArgumentNullException.ThrowIfNull(customer);
        return Execute(() => customer.Deactivate(changedAt));
    }

    public static Result ActivateCustomer(Customer customer, UtcTimestamp changedAt)
    {
        ArgumentNullException.ThrowIfNull(customer);
        return Execute(() => customer.Activate(changedAt));
    }

    public static Result<CustomerAddress> AddAddress(
        Customer customer,
        EntityId addressId,
        string addressLine,
        string? label = null,
        bool isDefault = false,
        UtcTimestamp? changedAt = null)
    {
        ArgumentNullException.ThrowIfNull(customer);
        return Execute(() => customer.AddAddress(addressId, addressLine, label, isDefault, changedAt));
    }

    public static Result<CustomerAddress> UpdateAddress(
        Customer customer,
        EntityId addressId,
        string addressLine,
        string? label,
        bool isDefault,
        UtcTimestamp? changedAt = null)
    {
        ArgumentNullException.ThrowIfNull(customer);
        return Execute(() => customer.UpdateAddress(addressId, addressLine, label, isDefault, changedAt));
    }

    public static Result<CustomerAddress> SelectDefaultAddress(
        Customer customer,
        EntityId addressId,
        UtcTimestamp? changedAt = null)
    {
        ArgumentNullException.ThrowIfNull(customer);
        return Execute(() => customer.SetDefaultAddress(addressId, changedAt));
    }

    public static Result<CustomerAddress> DeactivateAddress(
        Customer customer,
        EntityId addressId,
        UtcTimestamp? changedAt = null)
    {
        ArgumentNullException.ThrowIfNull(customer);
        return Execute(() => customer.DeactivateAddress(addressId, changedAt));
    }

    public static Result<CustomerAddress> ActivateAddress(
        Customer customer,
        EntityId addressId,
        UtcTimestamp? changedAt = null)
    {
        ArgumentNullException.ThrowIfNull(customer);
        return Execute(() => customer.ActivateAddress(addressId, changedAt));
    }

    private static Result Execute(Action operation) => Execute(() =>
    {
        operation();
        return true;
    });

    private static Result<T> Execute<T>(Func<T> operation)
        where T : notnull
    {
        try
        {
            return Result<T>.Success(operation());
        }
        catch (ArgumentException exception)
        {
            return Result<T>.Failure(Error.Validation(
                "customers.invalid_input",
                exception.Message,
                [new ErrorDetail(exception.ParamName ?? "customer", exception.Message)]));
        }
        catch (InvalidOperationException exception)
        {
            return Result<T>.Failure(Error.Conflict("customers.invalid_state", exception.Message));
        }
    }
}
