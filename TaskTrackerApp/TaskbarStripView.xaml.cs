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

/// <summary>
/// Experimental widget style: a one-line strip placed over the Windows taskbar, left of the system tray.
/// Windows 11 has no API for drawing inside the taskbar, so this is a topmost window that re-asserts its
/// position every second and hides itself while the taskbar is hidden (auto-hide, fullscreen apps).
/// Clicking the strip opens the full widget as a flyout above the taskbar.
/// </summary>
public partial class TaskbarStripView : Window, IWidgetPresenter
{
    private const int StripWidthDip = 260;
    private const int FlyoutGapDip = 6;

    private readonly MainWindow _mainWindow;
    private readonly DispatcherTimer _upkeepTimer;
    private readonly DispatcherTimer _flyoutCloseTimer;
    private IntPtr _hwnd;
    private bool _wanted;
    private SettingsModel? _settings;
    private WidgetView? _flyout;
    private TaskbarLocator.Placement? _placement;
    private List<TaskModel> _activeTasks = new();
    private int _currentIndex;

    public event EventHandler<TaskModel>? OnSkipRequested;
    public event EventHandler? OnResetRequested;
    public event EventHandler<TaskModel>? OnTaskRequested;

    public TaskbarStripView(MainWindow mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;

        _upkeepTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _upkeepTimer.Tick += (s, e) => UpdatePlacement();

        _flyoutCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _flyoutCloseTimer.Tick += FlyoutCloseTimer_Tick;
    }

    /// <summary>True when the taskbar can host the strip (horizontal taskbar found).</summary>
    public static bool IsSupported() => TaskbarLocator.TryGetPlacement(StripWidthDip, 1.0, out _);

    // --- IWidgetPresenter ---

    // "Visible" means the user wants the strip; it may be temporarily hidden while the taskbar is hidden
    bool IWidgetPresenter.IsVisible => _wanted;

    void IWidgetPresenter.Show()
    {
        _wanted = true;
        _upkeepTimer.Start();
        UpdatePlacement();
    }

    void IWidgetPresenter.Hide()
    {
        _wanted = false;
        _upkeepTimer.Stop();
        CloseFlyout();
        Hide();
    }

    void IWidgetPresenter.Close()
    {
        _upkeepTimer.Stop();
        _flyoutCloseTimer.Stop();
        _flyout?.Close();
        _flyout = null;
        Close();
    }

    public void ResetPosition()
    {
        // Position is always derived from the taskbar; just re-evaluate it now
        UpdatePlacement();
    }

    public void SetActiveTasks(List<TaskModel> tasks)
    {
        var currentId = CurrentTask?.Id;
        _activeTasks = tasks ?? new List<TaskModel>();
        _currentIndex = Math.Max(0, currentId is Guid id ? _activeTasks.FindIndex(t => t.Id == id) : 0);
        Refresh();

        if (_flyout?.IsVisible == true)
        {
            _flyout.SetActiveTasks(_activeTasks);
            if (CurrentTask != null) _flyout.SelectTask(CurrentTask.Id);
        }
    }

    public void ApplySettings(SettingsModel settings)
    {
        _settings = settings;
        byte alpha = (byte)(Math.Clamp(settings.WidgetOpacity, 0.4, 1.0) * 255);

        bool light = settings.WidgetTheme == "Light";
        SolidColorBrush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
        if (light)
        {
            Pill.Background = Frozen(Color.FromArgb(alpha, 0xF9, 0xF9, 0xF9));
            Pill.BorderBrush = Frozen(Color.FromRgb(0xE5, 0xE5, 0xE5));
            StripText.Foreground = Frozen(Color.FromRgb(0x1A, 0x1A, 0x1A));
            PrevTaskButton.Foreground = NextTaskButton.Foreground = Frozen(Color.FromRgb(0x5D, 0x5D, 0x5D));
            DoneButton.Foreground = Frozen(Color.FromRgb(0x0F, 0x7B, 0x0F));
        }
        else
        {
            Pill.Background = Frozen(Color.FromArgb(alpha, 0x20, 0x20, 0x20));
            Pill.BorderBrush = Frozen(Color.FromRgb(0x3D, 0x3D, 0x3D));
            StripText.Foreground = Brushes.White;
            PrevTaskButton.Foreground = NextTaskButton.Foreground = Frozen(Color.FromRgb(0xC5, 0xC5, 0xC5));
            DoneButton.Foreground = Frozen(Color.FromRgb(0x6C, 0xCB, 0x5F));
        }
        StripText.FontWeight = settings.WidgetTextBold ? FontWeights.SemiBold : FontWeights.Normal;

        _flyout?.ApplySettings(settings);
    }

    // --- Display ---

    private TaskModel? CurrentTask =>
        _currentIndex >= 0 && _currentIndex < _activeTasks.Count ? _activeTasks[_currentIndex] : null;

    private void Refresh()
    {
        var task = CurrentTask;
        if (task == null)
        {
            StripText.Text = "No active task";
            Pill.ToolTip = "Task And Ticket Tracker — click to open";
            DoneButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            StripText.Text = WidgetText.CompactText(task);
            Pill.ToolTip = WidgetText.DetailText(task);
            DoneButton.Visibility = Visibility.Visible;
            DoneButton.ToolTip = WidgetText.CurrentStep(task) != null ? "Mark current step done" : "Mark task done";
        }

        var multiple = _activeTasks.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        PrevTaskButton.Visibility = multiple;
        NextTaskButton.Visibility = multiple;
    }

    private void Cycle(int delta)
    {
        if (_activeTasks.Count <= 1) return;
        _currentIndex = (_currentIndex + delta + _activeTasks.Count) % _activeTasks.Count;
        Refresh();
        if (_flyout?.IsVisible == true && CurrentTask != null) _flyout.SelectTask(CurrentTask.Id);
    }

    // --- Placement upkeep ---

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.MakeNoActivate(_hwnd);
    }

    private void UpdatePlacement()
    {
        if (!_wanted) return;

        if (_hwnd == IntPtr.Zero)
        {
            _hwnd = new WindowInteropHelper(this).EnsureHandle();
        }

        double scale = NativeMethods.GetDpiScale(_hwnd);
        if (!TaskbarLocator.TryGetPlacement(StripWidthDip, scale, out var placement) ||
            !TaskbarLocator.IsTaskbarShown(placement) ||
            TaskbarLocator.IsFullscreenAppActive(placement, _hwnd))
        {
            // Taskbar missing (Explorer restarting), slid away, or covered by a fullscreen app
            _placement = null;
            if (IsVisible) Hide();
            CloseFlyout();
            return;
        }

        _placement = placement;
        var r = placement.Strip;
        // Re-assert topmost every tick: clicking the taskbar raises it above us
        NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, r.X, r.Y, r.Width, r.Height, NativeMethods.SWP_NOACTIVATE);
        if (!IsVisible) Show();
    }

    // --- Interaction ---

    private void Pill_MouseEnter(object sender, MouseEventArgs e)
    {
        HoverButtons.Visibility = Visibility.Visible;
        _flyoutCloseTimer.Stop();
    }

    private void Pill_MouseLeave(object sender, MouseEventArgs e)
    {
        HoverButtons.Visibility = Visibility.Collapsed;
        if (_flyout?.IsVisible == true) _flyoutCloseTimer.Start();
    }

    private void Window_MouseWheel(object sender, MouseWheelEventArgs e) => Cycle(e.Delta > 0 ? -1 : 1);

    private void PrevTaskButton_Click(object sender, RoutedEventArgs e) => Cycle(-1);

    private void NextTaskButton_Click(object sender, RoutedEventArgs e) => Cycle(1);

    private void DoneButton_Click(object sender, RoutedEventArgs e)
    {
        var task = CurrentTask;
        if (task == null) return;

        var step = WidgetText.CurrentStep(task);
        if (step != null)
        {
            step.IsDone = true;
            _mainWindow.NotifyTaskUpdatedFromWidget(task);
            _mainWindow.SaveAndRefresh(); // pushes the updated tasks back to us
        }
        else
        {
            _mainWindow.MarkTaskDone(task);
        }
    }

    private void Pill_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_flyout?.IsVisible == true) CloseFlyout();
        else OpenFlyout();
    }

    // --- Flyout (full widget above the taskbar) ---

    private void OpenFlyout()
    {
        if (_placement is not TaskbarLocator.Placement placement) return;

        if (_flyout == null)
        {
            _flyout = new WidgetView(_mainWindow, WidgetRole.Flyout);
            _flyout.OnSkipRequested += (s, t) => OnSkipRequested?.Invoke(this, t);
            _flyout.OnResetRequested += (s, a) => OnResetRequested?.Invoke(this, a);
            _flyout.OnTaskRequested += (s, t) => OnTaskRequested?.Invoke(this, t);
            _flyout.MouseEnter += (s, a) => _flyoutCloseTimer.Stop();
            _flyout.MouseLeave += (s, a) => _flyoutCloseTimer.Start();
        }

        if (_settings != null) _flyout.ApplySettings(_settings);
        _flyout.SetActiveTasks(_activeTasks);
        if (CurrentTask != null) _flyout.SelectTask(CurrentTask.Id);

        // Bottom-aligned just above the taskbar, right-aligned with the strip
        var flyoutHwnd = new WindowInteropHelper(_flyout).EnsureHandle();
        double scale = NativeMethods.GetDpiScale(_hwnd);
        int width = (int)Math.Round(_flyout.Width * scale);
        int height = (int)Math.Round(_flyout.Height * scale);
        int gap = (int)Math.Round(FlyoutGapDip * scale);
        int x = placement.Strip.Right - width;
        int y = placement.Taskbar.Y - height - gap;
        NativeMethods.SetWindowPos(flyoutHwnd, NativeMethods.HWND_TOPMOST, x, y, width, height, NativeMethods.SWP_NOACTIVATE);

        _flyout.Show();
    }

    private void CloseFlyout()
    {
        _flyoutCloseTimer.Stop();
        if (_flyout?.IsVisible == true) _flyout.Hide();
    }

    private void FlyoutCloseTimer_Tick(object? sender, EventArgs e)
    {
        _flyoutCloseTimer.Stop();
        if (_flyout == null || _flyout.IsMouseOver || IsMouseOver || Mouse.LeftButton == MouseButtonState.Pressed)
        {
            if (_flyout?.IsVisible == true) _flyoutCloseTimer.Start();
            return;
        }
        CloseFlyout();
    }
}
