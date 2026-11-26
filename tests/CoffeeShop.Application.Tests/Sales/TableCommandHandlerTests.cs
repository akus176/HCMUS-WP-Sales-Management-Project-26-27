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
    public async Task CreateAsync_WithValidData_AddsTableAndSaves()
    {
        var result = await handler.CreateAsync(new CreateTableCommand("  T09  ", 6));

        Assert.True(result.IsSuccess);
        var table = Assert.Single(repository.Tables);
        Assert.Equal("T09", table.TableNumber);
        Assert.Equal(6, table.Seats);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithBlankNumber_ReturnsValidationError()
    {
        var result = await handler.CreateAsync(new CreateTableCommand("  ", 4));

        Assert.True(result.IsFailure);
        Assert.Equal("tables.number_required", result.Error!.Code);
        Assert.Empty(repository.Tables);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithNonPositiveSeats_ReturnsValidationError()
    {
        var result = await handler.CreateAsync(new CreateTableCommand("T02", 0));

        Assert.True(result.IsFailure);
        Assert.Equal("tables.seats_invalid", result.Error!.Code);
        Assert.Empty(repository.Tables);
    }

    [Fact]
    public async Task CreateAsync_WithExistingNumber_ReturnsConflict()
    {
        AddTable("T03");

        var result = await handler.CreateAsync(new CreateTableCommand("T03", 2));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("tables.number_exists", result.Error.Code);
        Assert.Single(repository.Tables);
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

    [Fact]
    public async Task DeleteAsync_AllowsTheNumberToBeCreatedAgain()
    {
        var table = AddTable("T08");
        Assert.True((await handler.DeleteAsync(new DeleteTableCommand(table.Id))).IsSuccess);

        var result = await handler.CreateAsync(new CreateTableCommand("T08", 4));

        Assert.True(result.IsSuccess);
        Assert.Equal("T08", Assert.Single(repository.Tables).TableNumber);
    }
}
