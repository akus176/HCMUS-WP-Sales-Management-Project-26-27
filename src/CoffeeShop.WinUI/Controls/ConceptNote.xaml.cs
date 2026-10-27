using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace CoffeeShop.WinUI.Controls;

[ContentProperty(Name = nameof(NoteContent))]
public sealed partial class ConceptNote : UserControl
{
    public ConceptNote()
    {
        InitializeComponent();
        ShadowHost.SizeChanged += (_, _) => UpdateShadow();
    }
    private void UpdateShadow()
    {
        var compositor = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(this).Compositor;
        var visual = compositor.CreateSpriteVisual();
        visual.Size = new System.Numerics.Vector2((float)ShadowHost.ActualWidth, (float)ShadowHost.ActualHeight);
        var shadow = compositor.CreateDropShadow();
        shadow.Color = Windows.UI.Color.FromArgb(255, 38, 35, 33);
        shadow.BlurRadius = 8;
        shadow.Opacity = .16f;
        shadow.Offset = new System.Numerics.Vector3(1, 4, 0);
        visual.Shadow = shadow;
        Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.SetElementChildVisual(ShadowHost, visual);
    }
    public object? NoteContent { get => GetValue(NoteContentProperty); set => SetValue(NoteContentProperty, value); }
    public static readonly DependencyProperty NoteContentProperty = DependencyProperty.Register(nameof(NoteContent), typeof(object), typeof(ConceptNote), new PropertyMetadata(null, (sender, args) => ((ConceptNote)sender).NotePresenter.Content = args.NewValue));
    public string Status { get => (string)GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
    public double TapeAngle { get => (double)GetValue(TapeAngleProperty); set => SetValue(TapeAngleProperty, value); }
    public static readonly DependencyProperty TapeAngleProperty = DependencyProperty.Register(nameof(TapeAngle), typeof(double), typeof(ConceptNote), new PropertyMetadata(-4d, (sender, args) => ((ConceptNote)sender).TapeRotation.Angle = (double)args.NewValue));
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(nameof(Status), typeof(string), typeof(ConceptNote), new PropertyMetadata("Confirmed", (sender, _) => ((ConceptNote)sender).UpdatePaper()));
    public bool IsSelected { get => (bool)GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(ConceptNote), new PropertyMetadata(false, (sender, args) => ((ConceptNote)sender).SelectedEdge.Visibility = (bool)args.NewValue ? Visibility.Visible : Visibility.Collapsed));
    private void UpdatePaper()
    {
        var key = Status is "Preparing" or "Ready" ? Status : "Confirmed";
        Paper.Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources[$"Console{key}Brush"];
        Paper.BorderBrush = Fold.Fill = (Brush)Microsoft.UI.Xaml.Application.Current.Resources[$"Console{key}EdgeBrush"];
        Tape.Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources[$"Console{key}TapeBrush"];
    }
}
