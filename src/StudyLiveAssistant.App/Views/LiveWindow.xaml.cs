using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;
using StudyLiveAssistant.App.ViewModels;

namespace StudyLiveAssistant.App.Views;

public partial class LiveWindow : Window
{
    private readonly LiveViewModel _viewModel;

    public LiveWindow(LiveViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.PropertyChanged += ViewModelOnPropertyChanged;
    }

    public void ApplyAppearance()
    {
        _viewModel.RefreshAppearance();
        Width = _viewModel.CanvasWidth;
        Height = _viewModel.CanvasHeight;
        StartTopAnimation();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) => StartTopAnimation();

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) DragMove();
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        _viewModel.Appearance.WindowLeft = Left;
        _viewModel.Appearance.WindowTop = Top;
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
        _viewModel.Dispose();
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LiveViewModel.TopInfoText) or nameof(LiveViewModel.ScrollTopBar)) StartTopAnimation();
    }

    private void StartTopAnimation()
    {
        if (!IsLoaded) return;
        var transform = TopInfoText.RenderTransform as TranslateTransform ?? new TranslateTransform();
        TopInfoText.RenderTransform = transform;
        transform.BeginAnimation(TranslateTransform.XProperty, null);
        if (!_viewModel.ScrollTopBar)
        {
            TopInfoText.HorizontalAlignment = HorizontalAlignment.Center;
            transform.X = 0;
            return;
        }

        TopInfoText.HorizontalAlignment = HorizontalAlignment.Left;
        TopInfoText.Measure(new Size(double.PositiveInfinity, TopInfoViewport.ActualHeight));
        var distance = Math.Max(1, TopInfoViewport.ActualWidth + TopInfoText.DesiredSize.Width);
        var animation = new DoubleAnimation
        {
            From = TopInfoViewport.ActualWidth,
            To = -TopInfoText.DesiredSize.Width,
            Duration = TimeSpan.FromSeconds(distance / Math.Max(15, _viewModel.ScrollSpeed)),
            RepeatBehavior = RepeatBehavior.Forever
        };
        transform.BeginAnimation(TranslateTransform.XProperty, animation);
    }
}
