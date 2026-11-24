namespace CoffeeShop.Application.Sales;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public sealed record CreateDiningTableCommand(string? TableNumber, int? Seats);

public interface ITableManagementRepository
{
    /// <summary>Returns false only when the unique table-number constraint rejects the insert.</summary>
    Task<bool> CreateAsync(DiningTable table, CancellationToken cancellationToken = default);
}

public sealed class TableManagementCommandHandler(ITableManagementRepository repository)
{
    public async Task<Result<DiningTable>> CreateAsync(CreateDiningTableCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var number = command.TableNumber?.Trim();
        if (string.IsNullOrWhiteSpace(number) || number.Length > 20)
            return Result<DiningTable>.Failure(Error.Validation("tables.invalid_number",
                "Enter a table number of 1 to 20 characters.", [new ErrorDetail("tableNumber", "A table number is required and must be at most 20 characters.")]));
        if (command.Seats is not > 0)
            return Result<DiningTable>.Failure(Error.Validation("tables.invalid_seats",
                "Enter a positive seat count.", [new ErrorDetail("seats", "Seats must be a positive whole number.")]));

        var table = new DiningTable(EntityId.New(), number, command.Seats);
        if (!await repository.CreateAsync(table, cancellationToken))
            return Result<DiningTable>.Failure(Error.Conflict("tables.duplicate_number",
                "A table with this number already exists."));
        return Result<DiningTable>.Success(table);
    }
}
