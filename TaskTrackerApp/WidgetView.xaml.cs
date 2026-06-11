using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TaskTrackerApp.Models;

namespace TaskTrackerApp;

public partial class WidgetView : Window
{
    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int wMsg, int wParam, int lParam);

    private readonly MainWindow _mainWindow;
    private List<TaskModel> _activeTasks = new();
    private int _currentIndex = 0;
    private int _currentStepIndex = 0;

    public event EventHandler<TaskModel>? OnSkipRequested;
    public event EventHandler? OnResetRequested;
    public event EventHandler<TaskModel>? OnTaskRequested;

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
        
        if (settings.WidgetLeft.HasValue && settings.WidgetTop.HasValue)
        {
            this.Left = settings.WidgetLeft.Value;
            this.Top = settings.WidgetTop.Value;
        }

        byte alpha = (byte)(Math.Clamp(settings.WidgetOpacity, 0.1, 1.0) * 255);
        
        if (settings.WidgetTheme == "Light")
        {
            MainBorder.Background = new SolidColorBrush(Color.FromArgb(alpha, 250, 250, 250));
            ActiveTaskTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            ActiveVstsTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(30, 136, 229));
            if (WidgetStepTextBlock != null) WidgetStepTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(50, 50, 50));
            if (DragHandle != null) DragHandle.Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180));
        }
        else
        {
            MainBorder.Background = new SolidColorBrush(Color.FromArgb(alpha, 26, 29, 36)); // #1A1D24
            ActiveTaskTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
            ActiveVstsTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(100, 181, 246));
            if (WidgetStepTextBlock != null) WidgetStepTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238));
            if (DragHandle != null) DragHandle.Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85));
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var settingsManager = new Data.SettingsManager();
        var settings = settingsManager.LoadSettings();
        
        if (!settings.WidgetLeft.HasValue || !settings.WidgetTop.HasValue)
        {
            CenterWindow();
        }
        else
        {
            this.Left = settings.WidgetLeft.Value;
            this.Top = settings.WidgetTop.Value;
        }
    }

    private void Window_LocationChanged(object sender, EventArgs e)
    {
        if (this.IsLoaded)
        {
            var settingsManager = new Data.SettingsManager();
            var settings = settingsManager.LoadSettings();
            settings.WidgetLeft = this.Left;
            settings.WidgetTop = this.Top;
            settingsManager.SaveSettings(settings);
        }
    }

    public void CenterWindow()
    {
        var screen = System.Windows.SystemParameters.WorkArea;
        this.Left = (screen.Width - this.Width) / 2;
        this.Top = (screen.Height - this.Height) / 2;
        
        var settingsManager = new Data.SettingsManager();
        var settings = settingsManager.LoadSettings();
        settings.WidgetLeft = null;
        settings.WidgetTop = null;
        settingsManager.SaveSettings(settings);
    }

    private void RefreshCarousel()
    {
        if (_activeTasks.Count > 0)
        {
            var currentTask = _activeTasks[_currentIndex];
            if (!string.IsNullOrWhiteSpace(currentTask.VstsNumber))
            {
                ActiveVstsTextBlock.Text = $"#{currentTask.VstsNumber}";
                VstsBadgeButton.Visibility = Visibility.Visible;
            }
            else
            {
                ActiveVstsTextBlock.Text = "";
                VstsBadgeButton.Visibility = Visibility.Collapsed;
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
            if (currentTask.Steps != null && currentTask.Steps.Count > 0)
            {
                if (_currentStepIndex >= currentTask.Steps.Count || _currentStepIndex < 0)
                    _currentStepIndex = 0;
                    
                var step = currentTask.Steps[_currentStepIndex];
                WidgetStepTextBlock.Text = step.Description;
                WidgetStepCheckBox.IsChecked = step.IsDone;
                
                PrevStepButton.Visibility = currentTask.Steps.Count > 1 ? Visibility.Visible : Visibility.Hidden;
                NextStepButton.Visibility = currentTask.Steps.Count > 1 ? Visibility.Visible : Visibility.Hidden;
                
                StepDisplayGrid.Visibility = Visibility.Visible;
            }
            else
            {
                StepDisplayGrid.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            ActiveVstsTextBlock.Text = string.Empty;
            VstsBadgeButton.Visibility = Visibility.Collapsed;
            ActiveTaskTextBlock.Text = "No active task selected.";
            StepDisplayGrid.Visibility = Visibility.Collapsed;
            DoneButton.Visibility = Visibility.Collapsed;
            PrevButton.Visibility = Visibility.Collapsed;
            NextButton.Visibility = Visibility.Collapsed;
            SkipButton.Visibility = Visibility.Collapsed;
        }
    }

    private void VstsBadgeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTasks.Count > 0 && _currentIndex >= 0 && _currentIndex < _activeTasks.Count)
        {
            OnTaskRequested?.Invoke(this, _activeTasks[_currentIndex]);
        }
    }

    private void PrevButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTasks.Count <= 1) return;
        _currentIndex--;
        if (_currentIndex < 0) _currentIndex = _activeTasks.Count - 1;
        _currentStepIndex = 0;
        RefreshCarousel();
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTasks.Count <= 1) return;
        _currentIndex++;
        if (_currentIndex >= _activeTasks.Count) _currentIndex = 0;
        _currentStepIndex = 0;
        RefreshCarousel();
    }

    private void PrevStepButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTasks.Count == 0) return;
        var task = _activeTasks[_currentIndex];
        if (task.Steps == null || task.Steps.Count <= 1) return;
        
        _currentStepIndex--;
        if (_currentStepIndex < 0) _currentStepIndex = task.Steps.Count - 1;
        RefreshCarousel();
    }

    private void NextStepButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTasks.Count == 0) return;
        var task = _activeTasks[_currentIndex];
        if (task.Steps == null || task.Steps.Count <= 1) return;
        
        _currentStepIndex++;
        if (_currentStepIndex >= task.Steps.Count) _currentStepIndex = 0;
        RefreshCarousel();
    }

    private void WidgetStepCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTasks.Count == 0) return;
        var task = _activeTasks[_currentIndex];
        if (task.Steps != null && task.Steps.Count > 0 && _currentStepIndex < task.Steps.Count)
        {
            task.Steps[_currentStepIndex].IsDone = WidgetStepCheckBox.IsChecked ?? false;
            // The steps are still bound inside MainWindow, so we should call SaveAndRefresh
            // to persist the checkmark state immediately to the file.
            _mainWindow.NotifyTaskUpdatedFromWidget(task);
            _mainWindow.SaveAndRefresh();
            
            // Auto-advance to the next incomplete step
            if (task.Steps[_currentStepIndex].IsDone)
            {
                int nextIndex = -1;
                // search forward
                for (int i = _currentStepIndex + 1; i < task.Steps.Count; i++) 
                {
                    if (!task.Steps[i].IsDone) { nextIndex = i; break; }
                }
                // wrap around if necessary
                if (nextIndex == -1) 
                {
                    for (int i = 0; i < _currentStepIndex; i++) 
                    {
                        if (!task.Steps[i].IsDone) { nextIndex = i; break; }
                    }
                }
                
                if (nextIndex != -1) 
                {
                    _currentStepIndex = nextIndex;
                    RefreshCarousel();
                }
            }
        }
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