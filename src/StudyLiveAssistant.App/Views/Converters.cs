using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using StudyLiveAssistant.Core;

namespace StudyLiveAssistant.App.Views;

public sealed class StringToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try { return new BrushConverter().ConvertFromString(value as string ?? "#7A9E9F") ?? Brushes.SlateGray; }
        catch (FormatException) { return Brushes.SlateGray; }
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class ThemeCornerRadiusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        CardTheme.Cute => new CornerRadius(16),
        CardTheme.Game => new CornerRadius(2),
        _ => new CornerRadius(8)
    };
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class ThemeCardBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        CardTheme.Cute => new SolidColorBrush(Color.FromArgb(238, 255, 248, 250)),
        CardTheme.Game => new SolidColorBrush(Color.FromArgb(238, 31, 44, 62)),
        _ => new SolidColorBrush(Color.FromArgb(239, 255, 255, 255))
    };
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class ProgressCornerRadiusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        ProgressBarStyle.Square => new CornerRadius(0), ProgressBarStyle.Rounded => new CornerRadius(4),
        ProgressBarStyle.Pill => new CornerRadius(10), _ => new CornerRadius(1)
    };
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class ProgressMaskConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ProgressBarStyle.Segmented) return Brushes.White;
        return new DrawingBrush
        {
            Viewport = new Rect(0, 0, 15, 10), ViewportUnits = BrushMappingMode.Absolute, TileMode = TileMode.Tile,
            Drawing = new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0, 0, 11, 10)))
        };
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class EnumTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        ProgressKind.Time => "时间型（自动推进）",
        ProgressKind.Count => "计数型（手动推进）",
        ProgressUnit.Minute => "分钟", ProgressUnit.Chapter => "章节", ProgressUnit.Page => "页数",
        ProgressUnit.Question => "题目", ProgressUnit.Custom => "自定义",
        CardTheme.Focus => "基础专注型", CardTheme.Cute => "软萌风", CardTheme.Game => "游戏风",
        ProgressBarStyle.Square => "方形", ProgressBarStyle.Rounded => "圆角",
        ProgressBarStyle.Pill => "胶囊", ProgressBarStyle.Segmented => "分段",
        _ => value.ToString() ?? string.Empty
    };
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
