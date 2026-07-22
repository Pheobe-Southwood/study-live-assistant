using System.Windows;
using System.Windows.Input;
using StudyLiveAssistant.App.ViewModels;

namespace StudyLiveAssistant.App.Views;

public partial class StudyWindow : Window
{
    public StudyWindow(StudyViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void DragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        if (DataContext is StudyViewModel viewModel) viewModel.Dispose();
    }
}
