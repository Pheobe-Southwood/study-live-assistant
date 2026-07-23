using System.ComponentModel;
using System.Windows;
using Microsoft.Win32;
using StudyLiveAssistant.App.ViewModels;

namespace StudyLiveAssistant.App.Views;

public partial class MainWindow : Window
{
    private readonly AppRuntime _runtime;
    private bool _closing;

    public MainWindow(AppRuntime runtime)
    {
        InitializeComponent();
        _runtime = runtime;
        DataContext = runtime.MainViewModel;
    }

    private async void BrowseBackground_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "选择信息区背景图", Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp" };
        if (dialog.ShowDialog(this) != true || DataContext is not MainViewModel viewModel) return;
        try { await viewModel.ImportBackgroundAsync(dialog.FileName); }
        catch (Exception exception) { MessageBox.Show(this, exception.Message, "导入背景图失败", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void Canvas1080_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.SetCanvasSize(1920, 1080);
    }

    private void Canvas720_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        viewModel.SetCanvasSize(1280, 720);
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_closing) return;
        _closing = true;
        _runtime.PrepareForShutdown();
        Application.Current.Shutdown();
    }
}
