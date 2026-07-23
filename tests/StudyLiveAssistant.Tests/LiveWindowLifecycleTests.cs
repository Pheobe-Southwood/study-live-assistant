using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
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
