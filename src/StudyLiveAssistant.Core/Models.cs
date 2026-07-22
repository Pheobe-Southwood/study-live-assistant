namespace StudyLiveAssistant.Core;

public enum ProgressKind { Time = 0, Count = 1 }
public enum ProgressUnit { Minute = 0, Chapter = 1, Page = 2, Question = 3, Custom = 4 }
public enum StudyTaskStatus { Pending = 0, InProgress = 1, Completed = 2 }
public enum CardTheme { Focus = 0, Cute = 1, Game = 2 }
public enum ProgressBarStyle { Square = 0, Rounded = 1, Pill = 2, Segmented = 3 }
public enum HotkeyAction { PlayPause, Previous, Next, Increment, Decrement, ToggleStudyWindow }

public sealed class TaskCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AccentColor { get; set; } = "#7A9E9F";
    public int SortOrder { get; set; }
    public bool IsBuiltIn { get; set; }
    public bool IsPrimary => ParentId is null;
}

public sealed class StudyTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public Guid PrimaryCategoryId { get; set; }
    public Guid SecondaryCategoryId { get; set; }
    public string Detail { get; set; } = string.Empty;
    public TimeOnly ScheduledStart { get; set; } = new(8, 0);
    public ProgressKind ProgressKind { get; set; }
    public ProgressUnit Unit { get; set; } = ProgressUnit.Minute;
    public string CustomUnit { get; set; } = string.Empty;
    public double TargetValue { get; set; } = 60;
    public double CurrentValue { get; set; }
    public double AdjustmentStep { get; set; } = 1;
    public long ElapsedSeconds { get; set; }
    public int? ExpectedTotalMinutes { get; set; }
    public StudyTaskStatus Status { get; set; }
    public long CreationOrder { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public double ProgressRatio => TargetValue <= 0 ? 0 : Math.Clamp(CurrentValue / TargetValue, 0, 1);
    public TimeSpan Elapsed => TimeSpan.FromSeconds(Math.Max(0, ElapsedSeconds));
}

public sealed class DailyPlan
{
    public DateOnly Date { get; set; }
    public int? TargetStudyMinutes { get; set; }
}

public sealed class StudySession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }
    public long DurationSeconds { get; set; }
    public string EndReason { get; set; } = "heartbeat";
}

public sealed class CountdownEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "目标日";
    public DateOnly TargetDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(100));
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class AppearanceSettings
{
    public int CanvasWidth { get; set; } = 1920;
    public int CanvasHeight { get; set; } = 1080;
    public double TopBarRatio { get; set; } = 0.10;
    public double LeftBarRatio { get; set; } = 0.24;
    public string ChromaColor { get; set; } = "#00FF00";
    public string InfoBackgroundColor { get; set; } = "#F3F4F6";
    public string InfoBackgroundImage { get; set; } = string.Empty;
    public string FontFamily { get; set; } = "Microsoft YaHei UI";
    public double TopFontSize { get; set; } = 30;
    public double TitleFontSize { get; set; } = 26;
    public double BodyFontSize { get; set; } = 20;
    public string FontColor { get; set; } = "#22313A";
    public CardTheme Theme { get; set; } = CardTheme.Focus;
    public bool ShowProgressBar { get; set; } = true;
    public ProgressBarStyle ProgressStyle { get; set; } = ProgressBarStyle.Rounded;
    public bool ScrollTopBar { get; set; }
    public double ScrollSpeed { get; set; } = 40;
    public bool ShowCountdown { get; set; } = true;
    public bool ShowTodayDuration { get; set; } = true;
    public bool ShowCurrentTask { get; set; } = true;
    public bool ShowClock { get; set; } = true;
    public string CustomTopText { get; set; } = "专注当下，稳步向前";
    public double WindowLeft { get; set; } = 40;
    public double WindowTop { get; set; } = 40;
}

public sealed class HotkeyBinding
{
    public HotkeyAction Action { get; set; }
    public uint Modifiers { get; set; }
    public uint VirtualKey { get; set; }
    public string DisplayText { get; set; } = string.Empty;
}

public sealed class AppSettings
{
    public AppearanceSettings Appearance { get; set; } = new();
    public List<HotkeyBinding> Hotkeys { get; set; } = DefaultHotkeys.Create();
    public Guid? CurrentTaskId { get; set; }
    public string LastTaskDate { get; set; } = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
}

public sealed record StudyStatistics(TimeSpan Duration, int CompletedTasks);

public static class DefaultHotkeys
{
    public const uint Alt = 0x0001;
    public const uint Control = 0x0002;

    public static List<HotkeyBinding> Create() =>
    [
        new() { Action = HotkeyAction.PlayPause, Modifiers = Control | Alt, VirtualKey = 0x20, DisplayText = "Ctrl+Alt+Space" },
        new() { Action = HotkeyAction.Previous, Modifiers = Control | Alt, VirtualKey = 0x25, DisplayText = "Ctrl+Alt+Left" },
        new() { Action = HotkeyAction.Next, Modifiers = Control | Alt, VirtualKey = 0x27, DisplayText = "Ctrl+Alt+Right" },
        new() { Action = HotkeyAction.Increment, Modifiers = Control | Alt, VirtualKey = 0x26, DisplayText = "Ctrl+Alt+Up" },
        new() { Action = HotkeyAction.Decrement, Modifiers = Control | Alt, VirtualKey = 0x28, DisplayText = "Ctrl+Alt+Down" },
        new() { Action = HotkeyAction.ToggleStudyWindow, Modifiers = Control | Alt, VirtualKey = 0x53, DisplayText = "Ctrl+Alt+S" }
    ];
}
