using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;
using StudyLiveAssistant.App.ViewModels;

namespace StudyLiveAssistant.App.Views;

public partial class LiveWindow : Window
{
    private const int WmGetMinMaxInfo = 0x0024;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private readonly ILiveWindowViewModel _viewModel;
    private HwndSource? _hwndSource;
    private bool _allowPermanentClose;
    private bool _acceptLocationChanges;

    internal LiveWindow(ILiveWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.PropertyChanged += ViewModelOnPropertyChanged;
        ApplyConfiguredBounds();
    }

    public event EventHandler? HideRequested;

    public void ApplyAppearance()
    {
        _viewModel.RefreshAppearance();
        ApplyConfiguredBounds();
        StartTopAnimation();
    }

    internal void ApplyConfiguredBounds()
    {
        var width = _viewModel.CanvasWidth;
        var height = _viewModel.CanvasHeight;
        var left = _viewModel.Appearance.WindowLeft;
        var top = _viewModel.Appearance.WindowTop;
        Width = width;
        Height = height;
        Left = left;
        Top = top;

        if (_hwndSource is null || _hwndSource.Handle == nint.Zero) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        if (!SetWindowPos(
                _hwndSource.Handle,
                nint.Zero,
                ToDevicePixels(left, dpi.DpiScaleX),
                ToDevicePixels(top, dpi.DpiScaleY),
                ToDevicePixels(width, dpi.DpiScaleX),
                ToDevicePixels(height, dpi.DpiScaleY),
                SwpNoZOrder | SwpNoActivate))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法应用直播窗口尺寸。");
        }
        _acceptLocationChanges = true;
    }

    public void RequestHide()
    {
        if (IsVisible) Hide();
        HideRequested?.Invoke(this, EventArgs.Empty);
    }

    public void ClosePermanently()
    {
        _allowPermanentClose = true;
        Close();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        _hwndSource = (HwndSource?)PresentationSource.FromVisual(this);
        _hwndSource?.AddHook(WindowProcedure);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) => StartTopAnimation();

    private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Window_LocationChanged(object? sender, EventArgs e)
    {
        if (!_acceptLocationChanges) return;
        _viewModel.Appearance.WindowLeft = Left;
        _viewModel.Appearance.WindowTop = Top;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowPermanentClose) return;
        e.Cancel = true;
        RequestHide();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _hwndSource?.RemoveHook(WindowProcedure);
        _hwndSource = null;
        _viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
        _viewModel.Dispose();
    }

    private nint WindowProcedure(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message != WmGetMinMaxInfo || lParam == nint.Zero) return nint.Zero;

        var info = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var dpi = VisualTreeHelper.GetDpi(this);
        info.MaxTrackSize.X = Math.Max(info.MaxTrackSize.X, ToDevicePixels(_viewModel.CanvasWidth, dpi.DpiScaleX));
        info.MaxTrackSize.Y = Math.Max(info.MaxTrackSize.Y, ToDevicePixels(_viewModel.CanvasHeight, dpi.DpiScaleY));
        Marshal.StructureToPtr(info, lParam, false);
        handled = true;
        return nint.Zero;
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

    private static int ToDevicePixels(double value, double scale) =>
        checked((int)Math.Round(value * scale, MidpointRounding.AwayFromZero));

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint hWnd,
        nint hWndInsertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }
}
