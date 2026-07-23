using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using StudyLiveAssistant.App.ViewModels;
using StudyLiveAssistant.App.Views;
using StudyLiveAssistant.Core;

namespace StudyLiveAssistant.Tests;

public sealed class LiveWindowLifecycleTests
{
    [Fact]
    public void HideAndShow_ReusesHandleAndReappliesConfiguredBounds()
    {
        RunOnStaThread(() =>
        {
            var viewModel = new FakeLiveWindowViewModel
            {
                Appearance =
                {
                    CanvasWidth = 1280,
                    CanvasHeight = 720,
                    WindowLeft = 24,
                    WindowTop = 36
                }
            };
            var window = new LiveWindow(viewModel);
            var hideRequestCount = 0;
            window.HideRequested += (_, _) => hideRequestCount++;
            nint handle = 0;

            try
            {
                Assert.Equal(1280d, window.Width);
                Assert.Equal(720d, window.Height);
                Assert.Equal(24d, window.Left);
                Assert.Equal(36d, window.Top);

                window.Show();
                window.ApplyAppearance();
                handle = new WindowInteropHelper(window).Handle;
                Assert.NotEqual(nint.Zero, handle);
                Assert.True(IsWindow(handle));
                Assert.Equal(1280d, window.Width);
                Assert.Equal(720d, window.Height);
                AssertNativeSize(window, handle, 1280, 720);

                window.Close();
                Assert.False(window.IsVisible);
                Assert.True(IsWindow(handle));
                Assert.Equal(1, hideRequestCount);

                viewModel.Appearance.CanvasWidth = 1440;
                viewModel.Appearance.CanvasHeight = 810;
                window.Show();
                window.ApplyAppearance();

                Assert.Equal(handle, new WindowInteropHelper(window).Handle);
                Assert.Equal(1440d, window.Width);
                Assert.Equal(810d, window.Height);
                AssertNativeSize(window, handle, 1440, 810);

                window.ClosePermanently();
                Assert.True(viewModel.IsDisposed);
                Assert.False(IsWindow(handle));
            }
            finally
            {
                if (!viewModel.IsDisposed) window.ClosePermanently();
            }
        });
    }

    private static void AssertNativeSize(LiveWindow window, nint handle, double width, double height)
    {
        Assert.True(GetWindowRect(handle, out var bounds));
        var dpi = VisualTreeHelper.GetDpi(window);
        Assert.Equal((int)Math.Round(width * dpi.DpiScaleX), bounds.Right - bounds.Left);
        Assert.Equal((int)Math.Round(height * dpi.DpiScaleY), bounds.Bottom - bounds.Top);
        window.UpdateLayout();
        Assert.InRange(Math.Abs(window.ActualWidth - width), 0, 0.5);
        Assert.InRange(Math.Abs(window.ActualHeight - height), 0, 0.5);
        var content = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
        Assert.InRange(Math.Abs(content.ActualWidth - width), 0, 0.5);
        Assert.InRange(Math.Abs(content.ActualHeight - height), 0, 0.5);
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                captured = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "WPF STA 测试未在限定时间内完成。");
        if (captured is not null) ExceptionDispatchInfo.Capture(captured).Throw();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hWnd, out NativeRect lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private sealed class FakeLiveWindowViewModel : ILiveWindowViewModel
    {
        public AppearanceSettings Appearance { get; } = new();
        public double CanvasWidth => Appearance.CanvasWidth;
        public double CanvasHeight => Appearance.CanvasHeight;
        public bool ScrollTopBar => Appearance.ScrollTopBar;
        public double ScrollSpeed => Appearance.ScrollSpeed;
        public string TopInfoText => "测试";
        public Brush InfoBackgroundBrush => Brushes.Gainsboro;
        public Brush ChromaBrush => Brushes.Lime;
        public Brush FontBrush => Brushes.DarkSlateGray;
        public CardTheme Theme => Appearance.Theme;
        public bool ShowProgressBar => Appearance.ShowProgressBar;
        public ObservableCollection<TaskCardViewModel> TaskCards { get; } = [];
        public bool IsDisposed { get; private set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void RefreshAppearance() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));

        public void Dispose() => IsDisposed = true;
    }
}
