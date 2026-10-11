using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TaskTrackerApp.Theming;

namespace TaskTrackerApp.Onboarding;

/// <summary>
/// Runs a tour (a list of <see cref="CoachStep"/>s): Next/Back/Skip, keyboard (→ / Enter next, ← back, Esc skip),
/// and re-positioning when the window is resized. Window shortcuts are blocked while a tour is open.
/// </summary>
public partial class CoachMarkOverlay : UserControl
{
    private const double RingPadding = 6;
    private const double CardWidth = 360;

    private MainWindow? _host;
    private IReadOnlyList<CoachStep> _steps = Array.Empty<CoachStep>();
    private int _index;
    private Action? _closed;
    private bool _positionQueued;

    public CoachMarkOverlay()
    {
        InitializeComponent();
        SizeChanged += (s, e) => QueuePosition();
        PreviewKeyDown += Overlay_PreviewKeyDown;
        // Swallow everything else (Ctrl+N, Del, F1…) so window shortcuts can't act behind the tour
        KeyDown += (s, e) => e.Handled = true;
    }

    public bool IsOpen => Visibility == Visibility.Visible;

    /// <summary>Starts a tour; <paramref name="closed"/> runs when it ends (finished or skipped).</summary>
    public void Start(MainWindow host, IReadOnlyList<CoachStep> steps, Action? closed = null)
    {
        if (steps.Count == 0) return;
        if (IsOpen) Close();

        _host = host;
        _steps = steps;
        _closed = closed;
        _index = 0;
        Visibility = Visibility.Visible;
        ShowStep();
    }

    public void Close()
    {
        if (!IsOpen) return;
        Visibility = Visibility.Collapsed;
        Scrim.Data = null;
        var closed = _closed;
        _closed = null;
        closed?.Invoke();
    }

    private CoachStep Current => _steps[_index];

    private void ShowStep()
    {
        if (_host == null) return;
        var step = Current;
        step.Prepare?.Invoke(_host);

        bool last = _index == _steps.Count - 1;
        NewPill.Visibility = step.IsNew ? Visibility.Visible : Visibility.Collapsed;
        ProgressText.Text = _steps.Count > 1 ? $"{_index + 1} of {_steps.Count}" : string.Empty;
        TitleText.Text = step.Title;
        BodyText.Text = step.Body;
        BuildHighlights(step.Highlights);

        ExtraText.Text = step.ExtraText;
        AutomationProperties.SetName(ExtraButton, step.ExtraText ?? string.Empty);
        ExtraButton.Visibility = step.ExtraText != null && step.ExtraAction != null ? Visibility.Visible : Visibility.Collapsed;
        BackButton.Visibility = _index > 0 ? Visibility.Visible : Visibility.Collapsed;
        NextButton.Content = step.NextText ?? (last ? "Done" : "Next");
        SkipButton.Content = step.SkipText ?? "Skip tour";
        SkipButton.Visibility = last ? Visibility.Collapsed : Visibility.Visible;

        AutomationProperties.SetName(Card, $"{step.Title}. {step.Body}");

        Position();
        QueuePosition(); // again once Prepare's page switch has been laid out
        Motion.FadeSlideIn(Card, fromY: 8, milliseconds: 160, fade: true); // small surface: fade is cheap

        NextButton.Focus();
        UIElementAutomationPeer.CreatePeerForElement(Card)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void BuildHighlights(IReadOnlyList<CoachHighlight>? highlights)
    {
        HighlightsGrid.Children.Clear();
        HighlightsGrid.Visibility = highlights is { Count: > 0 } ? Visibility.Visible : Visibility.Collapsed;
        if (highlights == null) return;

        foreach (var h in highlights)
        {
            var glyph = new TextBlock
            {
                Text = h.Glyph,
                FontFamily = (FontFamily)FindResource("IconFont"),
                FontSize = 22,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8),
            };
            glyph.SetResourceReference(TextBlock.ForegroundProperty, "AccentText"); // follows theme and accent changes
            var tile = new StackPanel { Margin = new Thickness(4, 0, 4, 0) };
            tile.Children.Add(glyph);
            tile.Children.Add(new TextBlock { Text = h.Label, Style = (Style)FindResource("BodyStrongText"), HorizontalAlignment = HorizontalAlignment.Center });
            tile.Children.Add(new TextBlock
            {
                Text = h.Caption,
                Style = (Style)FindResource("CaptionText"),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0),
            });
            HighlightsGrid.Children.Add(tile);
        }
    }

    // --- Layout ---

    private void QueuePosition()
    {
        if (_positionQueued) return;
        _positionQueued = true;
        // Background priority: after layout, and after MainWindow re-positions its title bar (Loaded priority)
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _positionQueued = false;
            Position();
        }), DispatcherPriority.Background);
    }

    private void Position()
    {
        if (!IsOpen || _host == null || ActualWidth <= 0 || ActualHeight <= 0) return;
        var area = new Rect(0, 0, ActualWidth, ActualHeight);

        Rect? target = null;
        if (Current.Target?.Invoke(_host) is Rect r && r.Width > 0 && r.Height > 0 && _host.TransformToDescendant(this) is GeneralTransform toOverlay)
        {
            var t = toOverlay.TransformBounds(r);
            t.Inflate(RingPadding, RingPadding);
            t.Intersect(area);
            if (!t.IsEmpty && t.Width > 0 && t.Height > 0) target = t;
        }

        var full = new RectangleGeometry(area);
        if (target is Rect hole)
        {
            Scrim.Data = new CombinedGeometry(GeometryCombineMode.Exclude, full, new RectangleGeometry(hole, 8, 8));
            Ring.Width = hole.Width;
            Ring.Height = hole.Height;
            Canvas.SetLeft(Ring, hole.Left);
            Canvas.SetTop(Ring, hole.Top);
            Ring.Visibility = Visibility.Visible;
        }
        else
        {
            Scrim.Data = full;
            Ring.Visibility = Visibility.Collapsed;
        }

        Card.Width = Math.Max(220, Math.Min(CardWidth, area.Width - 2 * CoachLayout.Margin));
        Card.Measure(new Size(Card.Width, double.PositiveInfinity));
        var place = CoachLayout.PlaceCard(target, Card.DesiredSize, area.Size);
        Canvas.SetLeft(Card, place.X);
        Canvas.SetTop(Card, place.Y);
    }

    // --- Navigation ---

    private void Next()
    {
        if (_index >= _steps.Count - 1) { Close(); return; }
        _index++;
        ShowStep();
    }

    private void Back()
    {
        if (_index == 0) return;
        _index--;
        ShowStep();
    }

    private void Next_Click(object sender, RoutedEventArgs e) => Next();
    private void Back_Click(object sender, RoutedEventArgs e) => Back();
    private void Skip_Click(object sender, RoutedEventArgs e) => Close();

    private void Extra_Click(object sender, RoutedEventArgs e)
    {
        var action = Current.ExtraAction;
        var host = _host;
        Close();
        if (host != null) action?.Invoke(host);
    }

    private void Overlay_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            case Key.Right:
                Next();
                e.Handled = true;
                break;
            case Key.Left:
                Back();
                e.Handled = true;
                break;
            case Key.Enter when Keyboard.FocusedElement is not ButtonBase: // a focused button handles Enter itself
                Next();
                e.Handled = true;
                break;
        }
    }
}
