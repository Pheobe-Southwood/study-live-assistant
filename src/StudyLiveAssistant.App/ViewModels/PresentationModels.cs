using StudyLiveAssistant.Core;

namespace StudyLiveAssistant.App.ViewModels;

public sealed class TaskCardViewModel
{
    public required StudyTask Task { get; init; }
    public string PrimaryName { get; init; } = string.Empty;
    public string SecondaryName { get; init; } = string.Empty;
    public string AccentColor { get; init; } = "#7A9E9F";
    public bool IsCurrent { get; init; }
    public bool IsNext { get; init; }
    public string Title => string.IsNullOrWhiteSpace(Task.Detail) ? $"{PrimaryName} · {SecondaryName}" : $"{PrimaryName} · {SecondaryName}｜{Task.Detail}";
    public string TargetTimeText => $"目标 {Task.ScheduledStart:HH:mm}";
    public string ActualStartText => Task.ActualStartedAt is { } actual ? $"实际 {actual.LocalDateTime:HH:mm}" : "实际 --:--";
    public string ElapsedText => $"已用 {FormatDuration(Task.Elapsed)}";
    public string ProgressText => $"{Task.CurrentValue:0.#} / {Task.TargetValue:0.#} {TaskRules.UnitText(Task)}";
    public string ProgressTypeText => Task.ProgressKind == ProgressKind.Time ? "时间型" : $"计数型 · {TaskRules.UnitText(Task)}";
    public double ProgressPercent => Task.ProgressRatio * 100;
    public string StatusText => Task.Status switch { StudyTaskStatus.Completed => "已完成", StudyTaskStatus.InProgress => "进行中", _ => "待开始" };

    public static string FormatDuration(TimeSpan duration) => duration.TotalHours >= 1
        ? $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}"
        : $"{duration.Minutes:00}:{duration.Seconds:00}";
}

public sealed record ColorPreset(string Name, string Hex);

public sealed class HotkeyEditRow : BindableBase
{
    private string _modifier = "Ctrl+Alt";
    private string _key = "Space";

    public HotkeyAction Action { get; init; }
    public string ActionText => Action switch
    {
        HotkeyAction.PlayPause => "播放 / 暂停",
        HotkeyAction.Previous => "上一任务",
        HotkeyAction.Next => "下一任务",
        HotkeyAction.Increment => "进度 +1",
        HotkeyAction.Decrement => "进度 -1",
        _ => "显示 / 隐藏学习窗口"
    };
    public string Modifier { get => _modifier; set => SetProperty(ref _modifier, value); }
    public string Key { get => _key; set => SetProperty(ref _key, value); }
}

public static class HotkeyMapping
{
    public static readonly string[] Modifiers = ["Ctrl+Alt", "Ctrl+Shift", "Alt+Shift"];
    public static readonly string[] Keys = ["Space", "Left", "Right", "Up", "Down", "S", "P", "N", "F7", "F8", "F9"];

    public static HotkeyEditRow ToEditRow(HotkeyBinding binding)
    {
        var modifier = binding.Modifiers switch
        {
            0x0006 => "Ctrl+Shift",
            0x0005 => "Alt+Shift",
            _ => "Ctrl+Alt"
        };
        return new HotkeyEditRow { Action = binding.Action, Modifier = modifier, Key = KeyName(binding.VirtualKey) };
    }

    public static HotkeyBinding ToBinding(HotkeyEditRow row)
    {
        var modifiers = row.Modifier switch { "Ctrl+Shift" => 0x0006u, "Alt+Shift" => 0x0005u, _ => 0x0003u };
        return new HotkeyBinding
        {
            Action = row.Action, Modifiers = modifiers, VirtualKey = VirtualKey(row.Key),
            DisplayText = $"{row.Modifier}+{row.Key}"
        };
    }

    private static string KeyName(uint key) => key switch
    {
        0x20 => "Space", 0x25 => "Left", 0x27 => "Right", 0x26 => "Up", 0x28 => "Down",
        0x53 => "S", 0x50 => "P", 0x4E => "N", 0x76 => "F7", 0x77 => "F8", 0x78 => "F9", _ => "Space"
    };

    private static uint VirtualKey(string key) => key switch
    {
        "Left" => 0x25, "Right" => 0x27, "Up" => 0x26, "Down" => 0x28,
        "S" => 0x53, "P" => 0x50, "N" => 0x4E, "F7" => 0x76, "F8" => 0x77, "F9" => 0x78, _ => 0x20
    };
}
