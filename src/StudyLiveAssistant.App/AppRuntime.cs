using System.Windows;
using System.Windows.Threading;
using StudyLiveAssistant.App.Infrastructure;
using StudyLiveAssistant.App.ViewModels;
using StudyLiveAssistant.App.Views;
using StudyLiveAssistant.Core;
using CountdownModel = StudyLiveAssistant.Core.CountdownEvent;

namespace StudyLiveAssistant.App;

public sealed class AppRuntime : IDisposable
{
    private readonly string _dataDirectory;
    private readonly FileLogger _logger;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private MainWindow? _mainWindow;
    private LiveWindow? _liveWindow;
    private StudyWindow? _studyWindow;
    private GlobalHotkeyService? _hotkeys;
    private Guid? _lastPersistedTask;
    private bool _isShuttingDown;

    public AppRuntime()
    {
        _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StudyLiveAssistant");
        Database = new LocalDatabase(_dataDirectory);
        _logger = new FileLogger(Path.Combine(_dataDirectory, "Logs"));
        Engine = new StudySessionService(Database, new SystemClock());
        Settings = new AppSettings();
        _timer.Tick += TimerOnTick;
        Engine.StateChanged += EngineOnStateChanged;
    }

    public LocalDatabase Database { get; }
    public IStudySessionService Engine { get; }
    public AppSettings Settings { get; private set; }
    public IReadOnlyList<TaskCategory> Categories { get; private set; } = [];
    public IReadOnlyList<CountdownModel> Countdowns { get; private set; } = [];
    public MainViewModel MainViewModel { get; private set; } = null!;
    public string AssetsDirectory => Path.Combine(_dataDirectory, "Assets");
    public bool IsLiveWindowOpen => _liveWindow is not null;
    public bool IsStudyWindowOpen => _studyWindow is not null;

    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();
        Settings = await Database.LoadSettingsAsync();
        await RefreshReferenceDataAsync();
        await Engine.LoadDateAsync(DateOnly.FromDateTime(DateTime.Today), Settings.CurrentTaskId);
        _lastPersistedTask = Engine.CurrentTask?.Id;
        MainViewModel = new MainViewModel(this);
        await MainViewModel.InitializeAsync();
        _timer.Start();
    }

    public void AttachMainWindow(MainWindow window)
    {
        _mainWindow = window;
        window.SourceInitialized += MainWindowOnSourceInitialized;
    }

    public async Task RefreshReferenceDataAsync()
    {
        Categories = await Database.GetCategoriesAsync();
        Countdowns = await Database.GetCountdownEventsAsync();
    }

    public async Task ReloadTodayAsync()
    {
        var preferred = Engine.CurrentTask?.Id;
        await Engine.LoadDateAsync(DateOnly.FromDateTime(DateTime.Today), preferred);
    }

    public void ShowLiveWindow()
    {
        if (_liveWindow is not null)
        {
            _liveWindow.Activate();
            return;
        }
        var viewModel = new LiveViewModel(this);
        _liveWindow = new LiveWindow(viewModel);
        _liveWindow.Closed += (_, _) =>
        {
            Settings.Appearance.WindowLeft = _liveWindow?.Left ?? Settings.Appearance.WindowLeft;
            Settings.Appearance.WindowTop = _liveWindow?.Top ?? Settings.Appearance.WindowTop;
            _liveWindow = null;
            MainViewModel.RefreshStatus();
            if (!_isShuttingDown) _ = SaveSettingsSafeAsync();
        };
        _liveWindow.Show();
        MainViewModel.RefreshStatus();
    }

    public void CloseLiveWindow() => _liveWindow?.Close();

    public async Task EnterStudyModeAsync()
    {
        await ReloadTodayAsync();
        if (_studyWindow is null)
        {
            _studyWindow = new StudyWindow(new StudyViewModel(this));
            _studyWindow.Closed += (_, _) =>
            {
                _studyWindow = null;
                if (!_isShuttingDown)
                {
                    _mainWindow?.Show();
                    _mainWindow?.Activate();
                    MainViewModel.RefreshStatus();
                }
            };
            _studyWindow.Show();
        }
        else
        {
            _studyWindow.Show();
            _studyWindow.Activate();
        }
        _mainWindow?.Hide();
        MainViewModel.RefreshStatus();
    }

    public void ReturnToConfiguration()
    {
        _studyWindow?.Close();
        if (_studyWindow is null)
        {
            _mainWindow?.Show();
            _mainWindow?.Activate();
        }
    }

    public void ToggleStudyWindow()
    {
        if (_studyWindow is null) _ = EnterStudyModeAsync();
        else if (_studyWindow.IsVisible) _studyWindow.Hide();
        else { _studyWindow.Show(); _studyWindow.Activate(); }
    }

    public async Task<IReadOnlyList<string>> TryApplyHotkeysAsync(IReadOnlyList<HotkeyBinding> proposed)
    {
        if (_hotkeys is null) return ["快捷键服务尚未就绪。"];
        var previous = Settings.Hotkeys.Select(CloneBinding).ToList();
        var errors = _hotkeys.Register(proposed);
        if (errors.Count > 0)
        {
            _hotkeys.Register(previous);
            return errors;
        }
        Settings.Hotkeys = proposed.Select(CloneBinding).ToList();
        await Database.SaveSettingsAsync(Settings);
        return [];
    }

    public async Task SaveSettingsAsync()
    {
        await Database.SaveSettingsAsync(Settings);
        _liveWindow?.ApplyAppearance();
        MainViewModel.RefreshStatus();
    }

    public void Dispose()
    {
        _isShuttingDown = true;
        _timer.Stop();
        if (_mainWindow is not null) _mainWindow.SourceInitialized -= MainWindowOnSourceInitialized;
        try
        {
            Engine.PauseAsync("application-exit").GetAwaiter().GetResult();
            Settings.CurrentTaskId = Engine.CurrentTask?.Id;
            Database.SaveSettingsAsync(Settings).GetAwaiter().GetResult();
        }
        catch (Exception exception) { _logger.Error(exception, "关闭应用"); }
        _hotkeys?.Dispose();
        _studyWindow?.Close();
        _liveWindow?.Close();
        Database.Dispose();
    }

    private void MainWindowOnSourceInitialized(object? sender, EventArgs e)
    {
        if (_mainWindow is null || _hotkeys is not null) return;
        _mainWindow.SourceInitialized -= MainWindowOnSourceInitialized;
        try
        {
            _hotkeys = new GlobalHotkeyService(_mainWindow);
            _hotkeys.Attach();
            _hotkeys.Triggered += HotkeysOnTriggered;
            var errors = _hotkeys.Register(Settings.Hotkeys);
            if (errors.Count > 0) MainViewModel.StatusMessage = string.Join(" ", errors);
        }
        catch (Exception exception)
        {
            _hotkeys?.Dispose();
            _hotkeys = null;
            ReportError(exception, "初始化全局快捷键", false);
            MainViewModel.StatusMessage = "应用已启动，但全局快捷键初始化失败。";
        }
    }

    public void ReportError(Exception exception, string context, bool showMessage = true)
    {
        _logger.Error(exception, context);
        MainViewModel.StatusMessage = exception.Message;
        if (showMessage) MessageBox.Show(exception.Message, context, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private async void TimerOnTick(object? sender, EventArgs e)
    {
        try { await Engine.TickAsync(); }
        catch (Exception exception) { ReportError(exception, "保存计时进度", false); }
    }

    private void EngineOnStateChanged(object? sender, EventArgs e)
    {
        var current = Engine.CurrentTask?.Id;
        if (current != _lastPersistedTask)
        {
            _lastPersistedTask = current;
            Settings.CurrentTaskId = current;
            _ = SaveSettingsSafeAsync();
        }
        MainViewModel?.RefreshStatus();
    }

    private async void HotkeysOnTriggered(object? sender, HotkeyAction action)
    {
        try
        {
            switch (action)
            {
                case HotkeyAction.PlayPause:
                    if (Engine.IsRunning) await Engine.PauseAsync(); else await Engine.StartAsync();
                    break;
                case HotkeyAction.Previous: await Engine.MoveAsync(-1); break;
                case HotkeyAction.Next: await Engine.MoveAsync(1); break;
                case HotkeyAction.Increment: await Engine.AdjustAsync(1); break;
                case HotkeyAction.Decrement: await Engine.AdjustAsync(-1); break;
                case HotkeyAction.ToggleStudyWindow: ToggleStudyWindow(); break;
            }
        }
        catch (Exception exception) { ReportError(exception, "执行快捷键"); }
    }

    private async Task SaveSettingsSafeAsync()
    {
        try { await Database.SaveSettingsAsync(Settings); }
        catch (Exception exception) { _logger.Error(exception, "保存设置"); }
    }

    private static HotkeyBinding CloneBinding(HotkeyBinding source) => new()
    {
        Action = source.Action, Modifiers = source.Modifiers,
        VirtualKey = source.VirtualKey, DisplayText = source.DisplayText
    };
}
