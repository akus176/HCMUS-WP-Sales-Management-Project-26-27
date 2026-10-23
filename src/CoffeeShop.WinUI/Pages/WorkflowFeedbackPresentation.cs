namespace CoffeeShop.WinUI.Pages;

using CoffeeShop.WinUI.ViewModels;
using Microsoft.UI.Xaml.Controls;

internal static class WorkflowFeedbackPresentation
{
    public static void Apply(this InfoBar infoBar, WorkflowFeedback feedback)
    {
        infoBar.Title = feedback.Title;
        infoBar.Message = feedback.DisplayMessage;
        infoBar.Severity = feedback.Kind switch
        {
            WorkflowFeedbackKind.Success => InfoBarSeverity.Success,
            WorkflowFeedbackKind.Validation or WorkflowFeedbackKind.Conflict => InfoBarSeverity.Warning,
            WorkflowFeedbackKind.Error or WorkflowFeedbackKind.DatabaseError => InfoBarSeverity.Error,
            _ => InfoBarSeverity.Informational,
        };
        infoBar.IsOpen = true;
    }
}
