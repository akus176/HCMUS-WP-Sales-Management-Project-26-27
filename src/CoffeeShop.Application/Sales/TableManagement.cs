namespace CoffeeShop.Application.Sales;

using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public sealed record TableManagementSummary(EntityId Id, string TableNumber, int? Seats, bool IsOccupied,
    string? ActiveOrderNumber);

public sealed record UpdateTableCommand(EntityId Id, string TableNumber, int? Seats);
public sealed record DeleteTableCommand(EntityId Id);

public interface ITableManagementWorkspaceRepository
{
    Task<IReadOnlyList<TableManagementSummary>> ListTablesAsync(CancellationToken cancellationToken = default);
    Task<DiningTable?> FindTableAsync(EntityId id, CancellationToken cancellationToken = default);
    Task<bool> TableNumberExistsAsync(string tableNumber, EntityId? exceptId,
        CancellationToken cancellationToken = default);
    void RemoveTable(DiningTable table);
}

public sealed class TableManagementQueryHandler(ITableManagementWorkspaceRepository repository)
{
    public Task<IReadOnlyList<TableManagementSummary>> ListAsync(CancellationToken cancellationToken = default) =>
        repository.ListTablesAsync(cancellationToken);
}

public sealed class TableCommandHandler(
    ITableManagementWorkspaceRepository repository,
    IUnitOfWork unitOfWork)
{
    public async Task<Result> UpdateAsync(UpdateTableCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var table = await repository.FindTableAsync(command.Id, cancellationToken);
        if (table is null) return Result.Failure(NotFound());
        var number = NormalizeNumber(command.TableNumber);
        if (number.IsFailure) return Result.Failure(number.Error!);
        if (!ValidSeats(command.Seats)) return Result.Failure(SeatsError());
        if (await repository.TableNumberExistsAsync(number.Value, command.Id, cancellationToken))
            return Result.Failure(NumberExists(number.Value));

        table.UpdateDetails(number.Value, command.Seats);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(DeleteTableCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var table = await repository.FindTableAsync(command.Id, cancellationToken);
        if (table is null) return Result.Failure(NotFound());
        if (table.IsOccupied)
            return Result.Failure(Error.Conflict("tables.occupied",
                $"Table '{table.TableNumber}' is occupied. Release its order before deleting it."));

        repository.RemoveTable(table);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static Result<string> NormalizeNumber(string tableNumber)
    {
        if (string.IsNullOrWhiteSpace(tableNumber))
            return Result<string>.Failure(Error.Validation("tables.number_required", "Enter a table number."));
        var trimmed = tableNumber.Trim();
        if (trimmed.Length > 20)
            return Result<string>.Failure(Error.Validation("tables.number_too_long",
                "Table numbers must be 20 characters or fewer."));
        return Result<string>.Success(trimmed);
    }

    private static bool ValidSeats(int? seats) => seats is null or > 0;

    private static Error SeatsError() => Error.Validation("tables.seats_invalid", "Seats must be a positive number.");

    private static Error NumberExists(string number) => Error.Conflict("tables.number_exists",
        $"Table '{number}' already exists.");

    private static Error NotFound() => Error.NotFound("tables.not_found", "Dining table not found.");
}
