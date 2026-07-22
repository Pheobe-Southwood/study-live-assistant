using StudyLiveAssistant.App.ViewModels;
using StudyLiveAssistant.Core;

namespace StudyLiveAssistant.Tests;

public sealed class PresentationModelTests
{
    [Fact]
    public void TaskCard_FormatsTitleElapsedAndProgress()
    {
        var task = new StudyTask
        {
            Detail = "真题第一套", ProgressKind = ProgressKind.Count, Unit = ProgressUnit.Question,
            TargetValue = 30, CurrentValue = 12, ElapsedSeconds = 3723
        };
        var card = new TaskCardViewModel { Task = task, PrimaryName = "英语", SecondaryName = "真题" };

        Assert.Equal("英语 · 真题｜真题第一套", card.Title);
        Assert.Equal("已用 01:02:03", card.ElapsedText);
        Assert.Equal("12 / 30 题", card.ProgressText);
        Assert.Equal(40, card.ProgressPercent);
    }

    [Fact]
    public void HotkeyMapping_RoundTripsEditableChoice()
    {
        var original = DefaultHotkeys.Create().First();
        var row = HotkeyMapping.ToEditRow(original);
        var result = HotkeyMapping.ToBinding(row);

        Assert.Equal(original.Action, result.Action);
        Assert.Equal(original.Modifiers, result.Modifiers);
        Assert.Equal(original.VirtualKey, result.VirtualKey);
    }
}
