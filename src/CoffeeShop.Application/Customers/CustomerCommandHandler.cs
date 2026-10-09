namespace CoffeeShop.Application.Customers;

using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Domain.Common;

public sealed class CustomerCommandHandler
{
    private readonly ICustomerRepository repository;
    private readonly IUnitOfWork unitOfWork;
    private readonly IClock clock;

    public CustomerCommandHandler(ICustomerRepository repository, IUnitOfWork unitOfWork, IClock clock)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<EntityId>> CreateCustomerAsync(
        CreateCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!string.IsNullOrWhiteSpace(command.Phone)
            && await repository.PhoneExistsAsync(command.Phone.Trim(), cancellationToken: cancellationToken))
        {
            return Result<EntityId>.Failure(PhoneConflict(command.Phone));
        }

        var created = CustomerLifecycle.CreateCustomer(
            EntityId.New(), command.FullName, command.Phone, clock.UtcNow, command.Email);
        if (created.IsFailure)
        {
            return Result<EntityId>.Failure(created.Error!);
        }

        repository.AddCustomer(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<EntityId>.Success(created.Value.Id);
    }

    public async Task<Result> UpdateCustomerAsync(
        UpdateCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var customer = await repository.FindCustomerAsync(command.CustomerId, cancellationToken);
        if (customer is null)
        {
            return Result.Failure(CustomerNotFound(command.CustomerId));
        }

        if (!string.IsNullOrWhiteSpace(command.Phone)
            && await repository.PhoneExistsAsync(command.Phone.Trim(), customer.Id, cancellationToken))
        {
            return Result.Failure(PhoneConflict(command.Phone));
        }

        var updated = CustomerLifecycle.UpdateCustomer(
            customer, command.FullName, command.Phone, command.Email, clock.UtcNow);
        if (updated.IsFailure)
        {
            return updated;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeactivateCustomerAsync(
        DeactivateCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var customer = await repository.FindCustomerAsync(command.CustomerId, cancellationToken);
        if (customer is null)
        {
            return Result.Failure(CustomerNotFound(command.CustomerId));
        }

        var deactivated = CustomerLifecycle.DeactivateCustomer(customer, clock.UtcNow);
        if (deactivated.IsFailure)
        {
            return deactivated;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<EntityId>> AddAddressAsync(
        AddCustomerAddressCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var customer = await repository.FindCustomerAsync(command.CustomerId, cancellationToken);
        if (customer is null)
        {
            return Result<EntityId>.Failure(CustomerNotFound(command.CustomerId));
        }

        var added = CustomerLifecycle.AddAddress(
            customer, EntityId.New(), command.AddressLine, command.Label, command.IsDefault, clock.UtcNow);
        if (added.IsFailure)
        {
            return Result<EntityId>.Failure(added.Error!);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<EntityId>.Success(added.Value.Id);
    }

    public async Task<Result> UpdateAddressAsync(
        UpdateCustomerAddressCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var customer = await repository.FindCustomerAsync(command.CustomerId, cancellationToken);
        if (customer is null)
        {
            return Result.Failure(CustomerNotFound(command.CustomerId));
        }

        var updated = CustomerLifecycle.UpdateAddress(
            customer, command.AddressId, command.AddressLine, command.Label, command.IsDefault, clock.UtcNow);
        if (updated.IsFailure)
        {
            return updated;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> SetDefaultAddressAsync(
        SetDefaultCustomerAddressCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var customer = await repository.FindCustomerAsync(command.CustomerId, cancellationToken);
        if (customer is null)
        {
            return Result.Failure(CustomerNotFound(command.CustomerId));
        }

        var selected = CustomerLifecycle.SelectDefaultAddress(customer, command.AddressId, clock.UtcNow);
        if (selected.IsFailure)
        {
            return selected;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeactivateAddressAsync(
        DeactivateCustomerAddressCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var customer = await repository.FindCustomerAsync(command.CustomerId, cancellationToken);
        if (customer is null)
        {
            return Result.Failure(CustomerNotFound(command.CustomerId));
        }

        var deactivated = CustomerLifecycle.DeactivateAddress(customer, command.AddressId, clock.UtcNow);
        if (deactivated.IsFailure)
        {
            return deactivated;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static Error CustomerNotFound(EntityId id) =>
        Error.NotFound("customers.customer_not_found", $"Customer '{id}' was not found.");

    private static Error PhoneConflict(string phone) => Error.Conflict(
        "customers.phone_conflict",
        $"A customer with phone number '{phone.Trim()}' already exists.",
        [new ErrorDetail("phone", phone.Trim())]);
}
