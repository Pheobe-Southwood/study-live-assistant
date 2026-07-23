using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StudyLiveAssistant.Core;

namespace StudyLiveAssistant.App.ViewModels;

public sealed class LiveViewModel : BindableBase, IDisposable
{
    private readonly AppRuntime _runtime;
    private int? _dailyTarget;
    private string _topInfoText = string.Empty;

    public LiveViewModel(AppRuntime runtime)
    {
        _runtime = runtime;
        _runtime.Engine.StateChanged += EngineOnStateChanged;
        Refresh();
        _ = LoadDailyTargetAsync();
    }

    public ObservableCollection<TaskCardViewModel> TaskCards { get; } = [];
    public AppearanceSettings Appearance => _runtime.Settings.Appearance;
    public double CanvasWidth => Appearance.CanvasWidth;
    public double CanvasHeight => Appearance.CanvasHeight;
    public double TopHeight => Appearance.CanvasHeight * Appearance.TopBarRatio;
    public double LeftWidth => Appearance.CanvasWidth * Appearance.LeftBarRatio;
    public string TopInfoText { get => _topInfoText; private set => SetProperty(ref _topInfoText, value); }
    public Brush InfoBackgroundBrush => CreateInfoBrush();
    public Brush ChromaBrush => BrushFromColor(Appearance.ChromaColor, Brushes.Lime);
    public Brush FontBrush => BrushFromColor(Appearance.FontColor, Brushes.DarkSlateGray);
    public CardTheme Theme => Appearance.Theme;
    public bool ShowProgressBar => Appearance.ShowProgressBar;
    public bool ScrollTopBar => Appearance.ScrollTopBar;
    public double ScrollSpeed => Appearance.ScrollSpeed;

    public void RefreshAppearance()
    {
        RaisePropertyChanged(nameof(Appearance));
        RaisePropertyChanged(nameof(CanvasWidth));
        RaisePropertyChanged(nameof(CanvasHeight));
        RaisePropertyChanged(nameof(TopHeight));
        RaisePropertyChanged(nameof(LeftWidth));
        RaisePropertyChanged(nameof(InfoBackgroundBrush));
        RaisePropertyChanged(nameof(ChromaBrush));
        RaisePropertyChanged(nameof(FontBrush));
        RaisePropertyChanged(nameof(Theme));
        RaisePropertyChanged(nameof(ShowProgressBar));
        RaisePropertyChanged(nameof(ScrollTopBar));
        RaisePropertyChanged(nameof(ScrollSpeed));
        Refresh();
    }

    public void Dispose() => _runtime.Engine.StateChanged -= EngineOnStateChanged;

    private void EngineOnStateChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        var current = _runtime.Engine.CurrentTask;
        var next = _runtime.Engine.NextTask;
        TaskCards.Clear();
        foreach (var task in _runtime.Engine.Tasks)
        {
            TaskCards.Add(new TaskCardViewModel
            {
                Task = task,
                PrimaryName = CategoryName(task.PrimaryCategoryId), SecondaryName = CategoryName(task.SecondaryCategoryId),
                AccentColor = _runtime.Categories.FirstOrDefault(c => c.Id == task.PrimaryCategoryId)?.AccentColor ?? "#7A9E9F",
                IsCurrent = task.Id == current?.Id, IsNext = task.Id == next?.Id
            });
        }
        TopInfoText = BuildTopInfo();
    }

    private async Task LoadDailyTargetAsync()
    {
        try
        {
            var plan = await _runtime.Database.GetDailyPlanAsync(DateOnly.FromDateTime(DateTime.Today));
            _dailyTarget = plan?.TargetStudyMinutes;
            Refresh();
        }
        catch (Exception exception)
        {
            _runtime.ReportError(exception, "读取直播窗口数据", false);
        }
    }

    private string BuildTopInfo()
    {
        var parts = new List<string>();
        if (Appearance.ShowCountdown)
        {
            var countdown = _runtime.Countdowns.Where(c => c.IsEnabled).OrderBy(c => c.SortOrder).FirstOrDefault();
            if (countdown is not null)
            {
                var days = countdown.TargetDate.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;
                parts.Add(days switch
                {
                    > 0 => $"距离{countdown.Name}还有 {days} 天",
                    0 => $"{countdown.Name}就是今天",
                    _ => $"{countdown.Name}已过去 {-days} 天"
                });
            }
        }
        if (Appearance.ShowTodayDuration)
        {
            var elapsed = TimeSpan.FromSeconds(_runtime.Engine.Tasks.Sum(t => Math.Max(0, t.ElapsedSeconds)));
            var target = _dailyTarget ?? _runtime.Engine.Tasks.Sum(t => t.ExpectedTotalMinutes ?? (t.ProgressKind == ProgressKind.Time ? (int)Math.Ceiling(t.TargetValue) : 0));
            parts.Add($"今日学习 {FormatHours(elapsed)} / {target / 60:00}:{target % 60:00}");
        }
        if (Appearance.ShowCurrentTask && _runtime.Engine.CurrentTask is { } current)
            parts.Add($"当前：{CategoryName(current.PrimaryCategoryId)} · {CategoryName(current.SecondaryCategoryId)}");
        if (Appearance.ShowClock) parts.Add(DateTime.Now.ToString("HH:mm"));
        if (!string.IsNullOrWhiteSpace(Appearance.CustomTopText)) parts.Add(Appearance.CustomTopText.Trim());
        return string.Join("　·　", parts);
    }

    private Brush CreateInfoBrush()
    {
        if (!string.IsNullOrWhiteSpace(Appearance.InfoBackgroundImage) && File.Exists(Appearance.InfoBackgroundImage))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(Appearance.InfoBackgroundImage, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                return new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill, Opacity = 0.92 };
            }
            catch (IOException) { }
            catch (NotSupportedException) { }
        }
        return BrushFromColor(Appearance.InfoBackgroundColor, Brushes.Gainsboro);
    }

    private string CategoryName(Guid id) => _runtime.Categories.FirstOrDefault(c => c.Id == id)?.Name ?? "未分类";
    private static string FormatHours(TimeSpan value) => $"{(int)value.TotalHours:00}:{value.Minutes:00}";

    private static Brush BrushFromColor(string value, Brush fallback)
    {
        try
        {
            var converted = new BrushConverter().ConvertFromString(value) as Brush;
            if (converted is not null) { converted.Freeze(); return converted; }
        }
        catch (FormatException) { }
        return fallback;
    }
}
