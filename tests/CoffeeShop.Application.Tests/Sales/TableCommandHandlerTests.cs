using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Sales;
using CoffeeShop.Application.Tests.Testing;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

namespace CoffeeShop.Application.Tests.Sales;

public class TableCommandHandlerTests
{
    private readonly FakeTableManagementRepository repository = new();
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly TableCommandHandler handler;

    public TableCommandHandlerTests() => handler = new TableCommandHandler(repository, unitOfWork);

    private DiningTable AddTable(string number = "T01", int? seats = 4, bool occupied = false)
    {
        var table = new DiningTable(EntityId.New(), number, seats, occupied);
        repository.Tables.Add(table);
        return table;
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_ChangesNumberAndSeats()
    {
        var table = AddTable("T04", 2);

        var result = await handler.UpdateAsync(new UpdateTableCommand(table.Id, "T04A", 5));

        Assert.True(result.IsSuccess);
        Assert.Equal("T04A", table.TableNumber);
        Assert.Equal(5, table.Seats);
    }

    [Fact]
    public async Task UpdateAsync_WithUnknownTable_ReturnsNotFound()
    {
        var result = await handler.UpdateAsync(new UpdateTableCommand(EntityId.New(), "T05", 4));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("tables.not_found", result.Error.Code);
    }

    [Fact]
    public async Task DeleteAsync_WithOccupiedTable_ReturnsConflictAndKeepsIt()
    {
        var table = AddTable("T06", occupied: true);

        var result = await handler.DeleteAsync(new DeleteTableCommand(table.Id));

        Assert.True(result.IsFailure);
        Assert.Equal("tables.occupied", result.Error!.Code);
        Assert.Single(repository.Tables);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task DeleteAsync_WithAvailableTable_RemovesIt()
    {
        var table = AddTable("T07");

        var result = await handler.DeleteAsync(new DeleteTableCommand(table.Id));

        Assert.True(result.IsSuccess);
        Assert.Empty(repository.Tables);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

}
