namespace CoffeeShop.Application.Tests.Sales;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Sales;

public sealed class TableManagementCommandHandlerTests
{
    [Fact]
    public async Task Create_TrimsNumberAndReturnsAvailableTable()
    {
        var repository = new Tables();
        var result = await new TableManagementCommandHandler(repository).CreateAsync(new(" T12 ", 4));

        Assert.True(result.IsSuccess);
        Assert.Equal("T12", result.Value.TableNumber);
        Assert.Equal(4, result.Value.Seats);
        Assert.False(result.Value.IsOccupied);
        Assert.Null(result.Value.ActiveOrderId);
        Assert.Single(repository.Saved);
    }

    [Theory]
    [InlineData(" ", 2)]
    [InlineData("123456789012345678901", 2)]
    [InlineData("T1", 0)]
    [InlineData("T1", -1)]
    [InlineData("T1", null)]
    public async Task Create_RejectsInvalidInputWithoutWriting(string number, int? seats)
    {
        var repository = new Tables();
        var result = await new TableManagementCommandHandler(repository).CreateAsync(new(number, seats));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Empty(repository.Saved);
    }

    [Fact]
    public async Task Create_MapsDuplicateToConflict()
    {
        var repository = new Tables();
        var handler = new TableManagementCommandHandler(repository);
        Assert.True((await handler.CreateAsync(new("T1", 2))).IsSuccess);

        var duplicate = await handler.CreateAsync(new(" T1 ", 6));

        Assert.Equal(ErrorType.Conflict, duplicate.Error!.Type);
        Assert.Equal("tables.duplicate_number", duplicate.Error.Code);
        Assert.Single(repository.Saved);
    }

    private sealed class Tables : ITableManagementRepository
    {
        public List<DiningTable> Saved { get; } = [];

        public Task<bool> CreateAsync(DiningTable table, CancellationToken cancellationToken = default)
        {
            if (Saved.Any(existing => existing.TableNumber == table.TableNumber)) return Task.FromResult(false);
            Saved.Add(table);
            return Task.FromResult(true);
        }
    }
}
