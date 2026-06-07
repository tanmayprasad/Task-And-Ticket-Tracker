using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using TaskTrackerApp.Models;

namespace TaskTrackerApp;

public partial class WidgetView : Window
{
    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, int wParam, int lParam);

    private readonly MainWindow _mainWindow;
    private List<TaskModel> _activeTasks = new();
    private int _currentIndex = 0;

    public event EventHandler<TaskModel>? OnSkipRequested;
    public event EventHandler? OnResetRequested;

    public WidgetView(MainWindow mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
    }

    public void SetActiveTasks(List<TaskModel> tasks)
    {
        _activeTasks = tasks ?? new List<TaskModel>();
        _currentIndex = 0;
        RefreshCarousel();
    }

    public void ApplySettings(SettingsModel settings)
    {
        ActiveTaskTextBlock.FontSize = settings.WidgetTextSize;
        ActiveTaskTextBlock.FontWeight = settings.WidgetTextBold ? FontWeights.Bold : FontWeights.Normal;
    }

    private void RefreshCarousel()
    {
        if (_activeTasks.Count > 0)
        {
            var currentTask = _activeTasks[_currentIndex];
            if (!string.IsNullOrEmpty(currentTask.VstsNumber))
            {
                ActiveVstsTextBlock.Text = $"#{currentTask.VstsNumber}";
                VstsBadge.Visibility = Visibility.Visible;
            }
            else
            {
                ActiveVstsTextBlock.Text = "";
                VstsBadge.Visibility = Visibility.Collapsed;
            }
            
            ActiveTaskTextBlock.Text = currentTask.Title;
            
            DoneButton.Visibility = Visibility.Visible;
            
            if (_activeTasks.Count > 1)
            {
                PrevButton.Visibility = Visibility.Visible;
                NextButton.Visibility = Visibility.Visible;
            }
            else
            {
                PrevButton.Visibility = Visibility.Collapsed;
                NextButton.Visibility = Visibility.Collapsed;
            }
            SkipButton.Visibility = Visibility.Visible;
        }
        else
        {
            ActiveVstsTextBlock.Text = string.Empty;
            VstsBadge.Visibility = Visibility.Collapsed;
            ActiveTaskTextBlock.Text = "No active task selected.";
            DoneButton.Visibility = Visibility.Collapsed;
            PrevButton.Visibility = Visibility.Collapsed;
            NextButton.Visibility = Visibility.Collapsed;
            SkipButton.Visibility = Visibility.Collapsed;
        }
    }

    private void PrevButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTasks.Count <= 1) return;
        _currentIndex--;
        if (_currentIndex < 0) _currentIndex = _activeTasks.Count - 1;
        RefreshCarousel();
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTasks.Count <= 1) return;
        _currentIndex++;
        if (_currentIndex >= _activeTasks.Count) _currentIndex = 0;
        RefreshCarousel();
    }

    private void DoneButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTasks.Count > 0)
        {
            var task = _activeTasks[_currentIndex];
            _mainWindow.MarkTaskDone(task);
            // The MainWindow will call SetActiveTasks to refresh us
        }
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTasks.Count > 0)
        {
            var task = _activeTasks[_currentIndex];
            OnSkipRequested?.Invoke(this, task);
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        OnResetRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            WindowInteropHelper helper = new WindowInteropHelper(this);
            SendMessage(helper.Handle, 161, 2, 0);
        }
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        this.Hide();
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        _mainWindow.RestoreFromTray();
    }

    private void ResizeThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        var newWidth = this.Width + e.HorizontalChange;
        var newHeight = this.Height + e.VerticalChange;
        
        if (newWidth >= this.MinWidth && newWidth > 0)
            this.Width = newWidth;
            
        if (newHeight >= this.MinHeight && newHeight > 0)
            this.Height = newHeight;
    }
}

public class StringToVisibilityConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        var str = value as string;
        return string.IsNullOrWhiteSpace(str) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}