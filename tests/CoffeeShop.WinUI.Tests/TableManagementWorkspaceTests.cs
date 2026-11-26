using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.WinUI.Services;
using CoffeeShop.WinUI.ViewModels;

namespace CoffeeShop.WinUI.Tests;

public sealed class TableManagementWorkspaceTests
{
    private sealed class FakeSource : ITableManagementSource
    {
        public List<TableManagementSummary> Tables { get; } = [];
        public Error? NextError { get; set; }
        public int ListCalls { get; private set; }

        public Task<Result<IReadOnlyList<TableManagementSummary>>> ListAsync()
        {
            ListCalls++;
            return Task.FromResult(Result<IReadOnlyList<TableManagementSummary>>.Success(Tables.ToArray()));
        }

        public Task<Result<EntityId>> CreateAsync(CreateTableCommand command)
        {
            if (NextError is { } error) return Task.FromResult(Result<EntityId>.Failure(error));
            var id = EntityId.New();
            Tables.Add(new TableManagementSummary(id, command.TableNumber.Trim(), command.Seats, false, null));
            return Task.FromResult(Result<EntityId>.Success(id));
        }

        public Task<Result> UpdateAsync(UpdateTableCommand command)
        {
            if (NextError is { } error) return Task.FromResult(Result.Failure(error));
            var index = Tables.FindIndex(table => table.Id == command.Id);
            Tables[index] = Tables[index] with { TableNumber = command.TableNumber.Trim(), Seats = command.Seats };
            return Task.FromResult(Result.Success());
        }

        public Task<Result> DeleteAsync(DeleteTableCommand command)
        {
            if (NextError is { } error) return Task.FromResult(Result.Failure(error));
            Tables.RemoveAll(table => table.Id == command.Id);
            return Task.FromResult(Result.Success());
        }
    }

    private readonly FakeSource source = new();
    private readonly TableManagementWorkspace workspace;

    public TableManagementWorkspaceTests() => workspace = new TableManagementWorkspace(source);

    [Fact]
    public async Task LoadAsync_ShowsTablesAndSummarizesOccupancy()
    {
        source.Tables.Add(new(EntityId.New(), "T1", 2, false, null));
        source.Tables.Add(new(EntityId.New(), "T2", 4, true, "ORD-42"));

        await workspace.LoadAsync();

        Assert.Equal(2, workspace.Tables.Count);
        Assert.Contains("1 occupied", workspace.Message);
        Assert.Equal(WorkflowFeedbackKind.Ready, workspace.Feedback.Kind);
    }

    [Fact]
    public async Task CreateAsync_ReloadsTheListAndReportsSuccess()
    {
        await workspace.LoadAsync();

        var saved = await workspace.SaveAsync(null, "T09", 4);

        Assert.True(saved);
        Assert.Single(workspace.Tables);
        Assert.Equal("T09", workspace.Tables[0].TableNumber);
        Assert.Equal("Table created.", workspace.Message);
        Assert.Null(workspace.Error);
        Assert.Equal(WorkflowFeedbackKind.Success, workspace.Feedback.Kind);
    }

    [Fact]
    public async Task SaveAsync_WithApplicationError_ShowsTheErrorAndDoesNotReload()
    {
        await workspace.LoadAsync();
        var callsBefore = source.ListCalls;
        source.NextError = Error.Conflict("tables.number_exists", "Table 'T01' already exists.");

        var saved = await workspace.SaveAsync(null, "T01", 4);

        Assert.False(saved);
        Assert.Equal("tables.number_exists", workspace.Error!.Code);
        Assert.Equal(callsBefore, source.ListCalls);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheTableAndReloads()
    {
        var id = EntityId.New();
        source.Tables.Add(new(id, "T4", 2, false, null));
        await workspace.LoadAsync();

        Assert.True(await workspace.DeleteAsync(id));

        Assert.Empty(workspace.Tables);
        Assert.True(workspace.IsEmpty);
        Assert.Equal("Table deleted.", workspace.Message);
    }

    [Fact]
    public async Task DeleteAsync_WithOccupiedTable_ShowsConflict()
    {
        var id = EntityId.New();
        source.Tables.Add(new(id, "T5", 2, true, "ORD-7"));
        await workspace.LoadAsync();
        source.NextError = Error.Conflict("tables.occupied", "Table 'T5' is occupied.");

        Assert.False(await workspace.DeleteAsync(id));

        Assert.Equal("tables.occupied", workspace.Error!.Code);
        Assert.Single(workspace.Tables);
        Assert.Equal(WorkflowFeedbackKind.Conflict, workspace.Feedback.Kind);
    }
}
