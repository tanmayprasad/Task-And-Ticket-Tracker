using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TaskTrackerApp.Models;
using TaskTrackerApp.Widgets;

namespace TaskTrackerApp;

public enum WidgetRole
{
    /// <summary>Free-floating widget (the classic style).</summary>
    Floating,
    /// <summary>Snaps to outer screen edges and slides out of view when not hovered.</summary>
    EdgeDocked,
    /// <summary>Temporary popup opened from the taskbar strip; never saves its position.</summary>
    Flyout
}

public partial class WidgetView : Window, IWidgetPresenter
{
    // Transparent shadow margin around MainBorder + width of the dock tab, in DIPs
    private const int DockVisibleDip = 8 + 14;

    private readonly MainWindow _mainWindow;
    private readonly WidgetRole _role;
    private readonly Data.SettingsManager _settingsManager = new();
    private EdgeDockController? _dock;
    private List<TaskModel> _activeTasks = new();
    private int _currentIndex = 0;
    private int _currentStepIndex = 0;

    public event EventHandler<TaskModel>? OnSkipRequested;
    public event EventHandler? OnResetRequested;
    public event EventHandler<TaskModel>? OnTaskRequested;

    public WidgetView(MainWindow mainWindow, WidgetRole role = WidgetRole.Floating)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        _role = role;
    }

    public void SetActiveTasks(List<TaskModel> tasks)
    {
        var currentTaskId = (_activeTasks != null && _currentIndex >= 0 && _currentIndex < _activeTasks.Count)
            ? _activeTasks[_currentIndex].Id
            : Guid.Empty;

        _activeTasks = tasks ?? new List<TaskModel>();

        _currentIndex = 0;
        if (currentTaskId != Guid.Empty)
        {
            for (int i = 0; i < _activeTasks.Count; i++)
            {
                if (_activeTasks[i].Id == currentTaskId)
                {
                    _currentIndex = i;
                    break;
                }
            }
        }

        RefreshCarousel();
    }

    /// <summary>Shows the given task in the carousel (keeps the flyout in sync with the taskbar strip).</summary>
    public void SelectTask(Guid taskId)
    {
        int index = _activeTasks.FindIndex(t => t.Id == taskId);
        if (index >= 0 && index != _currentIndex)
        {
            _currentIndex = index;
            _currentStepIndex = 0;
            RefreshCarousel();
        }
    }

    public void ApplySettings(SettingsModel settings)
    {
        ActiveTaskTextBlock.FontSize = settings.WidgetTextSize;
        ActiveTaskTextBlock.FontWeight = settings.WidgetTextBold ? FontWeights.Bold : FontWeights.Normal;

        // Below 40% the widget becomes unreadable over busy wallpapers
        byte alpha = (byte)(Math.Clamp(settings.WidgetOpacity, 0.4, 1.0) * 255);

        bool light = settings.WidgetTheme == "Light";
        Brush Solid(byte r, byte g, byte b) => Freeze(new SolidColorBrush(Color.FromRgb(r, g, b)));
        Brush Argb(byte a, byte r, byte g, byte b) => Freeze(new SolidColorBrush(Color.FromArgb(a, r, g, b)));

        // Windows 11 neutral card colours, with the Windows accent for the ticket badge and dock tab
        var accent = Theming.AccentService.CurrentAccent(dark: !light);
        if (light)
        {
            MainBorder.Background = Argb(alpha, 0xF9, 0xF9, 0xF9);
            MainBorder.BorderBrush = Solid(0xE5, 0xE5, 0xE5);
            ActiveTaskTextBlock.Foreground = Solid(0x1A, 0x1A, 0x1A);
            WidgetStepTextBlock.Foreground = Solid(0x3A, 0x3A, 0x3A);
            DragHandle.Foreground = MaximizeBtn.Foreground = CloseBtn.Foreground = Solid(0x5D, 0x5D, 0x5D);
            ResetButton.Foreground = SkipButton.Foreground = Solid(0x5D, 0x5D, 0x5D);
            DoneButton.Foreground = Solid(0x0F, 0x7B, 0x0F);
            StepPill.Background = Argb(0x0A, 0, 0, 0);
            StepPill.BorderBrush = Argb(0x14, 0, 0, 0);
        }
        else
        {
            MainBorder.Background = Argb(alpha, 0x20, 0x20, 0x20);
            MainBorder.BorderBrush = Solid(0x3D, 0x3D, 0x3D);
            ActiveTaskTextBlock.Foreground = Solid(0xFF, 0xFF, 0xFF);
            WidgetStepTextBlock.Foreground = Solid(0xE0, 0xE0, 0xE0);
            DragHandle.Foreground = MaximizeBtn.Foreground = CloseBtn.Foreground = Solid(0xA0, 0xA0, 0xA0);
            ResetButton.Foreground = SkipButton.Foreground = Solid(0xA0, 0xA0, 0xA0);
            DoneButton.Foreground = Solid(0x6C, 0xCB, 0x5F);
            StepPill.Background = Argb(0x0F, 255, 255, 255);
            StepPill.BorderBrush = Argb(0x1A, 255, 255, 255);
        }
        ActiveVstsTextBlock.Foreground = Freeze(new SolidColorBrush(accent));
        VstsBadgeButton.Background = Freeze(new SolidColorBrush(Color.FromArgb(0x2E, accent.R, accent.G, accent.B)));
        DockTab.Background = Freeze(new SolidColorBrush(accent));
        DockTabText.Foreground = Freeze(new SolidColorBrush(Theming.AccentMath.BestTextOn(accent)));

        // Chevrons follow the text colour
        var chevron = light ? Solid(0x5D, 0x5D, 0x5D) : Solid(0xE0, 0xE0, 0xE0);
        if (PrevButton.Content is System.Windows.Shapes.Path prev) prev.Fill = chevron;
        if (NextButton.Content is System.Windows.Shapes.Path next) next.Fill = chevron;
        if (PrevStepButton.Content is System.Windows.Controls.TextBlock prevStep) prevStep.Foreground = chevron;
        if (NextStepButton.Content is System.Windows.Controls.TextBlock nextStep) nextStep.Foreground = chevron;

        // Flat Fluent text: no per-text shadow effect (also saves GPU work on every redraw)
        ActiveTaskTextBlock.Effect = null;
    }

    private static Brush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        // Clicking the widget must never take keyboard focus away from the app the user is working in
        NativeMethods.MakeNoActivate(new WindowInteropHelper(this).Handle);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_role == WidgetRole.Flyout) return; // the taskbar strip positions its flyout

        var settings = _settingsManager.LoadSettings();

        if (!settings.WidgetLeft.HasValue || !settings.WidgetTop.HasValue)
        {
            CenterWindow();
        }
        else
        {
            this.Left = settings.WidgetLeft.Value;
            this.Top = settings.WidgetTop.Value;
        }

        var hwnd = new WindowInteropHelper(this).Handle;
        if (_role == WidgetRole.EdgeDocked)
        {
            _dock = new EdgeDockController(this, hwnd, DockVisibleDip);
            _dock.StateChanged += (s, args) => UpdateDockVisuals();
            _dock.Restore(Enum.TryParse<DockEdge>(settings.DockEdge, out var edge) ? edge : null);
        }
        else
        {
            // Keep a position saved on a disconnected monitor or an old resolution on screen
            var rect = NativeMethods.GetWindowPixelRect(hwnd);
            var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (NativeMethods.TryGetMonitorInfo(monitor, out var info))
            {
                var clamped = DockMath.ClampToWorkArea(rect, info.rcWork.ToPixelRect());
                if (clamped != rect) NativeMethods.MoveWindow(hwnd, clamped.X, clamped.Y);
            }
        }
    }

    /// <summary>Persists the (expanded) position and dock edge. Called once when a drag or resize ends.</summary>
    private void SavePosition()
    {
        if (_role == WidgetRole.Flyout) return;

        // Let WPF process the final WM_MOVE from any snap before reading Left/Top
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (_dock?.IsCollapsed == true) return;
            var settings = _settingsManager.LoadSettings();
            settings.WidgetLeft = this.Left;
            settings.WidgetTop = this.Top;
            settings.DockEdge = _dock?.Edge?.ToString();
            _settingsManager.SaveSettings(settings);
        }));
    }

    public void CenterWindow()
    {
        _dock?.Undock();

        var screen = System.Windows.SystemParameters.WorkArea;
        this.Left = (screen.Width - this.Width) / 2;
        this.Top = (screen.Height - this.Height) / 2;

        var settings = _settingsManager.LoadSettings();
        settings.WidgetLeft = null;
        settings.WidgetTop = null;
        settings.DockEdge = null;
        _settingsManager.SaveSettings(settings);
    }

    public void ResetPosition() => CenterWindow();

    private void UpdateDockVisuals()
    {
        var edge = _dock?.Edge;
        if (_dock?.IsCollapsed != true || edge == null)
        {
            DockTab.Visibility = Visibility.Collapsed;
            MainBorder.Opacity = 1;
            return;
        }

        // The tab sits on the side of the window that stays on screen
        bool vertical = edge is DockEdge.Left or DockEdge.Right;
        DockTab.HorizontalAlignment = edge switch
        {
            DockEdge.Right => HorizontalAlignment.Left,
            DockEdge.Left => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Stretch,
        };
        DockTab.VerticalAlignment = edge switch
        {
            DockEdge.Bottom => VerticalAlignment.Top,
            DockEdge.Top => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Stretch,
        };
        DockTab.Width = vertical ? 14 : double.NaN;
        DockTab.Height = vertical ? double.NaN : 14;
        DockTabText.LayoutTransform = vertical ? new RotateTransform(edge == DockEdge.Left ? 90 : -90) : Transform.Identity;

        MainBorder.Opacity = 0;
        DockTab.Visibility = Visibility.Visible;
    }

    protected override void OnClosed(EventArgs e)
    {
        _dock?.Dispose();
        base.OnClosed(e);
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
            DockTabText.Text = string.IsNullOrWhiteSpace(currentTask.VstsNumber) ? "•" : WidgetText.TicketLabel(currentTask);
            DockTab.ToolTip = WidgetText.DetailText(currentTask);
            
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
            DockTabText.Text = "•";
            DockTab.ToolTip = null;
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
            
            // Auto-advance to the first incomplete priority step
            if (task.Steps[_currentStepIndex].IsDone)
            {
                int nextIndex = -1;
                for (int i = 0; i < task.Steps.Count; i++) 
                {
                    if (!task.Steps[i].IsDone) 
                    { 
                        nextIndex = i; 
                        break; 
                    }
                }
                
                if (nextIndex != -1) 
                {
                    _currentStepIndex = nextIndex;
                }
            }

            // The steps are still bound inside MainWindow, so we should call SaveAndRefresh
            // to persist the checkmark state immediately to the file.
            _mainWindow.NotifyTaskUpdatedFromWidget(task);
            _mainWindow.SaveAndRefresh();
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
        if (e.LeftButton == MouseButtonState.Pressed && _role != WidgetRole.Flyout)
        {
            WindowInteropHelper helper = new WindowInteropHelper(this);
            // Runs the OS move loop and returns once the mouse button is released
            NativeMethods.SendMessage(helper.Handle, NativeMethods.WM_NCLBUTTONDOWN, NativeMethods.HTCAPTION, 0);
            _dock?.OnDragCompleted();
            SavePosition();
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

    private void ResizeThumb_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        _dock?.OnDragCompleted();
        SavePosition();
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