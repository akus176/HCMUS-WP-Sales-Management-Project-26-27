namespace CoffeeShop.WinUI.Tests;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.WinUI.ViewModels;

public sealed class WorkflowFeedbackTests
{
    [Theory]
    [InlineData("menu")]
    [InlineData("customer")]
    [InlineData("inventory")]
    [InlineData("order")]
    [InlineData("fulfillment")]
    [InlineData("report")]
    public void RequiredWorkflowAreas_ShareVisibleLoadingAndActionableEmptyStates(string workflow)
    {
        var loading = WorkflowFeedback.Loading($"Loading {workflow} workflow...");
        var empty = WorkflowFeedback.Empty(
            $"No {workflow} records are available.",
            $"Create or reload {workflow} data to continue.");

        Assert.True(loading.IsLoading);
        Assert.Contains(workflow, loading.DisplayMessage);
        Assert.Equal(WorkflowFeedbackKind.Empty, empty.Kind);
        Assert.Contains("continue", empty.DisplayMessage);
    }

    [Fact]
    public void Validation_IdentifiesTheInputThatNeedsCorrection()
    {
        var feedback = WorkflowFeedback.FromError(Error.Validation(
            "orders.invalid_input",
            "Correct the highlighted values.",
            [new("paymentAmount", "Enter a positive amount.")]));

        Assert.Equal(WorkflowFeedbackKind.Validation, feedback.Kind);
        Assert.Contains("paymentAmount: Enter a positive amount.", feedback.DisplayMessage);
    }

    [Fact]
    public void Conflict_ExplainsStaleStateAndRequiresReload()
    {
        var feedback = WorkflowFeedback.FromError(Error.Conflict(
            "receipts.changed",
            "The receipt was already posted."));

        Assert.Equal(WorkflowFeedbackKind.Conflict, feedback.Kind);
        Assert.Contains("changed", feedback.DisplayMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reload", feedback.DisplayMessage);
    }

    [Fact]
    public void UnexpectedFailure_NeverDisplaysProviderOrConnectionDetails()
    {
        var feedback = WorkflowFeedback.FromError(Error.Unexpected(
            "database.failed",
            "Host=private;Password=secret;Npgsql timeout"));

        Assert.Equal(WorkflowFeedbackKind.DatabaseError, feedback.Kind);
        Assert.DoesNotContain("private", feedback.DisplayMessage);
        Assert.DoesNotContain("secret", feedback.DisplayMessage);
        Assert.DoesNotContain("Npgsql", feedback.DisplayMessage);
        Assert.Contains("database", feedback.DisplayMessage, StringComparison.OrdinalIgnoreCase);
    }
}
