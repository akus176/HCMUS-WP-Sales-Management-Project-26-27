using System.Text.Json;
using CoffeeShop.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace CoffeeShop.WinUI.Pages;

/// <summary>Developer smoke surface, available only with the --components launch argument.</summary>
internal sealed class ComponentGalleryPage : Page
{
    private readonly List<Control> primaryControls = [];
    private readonly TextBlock feedback = new() { Text = "Ready for keyboard and visual inspection." };
    private static object Resource(string key) => Microsoft.UI.Xaml.Application.Current.Resources[key];
    public ComponentGalleryPage()
    {
        RequestedTheme = ElementTheme.Light;
        Background = (Microsoft.UI.Xaml.Media.Brush)Resource("ConsoleGroundBrush");
        var grid = new Grid { Padding = new Thickness(32), ColumnSpacing = 24, RowSpacing = 20, Background = Background };
        grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition());
        var title = new TextBlock { Text = "Concept foundation", Style = (Style)Resource("ConsolePageTitleStyle") };
        Grid.SetColumnSpan(title, 3); grid.Children.Add(title);
        var controls = new StackPanel { Spacing = 14 };
        var primary = Button("PRIMARY ACTION", "ConsolePrimaryButtonStyle");
        primary.Click += (_, _) => feedback.Text = "Primary action completed.";
        controls.Children.Add(primary);
        var secondary = Button("SECONDARY ACTION", "ConsoleButtonStyle");
        secondary.Click += (_, _) => feedback.Text = "Secondary action completed.";
        controls.Children.Add(secondary);
        var icon = Button("+", "ConsoleIconButtonStyle");
        AutomationProperties.SetName(icon, "Add item");
        icon.Click += (_, _) => feedback.Text = "Add item completed.";
        controls.Children.Add(icon);
        var field = new TextBox { PlaceholderText = "Find a drink", Style = (Style)Resource("ConsoleFieldStyle") };
        AutomationProperties.SetName(field, "Find a drink");
        controls.Children.Add(field); primaryControls.Add(field);
        var tab = new ToggleButton { Content = "DRINKS & RECIPES", Style = (Style)Resource("ConsoleTabStyle") };
        AutomationProperties.SetName(tab, "Drinks and recipes tab");
        controls.Children.Add(tab); primaryControls.Add(tab);
        controls.Children.Add(new Border { Style = (Style)Resource("ConsoleStampStyle"), Child = new TextBlock { Text = "ACTIVE", Style = (Style)Resource("ConsoleLabelStyle") } });
        controls.Children.Add(feedback);
        Grid.SetRow(controls, 1); grid.Children.Add(controls);
        var surfaces = new StackPanel { Spacing = 14 };
        foreach (var (key, text) in new[] { ("ConsolePaperPanelStyle", "Paper panel"), ("ConsoleTicketStyle", "Ticket surface"), ("ConsoleListRowStyle", "List row"), ("ConsoleSelectedRowStyle", "Selected row"), ("ConsoleWarningStyle", "Warning state") })
        {
            var label = new TextBlock { Text = text, Style = (Style)Resource("ConsoleSectionTitleStyle") };
            if (key == "ConsoleTicketStyle") label.Foreground = (Microsoft.UI.Xaml.Media.Brush)Resource("ConsoleOnFieldBrush");
            surfaces.Children.Add(new Border { Style = (Style)Resource(key), Child = label });
        }
        surfaces.Children.Add(new ConceptPattern { Height = 26 });
        Grid.SetRow(surfaces, 1); Grid.SetColumn(surfaces, 1); grid.Children.Add(surfaces);
        var notes = new StackPanel { Spacing = 12 };
        foreach (var status in new[] { "Confirmed", "Preparing", "Ready" })
        {
            var note = new ConceptNote { Status = status, IsSelected = status == "Preparing" };
            var copy = new StackPanel { Spacing = 8 };
            copy.Children.Add(new TextBlock { Text = status, Style = (Style)Resource("ConsoleSectionTitleStyle") });
            copy.Children.Add(new TextBlock { Text = "Cappuccino · Cold Brew · Butter Croissant", Style = (Style)Resource("ConsoleBodyStyle") });
            var action = Button(status == "Confirmed" ? "START" : status == "Preparing" ? "FINISH" : "SERVE", "ConsoleButtonStyle");
            action.Click += (_, _) => feedback.Text = status + " note action completed.";
            copy.Children.Add(action); note.NoteContent = copy; notes.Children.Add(note);
        }
        Grid.SetRow(notes, 1); Grid.SetColumn(notes, 2); grid.Children.Add(notes);
        Content = grid;
        Loaded += async (_, _) =>
        {
            var capture = Environment.GetCommandLineArgs().Contains("--capture-components");
            if (capture) { grid.Width = 1440; grid.Height = 900; grid.UpdateLayout(); }
            await Task.Delay(200);
            primary.Focus(FocusState.Keyboard);
            var resources = new List<string>();
            foreach (var dictionary in Microsoft.UI.Xaml.Application.Current.Resources.MergedDictionaries)
                foreach (var key in dictionary.Keys)
                {
                    _ = dictionary[key];
                    resources.Add(key.ToString() ?? "");
                }
            var result = new
            {
                surface = "Component gallery",
                resourcesResolved = resources.Count,
                primaryTargets = primaryControls.Select(control => new { name = AutomationProperties.GetName(control), width = control.ActualWidth, height = control.ActualHeight }),
                allTargetsAtLeast48 = primaryControls.All(control => control.ActualWidth >= 48 && control.ActualHeight >= 48),
                keyboardFocus = primary.FocusState.ToString(),
                width = ActualWidth,
                height = ActualHeight,
            };
            var directory = PresentationVerification.DirectoryPath;
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "component-verification.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            if (capture)
            {
                await PresentationVerification.CaptureAsync(grid, "native-components.png");
                grid.Width = grid.Height = double.NaN;
            }
        };
    }
    private Button Button(string text, string style)
    {
        var button = new Button { Content = text, Style = (Style)Resource(style) };
        AutomationProperties.SetName(button, text);
        primaryControls.Add(button);
        return button;
    }
}
