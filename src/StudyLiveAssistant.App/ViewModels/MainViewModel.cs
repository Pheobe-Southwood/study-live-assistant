using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using StudyLiveAssistant.Core;
using CountdownModel = StudyLiveAssistant.Core.CountdownEvent;

namespace StudyLiveAssistant.App.ViewModels;

public sealed class MainViewModel : BindableBase
{
    private readonly AppRuntime _runtime;
    private DateTime _selectedDate = DateTime.Today;
    private TaskCategory? _selectedPrimaryCategory;
    private TaskCategory? _selectedSecondaryCategory;
    private TaskCategory? _selectedCategoryToDelete;
    private TaskCardViewModel? _selectedTaskRow;
    private Guid? _editingTaskId;
    private string _taskDetail = string.Empty;
    private string _startTimeText = "08:00";
    private ProgressKind _selectedProgressKind;
    private ProgressUnit _selectedUnit = ProgressUnit.Minute;
    private string _customUnit = string.Empty;
    private double _targetValue = 60;
    private double _adjustmentStep = 1;
    private int? _expectedMinutes = 60;
    private int? _dailyTargetMinutes;
    private string _newPrimaryName = string.Empty;
    private string _newSecondaryName = string.Empty;
    private string _newCategoryColor = "#7A9E9F";
    private string _selectedCategoryName = string.Empty;
    private string _selectedCategoryAccent = "#7A9E9F";
    private CountdownModel? _selectedCountdown;
    private string _countdownName = "目标日";
    private DateTime _countdownDate = DateTime.Today.AddDays(100);
    private string _statusMessage = "准备就绪";
    private string _todayStatistics = "--";
    private string _weekStatistics = "--";
    private string _monthStatistics = "--";

    public MainViewModel(AppRuntime runtime)
    {
        _runtime = runtime;
        ShowLiveCommand = new RelayCommand(runtime.ShowLiveWindow);
        CloseLiveCommand = new RelayCommand(runtime.CloseLiveWindow);
        EnterStudyModeCommand = new AsyncCommand(runtime.EnterStudyModeAsync, onError: ex => runtime.ReportError(ex, "开启学习模式"));
        SaveAppearanceCommand = new AsyncCommand(SaveAppearanceAsync, onError: ex => runtime.ReportError(ex, "保存基础配置"));
        ClearBackgroundCommand = new AsyncCommand(ClearBackgroundAsync, onError: ex => runtime.ReportError(ex, "清除背景图"));
        SaveTaskCommand = new AsyncCommand(SaveTaskAsync, onError: ex => runtime.ReportError(ex, "保存任务"));
        EditTaskCommand = new RelayCommand(EditSelectedTask);
        ResetTaskEditorCommand = new RelayCommand(ResetTaskEditor);
        DeleteTaskCommand = new AsyncCommand(DeleteTaskAsync, () => SelectedTaskRow is not null, ex => runtime.ReportError(ex, "删除任务"));
        CopyPreviousDayCommand = new AsyncCommand(CopyPreviousDayAsync, onError: ex => runtime.ReportError(ex, "复制任务"));
        SaveDailyTargetCommand = new AsyncCommand(SaveDailyTargetAsync, onError: ex => runtime.ReportError(ex, "保存今日目标"));
        AddPrimaryCategoryCommand = new AsyncCommand(AddPrimaryCategoryAsync, onError: ex => runtime.ReportError(ex, "新增一级类型"));
        AddSecondaryCategoryCommand = new AsyncCommand(AddSecondaryCategoryAsync, onError: ex => runtime.ReportError(ex, "新增二级类型"));
        DeleteCategoryCommand = new AsyncCommand(DeleteCategoryAsync, () => SelectedCategoryToDelete is not null, ex => runtime.ReportError(ex, "删除任务类型"));
        UpdateCategoryCommand = new AsyncCommand(UpdateCategoryAsync, () => SelectedCategoryToDelete is not null, ex => runtime.ReportError(ex, "更新任务类型"));
        SaveCountdownCommand = new AsyncCommand(SaveCountdownAsync, onError: ex => runtime.ReportError(ex, "保存倒数日"));
        NewCountdownCommand = new RelayCommand(NewCountdown);
        DeleteCountdownCommand = new AsyncCommand(DeleteCountdownAsync, () => SelectedCountdown is not null, ex => runtime.ReportError(ex, "删除倒数日"));
        SaveHotkeysCommand = new AsyncCommand(SaveHotkeysAsync, onError: ex => runtime.ReportError(ex, "保存快捷键"));
        ReloadStatisticsCommand = new AsyncCommand(LoadStatisticsAsync, onError: ex => runtime.ReportError(ex, "读取统计"));
    }

    public ObservableCollection<TaskCategory> PrimaryCategories { get; } = [];
    public ObservableCollection<TaskCategory> SecondaryCategories { get; } = [];
    public ObservableCollection<TaskCategory> AllCategories { get; } = [];
    public ObservableCollection<TaskCardViewModel> TaskRows { get; } = [];
    public ObservableCollection<CountdownModel> Countdowns { get; } = [];
    public ObservableCollection<HotkeyEditRow> HotkeyRows { get; } = [];
    public ObservableCollection<ProgressUnit> AvailableProgressUnits { get; } = [];

    public IReadOnlyList<ProgressKind> ProgressKinds { get; } = Enum.GetValues<ProgressKind>();
    public IReadOnlyList<CardTheme> Themes { get; } = Enum.GetValues<CardTheme>();
    public IReadOnlyList<ProgressBarStyle> ProgressStyles { get; } = Enum.GetValues<ProgressBarStyle>();
    public IReadOnlyList<string> ModifierOptions => HotkeyMapping.Modifiers;
    public IReadOnlyList<string> KeyOptions => HotkeyMapping.Keys;
    public IReadOnlyList<ColorPreset> InfoColorPresets { get; } =
    [
        new("雾白", "#F3F4F6"), new("薄荷", "#E8F3EF"), new("晴空", "#E8F1F8"),
        new("浅杏", "#F7F0E5"), new("淡粉", "#F7ECEF"), new("夜蓝", "#263447")
    ];
    public AppearanceSettings Appearance => _runtime.Settings.Appearance;

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (!SetProperty(ref _selectedDate, value)) return;
            _ = LoadTasksSafeAsync();
        }
    }

    public TaskCategory? SelectedPrimaryCategory
    {
        get => _selectedPrimaryCategory;
        set
        {
            if (!SetProperty(ref _selectedPrimaryCategory, value)) return;
            RefreshSecondaryCategories();
        }
    }
    public TaskCategory? SelectedSecondaryCategory { get => _selectedSecondaryCategory; set => SetProperty(ref _selectedSecondaryCategory, value); }
    public TaskCategory? SelectedCategoryToDelete
    {
        get => _selectedCategoryToDelete;
        set
        {
            if (!SetProperty(ref _selectedCategoryToDelete, value)) return;
            (DeleteCategoryCommand as AsyncCommand)?.NotifyCanExecuteChanged();
            (UpdateCategoryCommand as AsyncCommand)?.NotifyCanExecuteChanged();
            if (value is null) return;
            SelectedCategoryName = value.Name;
            SelectedCategoryAccent = value.AccentColor;
        }
    }
    public TaskCardViewModel? SelectedTaskRow
    {
        get => _selectedTaskRow;
        set
        {
            if (!SetProperty(ref _selectedTaskRow, value)) return;
            (DeleteTaskCommand as AsyncCommand)?.NotifyCanExecuteChanged();
        }
    }
    public string TaskDetail { get => _taskDetail; set => SetProperty(ref _taskDetail, value); }
    public string StartTimeText { get => _startTimeText; set => SetProperty(ref _startTimeText, value); }
    public ProgressKind SelectedProgressKind
    {
        get => _selectedProgressKind;
        set
        {
            if (!SetProperty(ref _selectedProgressKind, value)) return;
            RefreshProgressUnits();
            ExpectedMinutes ??= value == ProgressKind.Time ? (int)Math.Ceiling(TargetValue) : null;
        }
    }
    public ProgressUnit SelectedUnit { get => _selectedUnit; set => SetProperty(ref _selectedUnit, value); }
    public string CustomUnit { get => _customUnit; set => SetProperty(ref _customUnit, value); }
    public double TargetValue { get => _targetValue; set => SetProperty(ref _targetValue, value); }
    public double AdjustmentStep { get => _adjustmentStep; set => SetProperty(ref _adjustmentStep, value); }
    public int? ExpectedMinutes { get => _expectedMinutes; set => SetProperty(ref _expectedMinutes, value); }
    public int? DailyTargetMinutes { get => _dailyTargetMinutes; set => SetProperty(ref _dailyTargetMinutes, value); }
    public string NewPrimaryName { get => _newPrimaryName; set => SetProperty(ref _newPrimaryName, value); }
    public string NewSecondaryName { get => _newSecondaryName; set => SetProperty(ref _newSecondaryName, value); }
    public string NewCategoryColor { get => _newCategoryColor; set => SetProperty(ref _newCategoryColor, value); }
    public string SelectedCategoryName { get => _selectedCategoryName; set => SetProperty(ref _selectedCategoryName, value); }
    public string SelectedCategoryAccent { get => _selectedCategoryAccent; set => SetProperty(ref _selectedCategoryAccent, value); }
    public CountdownModel? SelectedCountdown
    {
        get => _selectedCountdown;
        set
        {
            if (!SetProperty(ref _selectedCountdown, value)) return;
            (DeleteCountdownCommand as AsyncCommand)?.NotifyCanExecuteChanged();
            if (value is null) return;
            CountdownName = value.Name;
            CountdownDate = value.TargetDate.ToDateTime(TimeOnly.MinValue);
        }
    }
    public string CountdownName { get => _countdownName; set => SetProperty(ref _countdownName, value); }
    public DateTime CountdownDate { get => _countdownDate; set => SetProperty(ref _countdownDate, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string TodayStatistics { get => _todayStatistics; set => SetProperty(ref _todayStatistics, value); }
    public string WeekStatistics { get => _weekStatistics; set => SetProperty(ref _weekStatistics, value); }
    public string MonthStatistics { get => _monthStatistics; set => SetProperty(ref _monthStatistics, value); }
    public string WindowStatus => _runtime.IsLiveWindowOpen ? "直播窗口：已启动" : "直播窗口：未启动";
    public string TimerStatus => _runtime.Engine.IsRunning ? "当前计时：进行中" : "当前计时：已暂停";
    public string CurrentTaskStatus => _runtime.Engine.CurrentTask is { } task ? $"当前任务：{FindCategoryName(task.PrimaryCategoryId)} · {FindCategoryName(task.SecondaryCategoryId)}" : "当前任务：今日暂无任务";

    public ICommand ShowLiveCommand { get; }
    public ICommand CloseLiveCommand { get; }
    public ICommand EnterStudyModeCommand { get; }
    public ICommand SaveAppearanceCommand { get; }
    public ICommand ClearBackgroundCommand { get; }
    public ICommand SaveTaskCommand { get; }
    public ICommand EditTaskCommand { get; }
    public ICommand ResetTaskEditorCommand { get; }
    public ICommand DeleteTaskCommand { get; }
    public ICommand CopyPreviousDayCommand { get; }
    public ICommand SaveDailyTargetCommand { get; }
    public ICommand AddPrimaryCategoryCommand { get; }
    public ICommand AddSecondaryCategoryCommand { get; }
    public ICommand DeleteCategoryCommand { get; }
    public ICommand UpdateCategoryCommand { get; }
    public ICommand SaveCountdownCommand { get; }
    public ICommand NewCountdownCommand { get; }
    public ICommand DeleteCountdownCommand { get; }
    public ICommand SaveHotkeysCommand { get; }
    public ICommand ReloadStatisticsCommand { get; }

    public async Task InitializeAsync()
    {
        RefreshProgressUnits();
        await ReloadCategoriesAsync();
        await LoadTasksAsync();
        ReloadCountdowns();
        HotkeyRows.Clear();
        foreach (var binding in _runtime.Settings.Hotkeys) HotkeyRows.Add(HotkeyMapping.ToEditRow(binding));
        await LoadStatisticsAsync();
        RefreshStatus();
    }

    public async Task ImportBackgroundAsync(string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg" or ".bmp")) throw new ArgumentException("请选择 PNG、JPG 或 BMP 图片。");
        Directory.CreateDirectory(_runtime.AssetsDirectory);
        var destination = Path.Combine(_runtime.AssetsDirectory, $"info-background-{Guid.NewGuid():N}{extension}");
        File.Copy(sourcePath, destination, false);
        Appearance.InfoBackgroundImage = destination;
        RaisePropertyChanged(nameof(Appearance));
        await _runtime.SaveSettingsAsync();
        StatusMessage = "背景图已导入。";
    }

    public void RefreshStatus()
    {
        RaisePropertyChanged(nameof(WindowStatus));
        RaisePropertyChanged(nameof(TimerStatus));
        RaisePropertyChanged(nameof(CurrentTaskStatus));
    }

    public void SetCanvasSize(int width, int height)
    {
        Appearance.CanvasWidth = width;
        Appearance.CanvasHeight = height;
        RaisePropertyChanged(nameof(Appearance));
        StatusMessage = $"已选择 {width} × {height} 画布，保存后应用。";
    }

    private async Task LoadTasksSafeAsync()
    {
        try { await LoadTasksAsync(); }
        catch (Exception exception) { _runtime.ReportError(exception, "读取任务"); }
    }

    private async Task LoadTasksAsync()
    {
        var date = DateOnly.FromDateTime(SelectedDate);
        var tasks = await _runtime.Database.GetTasksAsync(date);
        SelectedTaskRow = null;
        TaskRows.Clear();
        foreach (var task in tasks) TaskRows.Add(CreateTaskRow(task, false, false));
        var plan = await _runtime.Database.GetDailyPlanAsync(date);
        DailyTargetMinutes = plan?.TargetStudyMinutes;
        ResetTaskEditor();
        StatusMessage = $"已加载 {date:yyyy-MM-dd} 的 {tasks.Count} 项任务。";
    }

    private async Task SaveTaskAsync()
    {
        if (SelectedPrimaryCategory is null || SelectedSecondaryCategory is null) throw new ArgumentException("请选择一级和二级类型。");
        if (!TimeOnly.TryParseExact(StartTimeText.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
            throw new ArgumentException("起始时间请使用 HH:mm 格式，例如 08:30。");

        var existing = _editingTaskId is { } id ? TaskRows.FirstOrDefault(row => row.Task.Id == id)?.Task : null;
        var task = existing ?? new StudyTask();
        task.Date = DateOnly.FromDateTime(SelectedDate);
        task.PrimaryCategoryId = SelectedPrimaryCategory.Id;
        task.SecondaryCategoryId = SelectedSecondaryCategory.Id;
        task.Detail = TaskDetail;
        task.ScheduledStart = start;
        task.ProgressKind = SelectedProgressKind;
        task.Unit = SelectedUnit;
        task.CustomUnit = CustomUnit;
        task.TargetValue = TargetValue;
        task.AdjustmentStep = AdjustmentStep;
        task.ExpectedTotalMinutes = ExpectedMinutes;
        TaskRules.Validate(task);
        await _runtime.Database.SaveTaskAsync(task);
        await LoadTasksAsync();
        if (task.Date == DateOnly.FromDateTime(DateTime.Today)) await _runtime.ReloadTodayAsync();
        StatusMessage = existing is null ? "任务已添加。" : "任务已更新。";
    }

    private void EditSelectedTask()
    {
        if (SelectedTaskRow is null) return;
        var task = SelectedTaskRow.Task;
        _editingTaskId = task.Id;
        SelectedPrimaryCategory = PrimaryCategories.FirstOrDefault(c => c.Id == task.PrimaryCategoryId);
        SelectedSecondaryCategory = SecondaryCategories.FirstOrDefault(c => c.Id == task.SecondaryCategoryId);
        TaskDetail = task.Detail;
        StartTimeText = task.ScheduledStart.ToString("HH:mm");
        SelectedProgressKind = task.ProgressKind;
        SelectedUnit = task.Unit;
        CustomUnit = task.CustomUnit;
        TargetValue = task.TargetValue;
        AdjustmentStep = task.AdjustmentStep;
        ExpectedMinutes = task.ExpectedTotalMinutes;
        StatusMessage = "正在编辑所选任务。";
    }

    private void ResetTaskEditor()
    {
        _editingTaskId = null;
        TaskDetail = string.Empty;
        StartTimeText = "08:00";
        SelectedProgressKind = ProgressKind.Time;
        SelectedUnit = ProgressUnit.Minute;
        CustomUnit = string.Empty;
        TargetValue = 60;
        AdjustmentStep = 1;
        ExpectedMinutes = 60;
        SelectedPrimaryCategory ??= PrimaryCategories.FirstOrDefault();
        RefreshSecondaryCategories();
    }

    private async Task DeleteTaskAsync()
    {
        if (SelectedTaskRow is null) return;
        var task = SelectedTaskRow.Task;
        await _runtime.Database.DeleteTaskAsync(task.Id);
        await LoadTasksAsync();
        if (task.Date == DateOnly.FromDateTime(DateTime.Today)) await _runtime.ReloadTodayAsync();
        StatusMessage = "任务已删除。";
    }

    private async Task CopyPreviousDayAsync()
    {
        var destination = DateOnly.FromDateTime(SelectedDate);
        await _runtime.Database.CopyTasksAsync(destination.AddDays(-1), destination);
        await LoadTasksAsync();
        if (destination == DateOnly.FromDateTime(DateTime.Today)) await _runtime.ReloadTodayAsync();
        StatusMessage = "已复制前一天的任务。";
    }

    private async Task SaveDailyTargetAsync()
    {
        if (DailyTargetMinutes is <= 0) throw new ArgumentException("每日目标时长应大于零，或留空使用任务预计时长合计。");
        await _runtime.Database.SaveDailyPlanAsync(new DailyPlan { Date = DateOnly.FromDateTime(SelectedDate), TargetStudyMinutes = DailyTargetMinutes });
        StatusMessage = "每日目标时长已保存。";
    }

    private async Task AddPrimaryCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewPrimaryName)) throw new ArgumentException("一级类型名称不能为空。");
        var category = new TaskCategory { Name = NewPrimaryName.Trim(), AccentColor = NewCategoryColor, SortOrder = PrimaryCategories.Count };
        await _runtime.Database.SaveCategoryAsync(category);
        NewPrimaryName = string.Empty;
        await ReloadCategoriesAsync();
        SelectedPrimaryCategory = PrimaryCategories.FirstOrDefault(item => item.Id == category.Id);
        StatusMessage = "一级类型已添加。";
    }

    private async Task AddSecondaryCategoryAsync()
    {
        if (SelectedPrimaryCategory is null) throw new ArgumentException("请先选择所属一级类型。");
        if (string.IsNullOrWhiteSpace(NewSecondaryName)) throw new ArgumentException("二级类型名称不能为空。");
        var category = new TaskCategory
        {
            ParentId = SelectedPrimaryCategory.Id, Name = NewSecondaryName.Trim(),
            AccentColor = SelectedPrimaryCategory.AccentColor, SortOrder = SecondaryCategories.Count
        };
        await _runtime.Database.SaveCategoryAsync(category);
        NewSecondaryName = string.Empty;
        await ReloadCategoriesAsync();
        SelectedSecondaryCategory = SecondaryCategories.FirstOrDefault(item => item.Id == category.Id);
        StatusMessage = "二级类型已添加。";
    }

    private async Task DeleteCategoryAsync()
    {
        if (SelectedCategoryToDelete is null) return;
        await _runtime.Database.DeleteCategoryAsync(SelectedCategoryToDelete.Id);
        await ReloadCategoriesAsync();
        StatusMessage = "任务类型已删除。";
    }

    private async Task UpdateCategoryAsync()
    {
        if (SelectedCategoryToDelete is null) return;
        if (string.IsNullOrWhiteSpace(SelectedCategoryName)) throw new ArgumentException("类型名称不能为空。");
        SelectedCategoryToDelete.Name = SelectedCategoryName.Trim();
        SelectedCategoryToDelete.AccentColor = SelectedCategoryAccent.Trim();
        await _runtime.Database.SaveCategoryAsync(SelectedCategoryToDelete);
        await ReloadCategoriesAsync();
        if (DateOnly.FromDateTime(SelectedDate) == DateOnly.FromDateTime(DateTime.Today)) await _runtime.ReloadTodayAsync();
        StatusMessage = "任务类型已更新。";
    }

    private async Task ReloadCategoriesAsync()
    {
        var selectedPrimaryId = SelectedPrimaryCategory?.Id;
        await _runtime.RefreshReferenceDataAsync();
        AllCategories.Clear();
        PrimaryCategories.Clear();
        foreach (var category in _runtime.Categories)
        {
            AllCategories.Add(category);
            if (category.IsPrimary) PrimaryCategories.Add(category);
        }
        SelectedPrimaryCategory = PrimaryCategories.FirstOrDefault(c => c.Id == selectedPrimaryId) ?? PrimaryCategories.FirstOrDefault();
        RefreshSecondaryCategories();
    }

    private void RefreshSecondaryCategories()
    {
        var selectedId = SelectedSecondaryCategory?.Id;
        SecondaryCategories.Clear();
        if (SelectedPrimaryCategory is not null)
            foreach (var category in _runtime.Categories.Where(c => c.ParentId == SelectedPrimaryCategory.Id).OrderBy(c => c.SortOrder)) SecondaryCategories.Add(category);
        SelectedSecondaryCategory = SecondaryCategories.FirstOrDefault(c => c.Id == selectedId) ?? SecondaryCategories.FirstOrDefault();
    }

    private void RefreshProgressUnits()
    {
        AvailableProgressUnits.Clear();
        if (SelectedProgressKind == ProgressKind.Time)
        {
            AvailableProgressUnits.Add(ProgressUnit.Minute);
        }
        else
        {
            AvailableProgressUnits.Add(ProgressUnit.Chapter);
            AvailableProgressUnits.Add(ProgressUnit.Page);
            AvailableProgressUnits.Add(ProgressUnit.Question);
            AvailableProgressUnits.Add(ProgressUnit.Custom);
        }
        if (!AvailableProgressUnits.Contains(SelectedUnit)) SelectedUnit = AvailableProgressUnits[0];
    }

    private async Task SaveAppearanceAsync()
    {
        if (Appearance.CanvasWidth is < 640 or > 7680 || Appearance.CanvasHeight is < 360 or > 4320)
            throw new ArgumentException("画布尺寸应在 640×360 到 7680×4320 之间。");
        if (Appearance.TopBarRatio is < 0.05 or > 0.30 || Appearance.LeftBarRatio is < 0.10 or > 0.45)
            throw new ArgumentException("上栏比例应为 0.05–0.30，左栏比例应为 0.10–0.45。");
        await _runtime.SaveSettingsAsync();
        StatusMessage = "基础配置和显示样式已保存。";
    }

    private async Task ClearBackgroundAsync()
    {
        Appearance.InfoBackgroundImage = string.Empty;
        RaisePropertyChanged(nameof(Appearance));
        await _runtime.SaveSettingsAsync();
        StatusMessage = "已恢复纯色信息区背景。";
    }

    private void ReloadCountdowns()
    {
        Countdowns.Clear();
        foreach (var countdown in _runtime.Countdowns) Countdowns.Add(countdown);
        SelectedCountdown = Countdowns.FirstOrDefault();
    }

    private void NewCountdown()
    {
        SelectedCountdown = null;
        CountdownName = "新目标";
        CountdownDate = DateTime.Today.AddDays(30);
    }

    private async Task SaveCountdownAsync()
    {
        if (string.IsNullOrWhiteSpace(CountdownName)) throw new ArgumentException("倒数日名称不能为空。");
        var item = SelectedCountdown ?? new CountdownModel { SortOrder = Countdowns.Count };
        item.Name = CountdownName.Trim();
        item.TargetDate = DateOnly.FromDateTime(CountdownDate);
        await _runtime.Database.SaveCountdownEventAsync(item);
        await _runtime.RefreshReferenceDataAsync();
        ReloadCountdowns();
        StatusMessage = "倒数日已保存。";
    }

    private async Task DeleteCountdownAsync()
    {
        if (SelectedCountdown is null) return;
        await _runtime.Database.DeleteCountdownEventAsync(SelectedCountdown.Id);
        await _runtime.RefreshReferenceDataAsync();
        ReloadCountdowns();
        StatusMessage = "倒数日已删除。";
    }

    private async Task SaveHotkeysAsync()
    {
        var proposed = HotkeyRows.Select(HotkeyMapping.ToBinding).ToList();
        var errors = await _runtime.TryApplyHotkeysAsync(proposed);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        StatusMessage = "全局快捷键已保存并立即生效。";
    }

    private async Task LoadStatisticsAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var from = today.AddDays(-29);
        var sessions = await _runtime.Database.GetSessionsAsync(from, today);
        var tasks = new List<StudyTask>();
        for (var date = from; date <= today; date = date.AddDays(1)) tasks.AddRange(await _runtime.Database.GetTasksAsync(date));
        TodayStatistics = FormatStatistics(StatisticsCalculator.Calculate(sessions, tasks, today, today));
        WeekStatistics = FormatStatistics(StatisticsCalculator.Calculate(sessions, tasks, today.AddDays(-6), today));
        MonthStatistics = FormatStatistics(StatisticsCalculator.Calculate(sessions, tasks, from, today));
        StatusMessage = "统计数据已更新。";
    }

    private TaskCardViewModel CreateTaskRow(StudyTask task, bool current, bool next) => new()
    {
        Task = task, PrimaryName = FindCategoryName(task.PrimaryCategoryId), SecondaryName = FindCategoryName(task.SecondaryCategoryId),
        AccentColor = _runtime.Categories.FirstOrDefault(c => c.Id == task.PrimaryCategoryId)?.AccentColor ?? "#7A9E9F",
        IsCurrent = current, IsNext = next
    };

    private string FindCategoryName(Guid id) => _runtime.Categories.FirstOrDefault(c => c.Id == id)?.Name ?? "未分类";
    private static string FormatStatistics(StudyStatistics value) => $"{(int)value.Duration.TotalHours} 小时 {value.Duration.Minutes} 分钟 · 完成 {value.CompletedTasks} 项";
}
