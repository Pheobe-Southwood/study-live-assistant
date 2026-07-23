using System.Threading;
using System.Windows;
using System.Windows.Threading;
using StudyLiveAssistant.App.Infrastructure;
using StudyLiveAssistant.App.Views;

namespace StudyLiveAssistant.App;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private AppRuntime? _runtime;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        _singleInstance = new Mutex(true, "PheobeSouthwood.StudyLiveAssistant", out var isFirst);
        if (!isFirst)
        {
            MessageBox.Show("自习直播助手已经在运行。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        try
        {
            _runtime = new AppRuntime();
            await _runtime.InitializeAsync();
            var mainWindow = new MainWindow(_runtime);
            MainWindow = mainWindow;
            _runtime.AttachMainWindow(mainWindow);
            mainWindow.Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show($"应用启动失败：{exception.Message}", "自习直播助手", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= App_DispatcherUnhandledException;
        _runtime?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (_runtime is not null) _runtime.ReportError(e.Exception, "界面操作失败");
        else MessageBox.Show(e.Exception.Message, "界面操作失败", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
