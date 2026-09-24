using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace TaskTrackerApp.Widgets;

/// <summary>
/// Docks a window to an outer screen edge. When docked, the window slides out of view leaving only a
/// small tab visible, and slides back in while the mouse is over it. All positioning is in physical pixels.
/// </summary>
internal sealed class EdgeDockController : IDisposable
{
    private const int SnapThresholdDip = 48;
    private const double AnimationMs = 180;
    private static readonly TimeSpan CollapseDelay = TimeSpan.FromMilliseconds(700);

    private readonly Window _window;
    private readonly IntPtr _hwnd;
    private readonly int _visibleDip;
    private readonly DispatcherTimer _collapseTimer;
    private readonly DispatcherTimer _animationTimer;

    private PixelRect _expandedRect;
    private PixelRect _workArea;
    private PixelRect _animFrom;
    private PixelRect _animTo;
    private DateTime _animStart;

    public DockEdge? Edge { get; private set; }
    public bool IsCollapsed { get; private set; }

    /// <summary>Raised when the docked edge or collapsed state changes, so the view can update its tab.</summary>
    public event EventHandler? StateChanged;

    /// <param name="visibleDip">How much of the window stays on screen when collapsed (shadow margin + tab).</param>
    public EdgeDockController(Window window, IntPtr hwnd, int visibleDip)
    {
        _window = window;
        _hwnd = hwnd;
        _visibleDip = visibleDip;

        _collapseTimer = new DispatcherTimer { Interval = CollapseDelay };
        _collapseTimer.Tick += CollapseTimer_Tick;

        _animationTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(15) };
        _animationTimer.Tick += AnimationTimer_Tick;

        _window.MouseEnter += Window_MouseEnter;
        _window.MouseLeave += Window_MouseLeave;
        SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
    }

    private double Scale => NativeMethods.GetDpiScale(_hwnd);
    private int VisiblePx => (int)Math.Round(_visibleDip * Scale);

    /// <summary>Re-applies a saved edge on startup: snaps to it (if it is still an outer edge) and collapses immediately.</summary>
    public void Restore(DockEdge? savedEdge)
    {
        var rect = NativeMethods.GetWindowPixelRect(_hwnd);
        var work = WorkAreaFor(rect);

        if (savedEdge is DockEdge edge && IsOuterEdge(rect, work, edge))
        {
            Dock(rect, work, edge);
            if (!_window.IsMouseOver) Collapse(animate: false);
        }
        else
        {
            Edge = null;
            MoveTo(DockMath.ClampToWorkArea(rect, work));
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Call when a drag or resize ends: docks if the window is near an outer edge, otherwise undocks.</summary>
    public void OnDragCompleted()
    {
        StopAnimation();
        var rect = NativeMethods.GetWindowPixelRect(_hwnd);
        var work = WorkAreaFor(rect);
        var edge = DockMath.NearestEdge(rect, work, (int)Math.Round(SnapThresholdDip * Scale));

        if (edge is DockEdge e && IsOuterEdge(rect, work, e))
        {
            Dock(rect, work, e);
            if (!_window.IsMouseOver) _collapseTimer.Start();
        }
        else
        {
            Edge = null;
            IsCollapsed = false;
            MoveTo(DockMath.ClampToWorkArea(rect, work));
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Leaves dock mode and shows the window fully.</summary>
    public void Undock()
    {
        _collapseTimer.Stop();
        StopAnimation();
        if (Edge != null && IsCollapsed) MoveTo(_expandedRect);
        Edge = null;
        IsCollapsed = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Dock(PixelRect rect, PixelRect work, DockEdge edge)
    {
        Edge = edge;
        IsCollapsed = false;
        _workArea = work;
        _expandedRect = DockMath.ExpandedRect(rect, work, edge);
        MoveTo(_expandedRect);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Expand()
    {
        if (Edge == null || !IsCollapsed) return;
        IsCollapsed = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
        AnimateTo(_expandedRect);
    }

    private void Collapse(bool animate)
    {
        if (Edge is not DockEdge edge || IsCollapsed) return;
        IsCollapsed = true;
        StateChanged?.Invoke(this, EventArgs.Empty);

        var target = DockMath.CollapsedRect(_expandedRect, _workArea, edge, VisiblePx);
        if (animate) AnimateTo(target);
        else MoveTo(target);
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        _collapseTimer.Stop();
        Expand();
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (Edge != null && !IsCollapsed) _collapseTimer.Start();
    }

    private void CollapseTimer_Tick(object? sender, EventArgs e)
    {
        _collapseTimer.Stop();
        // Don't slide away mid-interaction (drag, resize, or pointer back over the widget)
        if (_window.IsMouseOver || Mouse.LeftButton == MouseButtonState.Pressed)
        {
            _collapseTimer.Start();
            return;
        }
        Collapse(animate: true);
    }

    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        // Raised on a system thread; re-evaluate on the UI thread once the new layout is in place
        _window.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            StopAnimation();
            var reference = Edge != null ? _expandedRect : NativeMethods.GetWindowPixelRect(_hwnd);
            var work = WorkAreaFor(reference);

            if (Edge is DockEdge edge && IsOuterEdge(reference, work, edge))
            {
                bool wasCollapsed = IsCollapsed;
                Dock(DockMath.ClampToWorkArea(reference, work), work, edge);
                if (wasCollapsed) Collapse(animate: false);
            }
            else
            {
                Edge = null;
                IsCollapsed = false;
                MoveTo(DockMath.ClampToWorkArea(reference, work));
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }));
    }

    private void AnimateTo(PixelRect target)
    {
        _animFrom = NativeMethods.GetWindowPixelRect(_hwnd);
        _animTo = target;
        _animStart = DateTime.UtcNow;
        _animationTimer.Start();
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        double t = (DateTime.UtcNow - _animStart).TotalMilliseconds / AnimationMs;
        int x = DockMath.EaseOut(_animFrom.X, _animTo.X, t);
        int y = DockMath.EaseOut(_animFrom.Y, _animTo.Y, t);
        NativeMethods.MoveWindow(_hwnd, x, y);
        if (t >= 1) _animationTimer.Stop();
    }

    private void StopAnimation() => _animationTimer.Stop();

    private void MoveTo(PixelRect rect) => NativeMethods.MoveWindow(_hwnd, rect.X, rect.Y);

    private static PixelRect WorkAreaFor(PixelRect rect)
    {
        var center = new NativeMethods.POINT(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        var monitor = NativeMethods.MonitorFromPoint(center, NativeMethods.MONITOR_DEFAULTTONEAREST);
        return NativeMethods.TryGetMonitorInfo(monitor, out var info) ? info.rcWork.ToPixelRect() : rect;
    }

    /// <summary>An edge is "outer" when no other monitor lies directly beyond it, so the tab can't spill onto a neighbour.</summary>
    private static bool IsOuterEdge(PixelRect rect, PixelRect work, DockEdge edge)
    {
        var (x, y) = DockMath.PointBeyondEdge(rect, work, edge);
        return !NativeMethods.IsPointOnAnyMonitor(x, y);
    }

    public void Dispose()
    {
        _collapseTimer.Stop();
        _animationTimer.Stop();
        _window.MouseEnter -= Window_MouseEnter;
        _window.MouseLeave -= Window_MouseLeave;
        SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
    }
}
