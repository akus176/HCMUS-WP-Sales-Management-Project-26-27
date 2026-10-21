namespace CoffeeShop.WinUI.ViewModels;

using CoffeeShop.Application.Common.Results;

public enum WorkflowFeedbackKind
{
    Ready,
    Loading,
    Empty,
    Validation,
    Conflict,
    Error,
    DatabaseError,
    Success,
}

/// <summary>
/// Headless presentation state shared by the direct-database WinUI workflows.
/// It deliberately accepts application errors, but never database exceptions,
/// so provider messages and connection details cannot reach operator-facing UI.
/// </summary>
public sealed record WorkflowFeedback(
    WorkflowFeedbackKind Kind,
    string Title,
    string Message,
    IReadOnlyList<ErrorDetail> Details,
    string? NextAction = null)
{
    public bool IsLoading => Kind == WorkflowFeedbackKind.Loading;
    public bool IsError => Kind is WorkflowFeedbackKind.Validation
        or WorkflowFeedbackKind.Conflict
        or WorkflowFeedbackKind.Error
        or WorkflowFeedbackKind.DatabaseError;

    public string DisplayMessage => string.Join(
        Environment.NewLine,
        new[] { Message }
            .Concat(Details.Select(detail => $"{detail.Target}: {detail.Message}"))
            .Concat(string.IsNullOrWhiteSpace(NextAction) ? [] : [NextAction]));

    public static WorkflowFeedback Resolve(
        bool isLoading,
        bool isEmpty,
        Error? error,
        string message)
    {
        if (isLoading)
        {
            return Loading(message);
        }

        if (error is not null)
        {
            return FromError(error);
        }

        return isEmpty ? Empty(message) : Ready(message);
    }

    public static WorkflowFeedback Ready(string message) =>
        new(WorkflowFeedbackKind.Ready, "Ready", message, []);

    public static WorkflowFeedback Loading(string message) =>
        new(WorkflowFeedbackKind.Loading, "Working", message, []);

    public static WorkflowFeedback Empty(string message, string? nextAction = null) =>
        new(WorkflowFeedbackKind.Empty, "Nothing here yet", message, [], nextAction);

    public static WorkflowFeedback Success(string message) =>
        new(WorkflowFeedbackKind.Success, "Done", message, []);

    public static WorkflowFeedback Validation(string target, string message) =>
        new(WorkflowFeedbackKind.Validation, "Check the input", message, [new(target, message)]);

    public static WorkflowFeedback DatabaseFailure(string operation, string nextAction) =>
        new(
            WorkflowFeedbackKind.DatabaseError,
            "Database unavailable",
            $"Could not {operation}. No connection details were displayed.",
            [],
            nextAction);

    public static WorkflowFeedback FromError(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Type switch
        {
            ErrorType.Validation => new(
                WorkflowFeedbackKind.Validation,
                "Check the input",
                error.Description,
                error.Details),
            ErrorType.Conflict => new(
                WorkflowFeedbackKind.Conflict,
                "Reload required",
                error.Description,
                error.Details,
                "The saved record changed or is no longer in the expected state. Reload it before retrying."),
            ErrorType.Unexpected => DatabaseFailure(
                "finish the operation",
                "Check the local database, then reload and verify the saved state before retrying."),
            _ => new(WorkflowFeedbackKind.Error, error.Type.ToString(), error.Description, error.Details),
        };
    }
}
