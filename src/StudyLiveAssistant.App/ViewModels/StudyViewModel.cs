using System.Windows.Input;
using StudyLiveAssistant.Core;

namespace StudyLiveAssistant.App.ViewModels;

public sealed class StudyViewModel : BindableBase, IDisposable
{
    private readonly AppRuntime _runtime;
    private string _title = "今日暂无任务";
    private string _detail = "请先在配置页建立任务";
    private string _elapsed = "00:00";
    private string _progressText = "0 / 0";
    private double _progressPercent;
    private string _nextTask = "后续暂无任务";
    private string _playIcon = "▶";
    private string _statusMessage = "保持专注";

    public StudyViewModel(AppRuntime runtime)
    {
        _runtime = runtime;
        PreviousCommand = new AsyncCommand(() => runtime.Engine.MoveAsync(-1), onError: HandleError);
        PlayPauseCommand = new AsyncCommand(PlayPauseAsync, onError: HandleError);
        NextCommand = new AsyncCommand(() => runtime.Engine.MoveAsync(1), onError: HandleError);
        DecrementCommand = new AsyncCommand(() => runtime.Engine.AdjustAsync(-1), onError: HandleError);
        IncrementCommand = new AsyncCommand(() => runtime.Engine.AdjustAsync(1), onError: HandleError);
        CompleteCommand = new AsyncCommand(runtime.Engine.CompleteAsync, onError: HandleError);
        ReturnCommand = new RelayCommand(runtime.ReturnToConfiguration);
        runtime.Engine.StateChanged += EngineOnStateChanged;
        Refresh();
    }

    public string Title { get => _title; private set => SetProperty(ref _title, value); }
    public string Detail { get => _detail; private set => SetProperty(ref _detail, value); }
    public string Elapsed { get => _elapsed; private set => SetProperty(ref _elapsed, value); }
    public string ProgressText { get => _progressText; private set => SetProperty(ref _progressText, value); }
    public double ProgressPercent { get => _progressPercent; private set => SetProperty(ref _progressPercent, value); }
    public string NextTask { get => _nextTask; private set => SetProperty(ref _nextTask, value); }
    public string PlayIcon { get => _playIcon; private set => SetProperty(ref _playIcon, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public bool ShowProgressBar => _runtime.Settings.Appearance.ShowProgressBar;
    public ICommand PreviousCommand { get; }
    public ICommand PlayPauseCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand DecrementCommand { get; }
    public ICommand IncrementCommand { get; }
    public ICommand CompleteCommand { get; }
    public ICommand ReturnCommand { get; }

    public void Dispose() => _runtime.Engine.StateChanged -= EngineOnStateChanged;

    private async Task PlayPauseAsync()
    {
        if (_runtime.Engine.IsRunning) await _runtime.Engine.PauseAsync(); else await _runtime.Engine.StartAsync();
    }

    private void EngineOnStateChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        var task = _runtime.Engine.CurrentTask;
        if (task is null)
        {
            Title = "今日暂无任务"; Detail = "请返回配置页建立任务"; Elapsed = "00:00";
            ProgressText = "0 / 0"; ProgressPercent = 0; NextTask = "后续暂无任务"; PlayIcon = "▶";
            return;
        }
        Title = $"{CategoryName(task.PrimaryCategoryId)} · {CategoryName(task.SecondaryCategoryId)}";
        Detail = string.IsNullOrWhiteSpace(task.Detail) ? "专注完成这一项" : task.Detail;
        Elapsed = TaskCardViewModel.FormatDuration(task.Elapsed);
        ProgressText = $"{task.CurrentValue:0.#} / {task.TargetValue:0.#} {TaskRules.UnitText(task)}";
        ProgressPercent = task.ProgressRatio * 100;
        PlayIcon = _runtime.Engine.IsRunning ? "Ⅱ" : "▶";
        NextTask = _runtime.Engine.NextTask is { } next
            ? $"下一项　{CategoryName(next.PrimaryCategoryId)} · {CategoryName(next.SecondaryCategoryId)}"
            : "后续暂无任务";
        StatusMessage = task.Status == StudyTaskStatus.Completed ? "已达标，可手动进入下一项" : _runtime.Engine.IsRunning ? "正在专注" : "已暂停";
    }

    private string CategoryName(Guid id) => _runtime.Categories.FirstOrDefault(c => c.Id == id)?.Name ?? "未分类";
    private void HandleError(Exception exception) => _runtime.ReportError(exception, "学习模式操作");
}
