using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TaskTrackerApp.Data;
using TaskTrackerApp.Models;
using TaskTrackerApp.Widgets;
using TaskTrackerApp.Theming;

namespace TaskTrackerApp;

public partial class MainWindow : Window
{
    private readonly TaskRepository _repository;
    private List<TaskModel> _tasks = new();
    private TaskModel? _currentTask;
    private System.Collections.ObjectModel.ObservableCollection<TicketStep> _editingSteps = new();
    private IWidgetPresenter? _widget;
    private string? _widgetMode;
    private bool _taskbarFallbackNotified;
    private ContextAwareEngine _contextEngine;
    private NotificationService _notificationService;
    private SettingsManager _settingsManager;

    // For drag and drop
    private DataGridRow? _draggedRow;
    private Point _gridDragStartPoint;

    // Notification bar (snackbar)
    private readonly System.Windows.Threading.DispatcherTimer _snackbarTimer = new();
    private Action? _snackbarAction;

    // Set when the user really wants to quit (tray → Exit); otherwise closing hides to the tray
    private bool _isExiting;

    // Task form state
    private bool _dueHasTime;               // false = the due date is "all day"
    private string _formSnapshot = string.Empty;
    private Action? _afterUnsavedResolved;  // what to do once the user saves/discards pending edits
    private bool _suppressSelectionGuard;

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, UIntPtr minimumWorkingSetSize, UIntPtr maximumWorkingSetSize);

    public MainWindow()
    {
        InitializeComponent();
        WindowEffects.Attach(this);
        SourceInitialized += (s, e) =>
        {
            if (WindowEffects.GetUsesSystemCaptionButtons(this))
            {
                // Windows draws native caption buttons (with Snap Layouts); hide ours and line the title bar
                // up with the native buttons (their size depends on DPI and window state)
                CaptionButtonsPanel.Visibility = Visibility.Collapsed;
                PositionTitleBar();
                Loaded += (s2, e2) => PositionTitleBar();
                SizeChanged += (s2, e2) => PositionTitleBar();
                StateChanged += (s2, e2) => Dispatcher.BeginInvoke(new Action(PositionTitleBar), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            else
            {
                // Custom caption buttons (Windows 10): report the maximize button to Windows for Snap Layouts
                System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle)?.AddHook(WndProc);
            }
        };
        App.ThemeChanged += (s, e) =>
        {
            UpdateThemeIcon();
            var settingsManager = _settingsManager; // assigned later in the constructor
            if (_widget != null && settingsManager != null) _widget.ApplySettings(settingsManager.LoadSettings()); // accent colour on the widget
        };
        _repository = new TaskRepository();
        
        // Setup Engines
        _contextEngine = new ContextAwareEngine();
        _settingsManager = new SettingsManager();
        _notificationService = new NotificationService(_contextEngine, _settingsManager, (title, message) => 
        {
            Dispatcher.Invoke(() => 
            {
                MyNotifyIcon.ShowNotification(title, message, H.NotifyIcon.Core.NotificationIcon.Info);
            });
        });
        
        // Start engine (DispatcherTimer requires UI thread)
        _contextEngine.StartMonitoring();

        var settings = _settingsManager.LoadSettings();
        _isDetailsMaximized = settings.IsDetailsMaximized;
        ApplyMaximizeState();

        LoadTasks();

        // Hook into Loaded event to aggressively trim RAM after UI is rendered
        this.Loaded += (s, e) => TrimMemory();

        _snackbarTimer.Tick += (s, e) => HideSnackbar();
        InitializeTaskForm();
        this.Closing += MainWindow_Closing;
        Application.Current.SessionEnding += (s, e) => _isExiting = true; // Windows logoff/shutdown must not be blocked
        RegisterShortcuts();
        UpdateThemeIcon();

        // Motion: the details pane slides in whenever it opens
        TaskDetailsGrid.IsVisibleChanged += (s, e) =>
        {
            if (TaskDetailsGrid.IsVisible) Motion.FadeSlideIn(TaskDetailsGrid, fromX: 16);
        };
    }

    // --- Keyboard shortcuts ---

    private void RegisterShortcuts()
    {
        AddShortcut(Key.N, ModifierKeys.Control, () =>
        {
            ShowTasksTab();
            NewTaskButton_Click(this, new RoutedEventArgs());
        });
        AddShortcut(Key.Enter, ModifierKeys.Control, () => SaveAndNew_Click(this, new RoutedEventArgs()),
            () => DetailsPanel.Visibility == Visibility.Visible && TasksTabContent.Visibility == Visibility.Visible);
        AddShortcut(Key.S, ModifierKeys.Control, () => SaveButton_Click(this, new RoutedEventArgs()),
            () => DetailsPanel.Visibility == Visibility.Visible && TasksTabContent.Visibility == Visibility.Visible);
        AddShortcut(Key.Escape, ModifierKeys.None, () =>
        {
            if (UnsavedBar.Visibility == Visibility.Visible) HideUnsavedBar(); // Esc = keep editing
            else CancelButton_Click(this, new RoutedEventArgs());
        }, () => DetailsPanel.Visibility == Visibility.Visible && TasksTabContent.Visibility == Visibility.Visible);
        AddShortcut(Key.F, ModifierKeys.Control, () =>
        {
            ShowTasksTab();
            FilterVstsTextBox.Focus();
            FilterVstsTextBox.SelectAll();
        });
        AddShortcut(Key.F1, ModifierKeys.None, () => ViewDocs_Click(this, new RoutedEventArgs()));
        // Delete only acts on the selected row, never while typing in a text field
        AddShortcut(Key.Delete, ModifierKeys.None, () => { if (TasksDataGrid.SelectedItem is TaskModel t) DeleteTask(t); },
            () => TasksTabContent.Visibility == Visibility.Visible && TasksDataGrid.SelectedItem is TaskModel
                  && Keyboard.FocusedElement is not TextBox);
    }

    private void AddShortcut(Key key, ModifierKeys modifiers, Action execute, Func<bool>? canExecute = null)
    {
        var command = new RoutedCommand();
        CommandBindings.Add(new CommandBinding(command,
            (s, e) => execute(),
            (s, e) => e.CanExecute = canExecute?.Invoke() ?? true));
        InputBindings.Add(new KeyBinding(command, key, modifiers));
    }

    private void ShowTasksTab()
    {
        if (NavTasks.IsChecked != true) NavTasks.IsChecked = true;
        else Nav_Changed(this, new RoutedEventArgs());
    }

    // --- Notification bar (snackbar) ---

    private void ShowSnackbar(string message, string? actionText = null, Action? action = null, int seconds = 6)
    {
        SnackbarText.Text = message;
        _snackbarAction = action;
        SnackbarActionButton.Content = actionText;
        SnackbarActionButton.Visibility = actionText != null && action != null ? Visibility.Visible : Visibility.Collapsed;
        bool wasHidden = Snackbar.Visibility != Visibility.Visible;
        Snackbar.Visibility = Visibility.Visible;
        if (wasHidden) Motion.FadeSlideIn(Snackbar, fromY: 12, milliseconds: 180, fade: true); // small surface: fade is cheap

        _snackbarTimer.Stop();
        _snackbarTimer.Interval = TimeSpan.FromSeconds(seconds);
        _snackbarTimer.Start();
    }

    private void HideSnackbar()
    {
        _snackbarTimer.Stop();
        _snackbarAction = null;
        Snackbar.Visibility = Visibility.Collapsed;
    }

    private void SnackbarAction_Click(object sender, RoutedEventArgs e)
    {
        var action = _snackbarAction;
        HideSnackbar();
        action?.Invoke();
    }

    private void SnackbarClose_Click(object sender, RoutedEventArgs e) => HideSnackbar();

    // --- Close to tray ---

    /// <summary>✕ / Alt+F4: hide straight to the tray without showing the widget (minimize "—" shows the widget).</summary>
    private void HideToTray()
    {
        this.Hide();
        _widget?.Hide();

        var settings = _settingsManager.LoadSettings();
        if (!settings.CloseToTrayTipShown)
        {
            settings.CloseToTrayTipShown = true;
            _settingsManager.SaveSettings(settings);
            MyNotifyIcon.ShowNotification("Still running in the tray",
                "Task And Ticket Tracker keeps running so reminders keep working. Double-click the tray icon to open it, or right-click it and choose Exit to quit.",
                H.NotifyIcon.Core.NotificationIcon.Info);
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Alt+F4 and the close button hide to the tray; only tray → Exit quits
        if (_isExiting) return;
        e.Cancel = true;
        HideToTray();
    }

    private void TrimMemory()
    {
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, unchecked((UIntPtr)(uint)-1), unchecked((UIntPtr)(uint)-1));
            }
        }
        catch { }
    }

    private void MyNotifyIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        RestoreFromTray();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        HideToTray();
    }

    private void MenuItemShow_Click(object sender, RoutedEventArgs e)
    {
        RestoreFromTray();
    }

    private void MenuItemCenterWidget_Click(object sender, RoutedEventArgs e)
    {
        if (_widget != null)
        {
            _widget.ResetPosition();
        }
        else
        {
            var settings = _settingsManager.LoadSettings();
            settings.WidgetLeft = null;
            settings.WidgetTop = null;
            settings.DockEdge = null;
            _settingsManager.SaveSettings(settings);
        }
    }

    private void MenuItemExit_Click(object sender, RoutedEventArgs e)
    {
        _isExiting = true;
        Application.Current.Shutdown();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        NavSettings.IsChecked = true; // Nav_Changed shows the page and loads the values
    }

    private bool _loadingSettings;

    /// <summary>Fills the Settings page from settings.json without triggering saves.</summary>
    private void LoadSettingsIntoControls()
    {
        _loadingSettings = true;
        try
        {
            var settings = _settingsManager.LoadSettings();
            SelectByTag(MaxActiveTasksComboBox, Math.Clamp(settings.MaxActiveTasks, 1, 10).ToString());
            NotificationsToggle.IsChecked = settings.NotificationsEnabled;
            SelectAlertTime(settings.AlertTimerHours);
            SelectByTag(ThemeComboBox, settings.Theme);
            SelectByTag(WidgetModeComboBox, settings.WidgetMode);
            SelectByTag(WidgetThemeComboBox, settings.WidgetTheme);
            WidgetTextSizeSlider.Value = Math.Clamp(settings.WidgetTextSize, 12, 36);
            WidgetTextBoldToggle.IsChecked = settings.WidgetTextBold;
            WidgetOpacitySlider.Value = Math.Clamp(settings.WidgetOpacity, 0.4, 1.0);
        }
        finally
        {
            _loadingSettings = false;
        }
    }

    private static void SelectByTag(ComboBox comboBox, string? tag)
    {
        var item = comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Tag?.ToString() == tag);
        comboBox.SelectedItem = item ?? comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();
    }

    private void SelectAlertTime(double hours)
    {
        var tag = hours.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var item = AlertTimerComboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => i.Tag?.ToString() == tag);
        if (item == null)
        {
            // Keep a custom value from an older version instead of silently changing it
            item = new ComboBoxItem { Content = $"{hours:0.##} hours before", Tag = tag };
            AlertTimerComboBox.Items.Add(item);
        }
        AlertTimerComboBox.SelectedItem = item;
    }

    /// <summary>
    /// Instant-apply for the Settings page: load, change, save, then apply (theme / widget).
    /// Ignored while the page is being filled in.
    /// </summary>
    private void UpdateSetting(Action<SettingsModel> change, bool save = true)
    {
        if (_loadingSettings) return;
        var settings = _settingsManager.LoadSettings();
        change(settings);
        if (save) _settingsManager.SaveSettings(settings);
        if (_widget != null) EnsureWidget(settings).ApplySettings(settings);
    }

    private static string? TagOf(object sender) => ((sender as ComboBox)?.SelectedItem as ComboBoxItem)?.Tag?.ToString();

    private void MaxActiveTasksComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (int.TryParse(TagOf(sender), out int max)) UpdateSetting(s => s.MaxActiveTasks = max);
    }

    private void NotificationsToggle_Click(object sender, RoutedEventArgs e) =>
        UpdateSetting(s => s.NotificationsEnabled = NotificationsToggle.IsChecked == true);

    private void AlertTimerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (double.TryParse(TagOf(sender), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double hours))
            UpdateSetting(s => s.AlertTimerHours = hours);
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var theme = TagOf(sender);
        if (theme == null || _loadingSettings) return;

        App.ApplyTheme(theme);
        // The widget follows the app theme (it can still be changed separately below)
        var widgetTheme = App.IsDarkTheme ? "Dark" : "Light";
        UpdateSetting(s => { s.Theme = theme; s.WidgetTheme = widgetTheme; });
        _loadingSettings = true;
        SelectByTag(WidgetThemeComboBox, widgetTheme);
        _loadingSettings = false;
    }

    private void WidgetModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var mode = TagOf(sender);
        if (mode == null) return;
        UpdateSetting(s =>
        {
            if (s.WidgetMode == mode) return;
            s.WidgetMode = mode;
            // A newly chosen edge-docked style starts docked to the right edge instead of floating
            if (mode == WidgetModes.EdgeDocked && s.DockEdge == null) s.DockEdge = nameof(DockEdge.Right);
        });
    }

    private void WidgetThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var theme = TagOf(sender);
        if (theme != null) UpdateSetting(s => s.WidgetTheme = theme);
    }

    private void WidgetTextBoldToggle_Click(object sender, RoutedEventArgs e) =>
        UpdateSetting(s => s.WidgetTextBold = WidgetTextBoldToggle.IsChecked == true);

    private void ApplyWidgetSliders(SettingsModel s)
    {
        s.WidgetTextSize = (int)Math.Round(WidgetTextSizeSlider.Value);
        s.WidgetOpacity = Math.Round(WidgetOpacitySlider.Value, 2);
    }

    private void WidgetSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || WidgetTextSizeSlider == null || WidgetOpacitySlider == null) return;
        // While dragging, preview on the widget without writing to disk; save when the mouse is released
        bool dragging = Mouse.LeftButton == MouseButtonState.Pressed;
        UpdateSetting(ApplyWidgetSliders, save: !dragging);
    }

    private void WidgetSlider_MouseUp(object sender, MouseButtonEventArgs e) => UpdateSetting(ApplyWidgetSliders);

    private void UpdateThemeIcon()
    {
        // Brightness icon in dark mode, moon in light mode
        if (ThemeIconTextBlock != null) ThemeIconTextBlock.Text = App.IsDarkTheme ? "\xE706" : "\xE708";
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsManager.LoadSettings();
        if (App.IsDarkTheme)
        {
            settings.Theme = "Light";
            settings.WidgetTheme = "Light";
        }
        else
        {
            settings.Theme = "Dark";
            settings.WidgetTheme = "Dark";
        }
        _settingsManager.SaveSettings(settings);
        App.ApplyTheme(settings.Theme);
        if (_widget != null)
        {
            _widget.ApplySettings(settings);
        }

        // Keep the Settings page in sync
        if (NavSettings.IsChecked == true) LoadSettingsIntoControls();
    }

    private void LoadTasks()
    {
        // Load and sort by priority
        _tasks = _repository.LoadTasks().OrderBy(t => t.Priority).ToList();

        if (_repository.LoadWarning != null)
        {
            MessageBox.Show(_repository.LoadWarning, "Task Data Recovered", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        
        // Migrate legacy "Doing" state to "In progress"
        foreach (var t in _tasks)
        {
            if (t.State == "Doing") t.State = "In progress";
        }
        
        EnforceActiveTaskRules(); // Initial check on load
        
        RefreshGrid();
        
        // Push to notification service
        _notificationService.UpdateTasks(_tasks);
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        RefreshGrid();
    }

    private void RefreshGrid()
    {
        if (_tasks == null) return;
        
        var filtered = _tasks.AsEnumerable();
        
        if (FilterVstsTextBox != null && !string.IsNullOrWhiteSpace(FilterVstsTextBox.Text))
        {
            var search = FilterVstsTextBox.Text.ToLower();
            filtered = filtered.Where(t => (t.VstsNumber?.ToLower().Contains(search) == true) || (t.Title?.ToLower().Contains(search) == true));
        }
        
        // Multi-select State filter
        var validStates = new List<string>();
        if (FilterStateToDo?.IsChecked == true) validStates.Add("To Do");
        if (FilterStateInProgress?.IsChecked == true) validStates.Add("In progress");
        if (FilterStateDone?.IsChecked == true) validStates.Add("Done");
        
        filtered = filtered.Where(t => validStates.Contains(t.State));
        
        if (FilterActiveCheckBox != null && FilterActiveCheckBox.IsChecked == true)
        {
            filtered = filtered.Where(t => t.IsActive);
        }

        var selectedId = _currentTask?.Id ?? Guid.Empty;

        TasksDataGrid.SelectionChanged -= TasksDataGrid_SelectionChanged;
        TasksDataGrid.ItemsSource = null;
        var filteredList = filtered.ToList();
        TasksDataGrid.ItemsSource = filteredList;
        
        if (selectedId != Guid.Empty)
        {
            var itemToSelect = filteredList.FirstOrDefault(t => t.Id == selectedId);
            if (itemToSelect != null)
            {
                TasksDataGrid.SelectedItem = itemToSelect;
            }
        }
        TasksDataGrid.SelectionChanged += TasksDataGrid_SelectionChanged;

        UpdateEmptyState(filteredList.Count);
    }

    private void UpdateEmptyState(int visibleCount)
    {
        if (EmptyStatePanel == null) return;
        EmptyStatePanel.Visibility = visibleCount == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (visibleCount > 0) return;

        if (_tasks.Count == 0)
        {
            EmptyStateTitle.Text = "No tasks yet";
            EmptyStateHint.Text = "Create a task, break it into steps, and mark it active to see it on the widget.";
            EmptyStateButton.Content = "Create your first task";
        }
        else
        {
            EmptyStateTitle.Text = "No tasks match your filters";
            EmptyStateHint.Text = "Done tasks are hidden by default.";
            EmptyStateButton.Content = "Show all tasks";
        }
    }

    private void EmptyStateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_tasks.Count == 0)
        {
            NewTaskButton_Click(sender, e);
            TitleTextBox.Focus();
            return;
        }

        // Clear every filter so all tasks, including Done ones, are visible
        FilterVstsTextBox.Text = string.Empty;
        FilterStateToDo.IsChecked = true;
        FilterStateInProgress.IsChecked = true;
        FilterStateDone.IsChecked = true;
        FilterActiveCheckBox.IsChecked = false;
        RefreshGrid();
    }

    private bool _isDetailsMaximized = false;

    private void ToggleMaximize_Click(object sender, RoutedEventArgs e)
    {
        _isDetailsMaximized = !_isDetailsMaximized;
        ApplyMaximizeState();
        
        var settings = _settingsManager.LoadSettings();
        settings.IsDetailsMaximized = _isDetailsMaximized;
        _settingsManager.SaveSettings(settings);
    }

    private void ApplyMaximizeState()
    {
        if (_isDetailsMaximized && DetailsPanel != null && DetailsPanel.Visibility == Visibility.Visible)
        {
            TaskListColumn.Width = new GridLength(0);
            TasksDividerColumn.Width = new GridLength(0);
            TaskDetailsColumn.Width = new GridLength(1, GridUnitType.Star);
            TaskDetailsGrid.Width = double.NaN;
            TaskDetailsGrid.Margin = new Thickness(28, 10, 28, 20); // same gutter as the other pages

            MaximizeDetailsButton.Content = "\xE73F";
            MaximizeDetailsButton.ToolTip = "Restore details";

            DescriptionTextBox.MinHeight = 120;
            DescriptionTextBox.MaxHeight = 420;
            AcTextBox.MinHeight = 120;
            AcTextBox.MaxHeight = 420;

            // Apply Split Layout
            DetailsRightColumn.Width = new GridLength(35, GridUnitType.Star);
            DetailsFormGrid.ColumnDefinitions[0].Width = new GridLength(65, GridUnitType.Star);
            RightColumnStack.Visibility = Visibility.Visible;
            if (ContentStack.Children.Contains(PlanningSection))
            {
                ContentStack.Children.Remove(PlanningSection);
                RightColumnStack.Children.Add(PlanningSection);
            }
            PlanningSection.Margin = new Thickness(0); // top of the right column: align with the Title label
        }
        else
        {
            TaskListColumn.Width = new GridLength(1, GridUnitType.Star);
            TasksDividerColumn.Width = GridLength.Auto;
            TaskDetailsColumn.Width = GridLength.Auto;
            TaskDetailsGrid.Width = 350;
            TaskDetailsGrid.Margin = new Thickness(30, 10, 30, 20);

            MaximizeDetailsButton.Content = "\xE740";
            MaximizeDetailsButton.ToolTip = "Expand details";

            // Notes start compact and grow with their content
            DescriptionTextBox.MinHeight = 60;
            DescriptionTextBox.MaxHeight = 300;
            AcTextBox.MinHeight = 60;
            AcTextBox.MaxHeight = 300;

            // Restore Layout
            DetailsRightColumn.Width = new GridLength(0);
            DetailsFormGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            RightColumnStack.Visibility = Visibility.Collapsed;
            if (RightColumnStack.Children.Contains(PlanningSection))
            {
                RightColumnStack.Children.Remove(PlanningSection);
                // Order: Title, Steps, Planning, Notes
                ContentStack.Children.Insert(ContentStack.Children.IndexOf(StepsSection) + 1, PlanningSection);
            }
            PlanningSection.Margin = new Thickness(0, 16, 0, 0);
        }
    }

    private void NewTaskButton_Click(object sender, RoutedEventArgs e)
    {
        RunAfterUnsavedCheck(() => StartNewTask());
    }

    /// <summary>Opens an empty (optionally prefilled) form for a new task and puts the cursor in Title.</summary>
    private void StartNewTask(string? title = null, string? ticket = null)
    {
        _currentTask = new TaskModel { Title = title ?? string.Empty, VstsNumber = ticket ?? string.Empty };
        _suppressSelectionGuard = true;
        TasksDataGrid.SelectedItem = null;
        _suppressSelectionGuard = false;
        PopulateForm(_currentTask);
        DetailsPanel.Visibility = Visibility.Visible;
        ButtonPanel.Visibility = Visibility.Visible;
        ApplyMaximizeState();

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
        {
            TitleTextBox.Focus();
            TitleTextBox.CaretIndex = TitleTextBox.Text.Length;
        }));
    }

    private void TasksDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionGuard) return;

        if (TasksDataGrid.SelectedItem is TaskModel selected)
        {
            // Switching to another task while this form has edits: ask first, and keep the current selection
            if (_currentTask != null && selected.Id != _currentTask.Id && IsFormDirty())
            {
                _suppressSelectionGuard = true;
                TasksDataGrid.SelectedItem = _tasks.Any(t => t.Id == _currentTask.Id) ? _currentTask : null;
                _suppressSelectionGuard = false;
                ShowUnsavedBar(() => TasksDataGrid.SelectedItem = selected);
                return;
            }

            _currentTask = selected;
            PopulateForm(selected);
            DetailsPanel.Visibility = Visibility.Visible;
            ButtonPanel.Visibility = Visibility.Visible;
            ApplyMaximizeState();
        }
        else
        {
            DetailsPanel.Visibility = Visibility.Collapsed;
            ButtonPanel.Visibility = Visibility.Collapsed;
            ApplyMaximizeState();
        }
    }

    private void PopulateForm(TaskModel task)
    {
        HideUnsavedBar();
        if (TitleErrorText != null) TitleErrorText.Visibility = Visibility.Collapsed;
        if (StepsErrorText != null) StepsErrorText.Visibility = Visibility.Collapsed;
        VstsTextBox.Text = task.VstsNumber;
        TitleTextBox.Text = task.Title;
        DescriptionTextBox.Text = task.Description;
        AcTextBox.Text = task.AcceptanceCriteria;
        NewStepTextBox.Text = string.Empty;

        // Due date: a 00:00 time means "all day"
        _dueHasTime = task.TargetDate is DateTime due && due.TimeOfDay != TimeSpan.Zero;
        TargetDatePicker.SelectedDate = task.TargetDate?.Date;
        SelectTime(task.TargetDate?.TimeOfDay ?? new TimeSpan(17, 0, 0));
        UpdateDueUi();

        foreach (ComboBoxItem item in StateComboBox.Items)
        {
            if ((item.Tag?.ToString() ?? item.Content.ToString()) == task.State)
            {
                StateComboBox.SelectedItem = item;
                break;
            }
        }

        bool isNew = !_tasks.Any(t => t.Id == task.Id);
        DetailsHeaderText.Text = isNew ? "New task" : "Task details";
        PriorityText.Text = $"#{task.Priority} in the list";
        TextBoxHelper.SetPlaceholder(VstsTextBox, $"Auto: {TaskInput.NextAutoTicket(_tasks)}");
        DeleteTaskButton.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;
        SaveAndNewButton.Visibility = isNew ? Visibility.Visible : Visibility.Collapsed;

        // New tasks choose "Start now" and a position; existing tasks show State and Priority
        StatePanel.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;
        PriorityPanel.Visibility = isNew ? Visibility.Collapsed : Visibility.Visible;
        StartNowPanel.Visibility = isNew ? Visibility.Visible : Visibility.Collapsed;
        PositionPanel.Visibility = isNew ? Visibility.Visible : Visibility.Collapsed;
        StartNowToggle.IsChecked = false;
        PositionBottomRadio.IsChecked = true;

        if (task.Steps == null)
            task.Steps = new System.Collections.ObjectModel.ObservableCollection<TicketStep>();

        _editingSteps = new System.Collections.ObjectModel.ObservableCollection<TicketStep>(
            task.Steps.Select(s => new TicketStep { Id = s.Id, IsDone = s.IsDone, Description = s.Description })
        );
        StepsListView.ItemsSource = _editingSteps;
        UpdateStepsHeader();

        _formSnapshot = FormSnapshot();
        DetailsScroll.ScrollToTop();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (SaveCurrentTask()) CloseDetailsPanel();
    }

    private void SaveAndNew_Click(object sender, RoutedEventArgs e)
    {
        var title = TitleTextBox.Text.Trim();
        if (!SaveCurrentTask()) return;
        StartNewTask();
        ShowSnackbar($"Saved “{Shorten(title)}”. Add the next one.");
    }

    /// <summary>Validates and saves the form into <see cref="_currentTask"/>. Returns false when validation fails.</summary>
    private bool SaveCurrentTask()
    {
        if (_currentTask == null) return false;

        bool hasError = false;

        if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
        {
            TitleErrorText.Visibility = Visibility.Visible;
            TitleTextBox.Focus();
            hasError = true;
        }
        else
        {
            TitleErrorText.Visibility = Visibility.Collapsed;
        }

        if (_editingSteps.Any(s => string.IsNullOrWhiteSpace(s.Description)))
        {
            StepsErrorText.Visibility = Visibility.Visible;
            hasError = true;
        }
        else
        {
            StepsErrorText.Visibility = Visibility.Collapsed;
        }

        if (hasError) return false;

        bool isNew = !_tasks.Any(t => t.Id == _currentTask.Id);

        _currentTask.VstsNumber = string.IsNullOrWhiteSpace(VstsTextBox.Text) ? TaskInput.NextAutoTicket(_tasks) : VstsTextBox.Text.Trim();
        _currentTask.Title = TitleTextBox.Text.Trim();
        _currentTask.Description = DescriptionTextBox.Text;
        _currentTask.AcceptanceCriteria = AcTextBox.Text;
        _currentTask.Steps = new System.Collections.ObjectModel.ObservableCollection<TicketStep>(
            _editingSteps.Select(s => new TicketStep { Id = s.Id, IsDone = s.IsDone, Description = s.Description })
        );
        _currentTask.TargetDate = CurrentDueValue();

        if (isNew)
        {
            // "Start now" makes the task active straight away, within the active-task limit
            bool startNow = StartNowToggle.IsChecked == true;
            int maxActive = _settingsManager.LoadSettings().MaxActiveTasks;
            if (startNow && _tasks.Count(t => t.IsActive) >= maxActive)
            {
                startNow = false;
                ShowSnackbar($"Saved as To Do — you already have {maxActive} active tasks.",
                    "Change limit", () => OpenSettings_Click(this, new RoutedEventArgs()));
            }
            _currentTask.State = startNow ? "In progress" : "To Do";
            _currentTask.IsActive = startNow;

            if (PositionTopRadio.IsChecked == true) _tasks.Insert(0, _currentTask);
            else _tasks.Add(_currentTask);
        }
        else
        {
            if (StateComboBox.SelectedItem is ComboBoxItem stateItem)
            {
                _currentTask.State = stateItem.Tag?.ToString() ?? stateItem.Content.ToString() ?? "To Do";
            }
            if (_currentTask.State != "In progress")
            {
                _currentTask.IsActive = false;
            }
        }

        SaveAndRefresh(); // renumbers priorities from list order
        _formSnapshot = FormSnapshot();
        HideUnsavedBar();
        return true;
    }

    private static string Shorten(string text, int max = 40) => text.Length > max ? text[..max] + "…" : text;
        
    // --- Task form: setup, due date, steps, unsaved changes ---

    private void InitializeTaskForm()
    {
        // 30-minute time slots
        for (int minutes = 0; minutes < 24 * 60; minutes += 30)
        {
            var time = TimeSpan.FromMinutes(minutes);
            TimeComboBox.Items.Add(new ComboBoxItem { Content = time.ToString(@"hh\:mm"), Tag = time });
        }

        // Pasting a multi-line list into the step box adds one step per line
        DataObject.AddPastingHandler(NewStepTextBox, NewStepTextBox_Pasting);

        StartNowToggle.Checked += (s, e) => StartNowHint.Text = "Active now";
        StartNowToggle.Unchecked += (s, e) => StartNowHint.Text = "To Do";
    }

    private void SelectTime(TimeSpan time)
    {
        var match = TimeComboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (TimeSpan)i.Tag == time);
        if (match == null)
        {
            // Keep an existing off-slot time such as 17:45 instead of silently rounding it
            match = new ComboBoxItem { Content = time.ToString(@"hh\:mm"), Tag = time };
            int index = TimeComboBox.Items.OfType<ComboBoxItem>().TakeWhile(i => (TimeSpan)i.Tag < time).Count();
            TimeComboBox.Items.Insert(index, match);
        }
        TimeComboBox.SelectedItem = match;
    }

    private DateTime? CurrentDueValue()
    {
        if (TargetDatePicker.SelectedDate is not DateTime date) return null;
        var time = _dueHasTime && TimeComboBox.SelectedItem is ComboBoxItem { Tag: TimeSpan t } ? t : TimeSpan.Zero;
        return date.Date + time;
    }

    private void UpdateDueUi()
    {
        var date = TargetDatePicker.SelectedDate;
        DueDateText.Text = date is DateTime d
            ? d.ToString("ddd, d MMM yyyy", System.Globalization.CultureInfo.CurrentCulture)
            : "No due date";
        DueClearChip.Visibility = date != null ? Visibility.Visible : Visibility.Collapsed;
        AddTimeButton.Visibility = date != null && !_dueHasTime ? Visibility.Visible : Visibility.Collapsed;
        TimePanel.Visibility = date != null && _dueHasTime ? Visibility.Visible : Visibility.Collapsed;
    }

    private void DueDateButton_Click(object sender, RoutedEventArgs e) => TargetDatePicker.IsDropDownOpen = true;

    private void TargetDatePicker_SelectedDateChanged(object? sender, SelectionChangedEventArgs e) => UpdateDueUi();

    private void SetDueDate(DateTime? date)
    {
        TargetDatePicker.SelectedDate = date;
        if (date == null) _dueHasTime = false;
        UpdateDueUi();
    }

    private void DueToday_Click(object sender, RoutedEventArgs e) => SetDueDate(DateTime.Today);
    private void DueTomorrow_Click(object sender, RoutedEventArgs e) => SetDueDate(DateTime.Today.AddDays(1));
    private void DueNextWeek_Click(object sender, RoutedEventArgs e) => SetDueDate(TaskInput.NextWeekStart(DateTime.Today));
    private void DueClear_Click(object sender, RoutedEventArgs e) => SetDueDate(null);

    private void AddTime_Click(object sender, RoutedEventArgs e)
    {
        _dueHasTime = true;
        UpdateDueUi();
        TimeComboBox.Focus();
    }

    private void RemoveTime_Click(object sender, RoutedEventArgs e)
    {
        _dueHasTime = false;
        UpdateDueUi();
    }

    private void UpdateStepsHeader()
    {
        var progress = TaskFormat.StepProgress(_editingSteps);
        StepsProgressText.Text = progress.Length == 0 ? string.Empty : $"{progress} done";
    }

    private void NewStepTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true)) return;
        var text = e.SourceDataObject.GetData(DataFormats.UnicodeText) as string;
        if (text == null || !text.Contains('\n')) return; // single line: normal paste

        e.CancelCommand();
        var lines = TaskInput.SplitStepLines(text);
        foreach (var line in lines) _editingSteps.Add(new TicketStep { Description = line });
        StepsListView.Items.Refresh();
        UpdateStepsHeader();
        if (lines.Count > 0) ShowSnackbar($"Added {lines.Count} steps from the pasted list.");
    }

    /// <summary>A string capturing every editable value of the form, used to detect unsaved edits.</summary>
    private string FormSnapshot()
    {
        var steps = string.Join("|", _editingSteps.Select(s => $"{s.IsDone}:{s.Description}"));
        var state = (StateComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        return string.Join("\u001f", TitleTextBox.Text, VstsTextBox.Text, DescriptionTextBox.Text, AcTextBox.Text,
            state, StartNowToggle.IsChecked, PositionTopRadio.IsChecked, CurrentDueValue(), NewStepTextBox.Text, steps);
    }

    private bool IsFormDirty() =>
        DetailsPanel.Visibility == Visibility.Visible && _currentTask != null && FormSnapshot() != _formSnapshot;

    /// <summary>Runs <paramref name="action"/> now, or after the user resolves unsaved edits in the form.</summary>
    private void RunAfterUnsavedCheck(Action action)
    {
        if (IsFormDirty()) ShowUnsavedBar(action);
        else action();
    }

    private void ShowUnsavedBar(Action afterResolved)
    {
        _afterUnsavedResolved = afterResolved;
        UnsavedBar.Visibility = Visibility.Visible;
    }

    private void HideUnsavedBar()
    {
        _afterUnsavedResolved = null;
        if (UnsavedBar != null) UnsavedBar.Visibility = Visibility.Collapsed;
    }

    private void UnsavedKeepEditing_Click(object sender, RoutedEventArgs e) => HideUnsavedBar();

    private void UnsavedDiscard_Click(object sender, RoutedEventArgs e)
    {
        var next = _afterUnsavedResolved;
        _formSnapshot = FormSnapshot(); // accept the loss
        HideUnsavedBar();
        next?.Invoke();
    }

    private void UnsavedSave_Click(object sender, RoutedEventArgs e)
    {
        var next = _afterUnsavedResolved;
        if (!SaveCurrentTask()) return; // validation message is shown; stay in the form
        next?.Invoke();
    }

    // --- Quick add ---

    private void QuickAddTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            QuickAddTextBox.Text = string.Empty;
            e.Handled = true;
            return;
        }
        if (e.Key != Key.Enter) return;
        e.Handled = true;

        var (ticket, title) = TaskInput.ParseQuickAdd(QuickAddTextBox.Text);
        if (title.Length == 0) return;

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            // Ctrl+Enter: continue in the full form (nothing is saved until Save)
            RunAfterUnsavedCheck(() =>
            {
                QuickAddTextBox.Text = string.Empty;
                StartNewTask(title, ticket);
            });
            return;
        }

        var task = new TaskModel
        {
            Title = title,
            VstsNumber = string.IsNullOrWhiteSpace(ticket) ? TaskInput.NextAutoTicket(_tasks) : ticket
        };
        _tasks.Add(task);
        SaveAndRefresh();
        QuickAddTextBox.Text = string.Empty;
        ShowSnackbar($"Added “{Shorten(title)}” as #{task.VstsNumber}.", "Open", () => OpenTaskDetails(task));
    }

    /// <summary>Opens a task's details even if the current filters hide it in the list.</summary>
    private void OpenTaskDetails(TaskModel task)
    {
        RunAfterUnsavedCheck(() =>
        {
            ShowTasksTab();
            if (TasksDataGrid.Items.Contains(task))
            {
                TasksDataGrid.SelectedItem = task;
                TasksDataGrid.ScrollIntoView(task);
                return;
            }
            _currentTask = task;
            PopulateForm(task);
            DetailsPanel.Visibility = Visibility.Visible;
            ButtonPanel.Visibility = Visibility.Visible;
            ApplyMaximizeState();
        });
    }
        
    private void CloseDetailsPanel()
    {
        DetailsPanel.Visibility = Visibility.Collapsed;
        ButtonPanel.Visibility = Visibility.Collapsed;
        TasksDataGrid.SelectedItem = null;
        _currentTask = null;
        ApplyMaximizeState();
    }

    private DocumentationWindow? _userGuide;

    private void ViewDocs_Click(object sender, RoutedEventArgs e)
    {
        // One guide at a time: pressing F1 again just brings the open guide to the front
        if (_userGuide != null)
        {
            _userGuide.Activate();
            return;
        }

        _userGuide = new DocumentationWindow { Owner = this };
        _userGuide.Closed += (s, args) => _userGuide = null;
        _userGuide.ShowDialog();
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch { }
        e.Handled = true;
    }

    private void ActiveCheckBox_Clicked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.DataContext is TaskModel task)
        {
            if (task.IsActive)
            {
                task.State = "In progress"; // All active tasks must be in In progress state
            }
        }

        // Enforce max active tasks setting
        var settings = _settingsManager.LoadSettings();
        var activeCount = _tasks.Count(t => t.IsActive);
        if (activeCount > settings.MaxActiveTasks)
        {
            ShowSnackbar($"You can have up to {settings.MaxActiveTasks} active tasks. Finish or deactivate one first.",
                "Change limit", () => OpenSettings_Click(this, new RoutedEventArgs()));
            
            // Revert the check
            if (sender is CheckBox cbRevert && cbRevert.DataContext is TaskModel t)
            {
                t.IsActive = false;
                cbRevert.IsChecked = false;
            }
            return;
        }

        SaveAndRefresh();
    }

    private void ActiveCheckBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is CheckBox cb && cb.DataContext is TaskModel task)
        {
            cb.IsChecked = !cb.IsChecked;
            task.IsActive = cb.IsChecked == true;
            ActiveCheckBox_Clicked(sender, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentTask == null) return;
        var existing = _tasks.FirstOrDefault(t => t.Id == _currentTask.Id);
        if (existing != null) DeleteTask(existing);
    }

    /// <summary>Deletes immediately and offers Undo, instead of asking for confirmation.</summary>
    private void DeleteTask(TaskModel task)
    {
        int index = _tasks.IndexOf(task);
        if (index < 0) return;

        bool wasActive = task.IsActive;
        string state = task.State;

        if (_currentTask?.Id == task.Id) CloseDetailsPanel();
        _tasks.RemoveAt(index);
        SaveAndRefresh();

        var title = task.Title.Length > 40 ? task.Title[..40] + "…" : task.Title;
        ShowSnackbar($"Deleted “{title}”", "Undo", () =>
        {
            if (_tasks.Any(t => t.Id == task.Id)) return;

            // Another task may have been auto-activated meanwhile; don't exceed the active limit
            var max = _settingsManager.LoadSettings().MaxActiveTasks;
            task.IsActive = wasActive && _tasks.Count(t => t.IsActive) < max;
            task.State = state;
            _tasks.Insert(Math.Min(index, _tasks.Count), task);
            SaveAndRefresh();
        });
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        RunAfterUnsavedCheck(CloseDetailsPanel);
    }

    private void EnforceActiveTaskRules()
    {
        if (_tasks == null || !_tasks.Any()) return;
        
        // Ensure all active tasks are in 'In progress' state
        foreach (var task in _tasks.Where(t => t.IsActive))
        {
            task.State = "In progress";
        }
        
        // Always one top priority non-done task should be active
        if (!_tasks.Any(t => t.IsActive && t.State != "Done"))
        {
            var nextPriorityTask = _tasks.OrderBy(t => t.Priority).FirstOrDefault(t => t.State != "Done");
            if (nextPriorityTask != null)
            {
                nextPriorityTask.IsActive = true;
                nextPriorityTask.State = "In progress";
                if (_notificationService != null)
                {
                    _notificationService.ShowTaskReminder("Auto-Activated Next Task", nextPriorityTask.Title);
                }
            }
        }
    }

    public void NotifyTaskUpdatedFromWidget(TaskModel task)
    {
        if (_currentTask != null && _currentTask.Id == task.Id)
        {
            CloseDetailsPanel();
        }
    }

    public void SaveAndRefresh()
    {
        // Re-assign priority based on list index to ensure consistency (1 to N)
        for (int i = 0; i < _tasks.Count; i++)
        {
            _tasks[i].Priority = i + 1;
        }

        EnforceActiveTaskRules();

        PersistTasks();
        RefreshGrid();
        
        _notificationService?.UpdateTasks(_tasks);
        UpdateWidgetIfActive();
    }

    private void PersistTasks()
    {
        try
        {
            _repository.SaveTasks(_tasks);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"Your changes could not be saved to disk:\n{ex.Message}\n\nThey are kept in memory; try again before closing the app.", "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        UpdateMaximizeState();
        if (this.WindowState == WindowState.Minimized)
        {
            this.Hide();
            
            var settings = _settingsManager.LoadSettings();
            var widget = EnsureWidget(settings);
            widget.ApplySettings(settings);
            widget.SetActiveTasks(_tasks.Where(t => t.IsActive).ToList());
            widget.Show();
        }
    }

    /// <summary>
    /// Returns the widget presenter for the configured style, replacing the current one when the style changed.
    /// Falls back to the floating widget when the taskbar strip can't be used (e.g. vertical taskbar).
    /// </summary>
    private IWidgetPresenter EnsureWidget(SettingsModel settings)
    {
        var mode = settings.WidgetMode;
        if (mode == WidgetModes.TaskbarStrip && !TaskbarStripView.IsSupported())
        {
            if (!_taskbarFallbackNotified)
            {
                _taskbarFallbackNotified = true;
                MyNotifyIcon.ShowNotification("Taskbar strip unavailable",
                    "The taskbar strip needs a horizontal taskbar. Showing the floating widget instead.",
                    H.NotifyIcon.Core.NotificationIcon.Info);
            }
            mode = WidgetModes.Floating;
        }

        if (_widget != null && _widgetMode == mode) return _widget;

        bool wasVisible = _widget?.IsVisible == true;
        if (_widget != null)
        {
            _widget.OnSkipRequested -= WidgetView_OnSkipRequested;
            _widget.OnResetRequested -= WidgetView_OnResetRequested;
            _widget.OnTaskRequested -= WidgetView_OnTaskRequested;
            _widget.Close();
        }

        _widget = mode switch
        {
            WidgetModes.TaskbarStrip => new TaskbarStripView(this),
            WidgetModes.EdgeDocked => new WidgetView(this, WidgetRole.EdgeDocked),
            _ => new WidgetView(this, WidgetRole.Floating),
        };
        _widgetMode = mode;
        _widget.OnSkipRequested += WidgetView_OnSkipRequested;
        _widget.OnResetRequested += WidgetView_OnResetRequested;
        _widget.OnTaskRequested += WidgetView_OnTaskRequested;

        if (wasVisible)
        {
            _widget.ApplySettings(settings);
            _widget.SetActiveTasks(_tasks.Where(t => t.IsActive).ToList());
            _widget.Show();
        }
        return _widget;
    }

    private void WidgetView_OnSkipRequested(object? sender, TaskModel currentTask)
    {
        if (_currentTask != null && _currentTask.Id == currentTask.Id)
        {
            CloseDetailsPanel();
        }
        
        currentTask.IsActive = false;
        
        var nextTask = _tasks
            .Where(t => t.State != "Done" && !t.IsActive && t.Id != currentTask.Id)
            .OrderBy(t => t.Priority)
            .FirstOrDefault(t => t.Priority >= currentTask.Priority);

        if (nextTask == null)
        {
            // Wrap around if no lower priority tasks exist
            nextTask = _tasks
                .Where(t => t.State != "Done" && !t.IsActive && t.Id != currentTask.Id)
                .OrderBy(t => t.Priority)
                .FirstOrDefault();
        }
            
        if (nextTask != null)
        {
            nextTask.IsActive = true;
            nextTask.State = "In progress";
        }
        
        PersistTasks();
        TasksDataGrid.Items.Refresh();
        
        if (_widget != null)
        {
            _widget.SetActiveTasks(_tasks.Where(t => t.IsActive).ToList());
        }
    }

    private void WidgetView_OnResetRequested(object? sender, EventArgs e)
    {
        if (_currentTask != null && _currentTask.IsActive)
        {
            CloseDetailsPanel();
        }
        
        foreach (var task in _tasks.Where(t => t.IsActive))
        {
            task.IsActive = false;
        }
        
        var highestTask = _tasks
            .Where(t => t.State != "Done")
            .OrderBy(t => t.Priority)
            .FirstOrDefault();
            
        if (highestTask != null)
        {
            highestTask.IsActive = true;
        }
        
        PersistTasks();
        TasksDataGrid.Items.Refresh();
        
        if (_widget != null)
        {
            _widget.SetActiveTasks(_tasks.Where(t => t.IsActive).ToList());
        }
    }

    public void RestoreFromTray()
    {
        this.Show();
        this.WindowState = WindowState.Normal;
        this.Activate();
        
        if (_widget != null)
        {
            _widget.Hide();
        }
        
        LoadTasks();
    }

    public void OpenTask(TaskModel task)
    {
        RestoreFromTray();
        
        var existingTask = _tasks.FirstOrDefault(t => t.Id == task.Id);
        if (existingTask != null)
        {
            TasksDataGrid.SelectedItem = existingTask;
            TasksDataGrid.ScrollIntoView(existingTask);
        }
    }

    private void WidgetView_OnTaskRequested(object? sender, TaskModel task)
    {
        OpenTask(task);
    }
    
    private void UpdateWidgetIfActive()
    {
        if (_widget != null && _widget.IsVisible)
        {
            _widget.SetActiveTasks(_tasks.Where(t => t.IsActive).ToList());
        }
    }

    // --- Drag and Drop Logic ---

    private void TasksDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _draggedRow = null;
        var source = e.OriginalSource as DependencyObject;
        var row = FindVisualParent<DataGridRow>(source);
        if (row == null || row.IsEditing) return;

        // Let the ACTIVE checkbox handle its own clicks
        if (source is CheckBox || FindVisualParent<CheckBox>(source) != null) return;

        _draggedRow = row;
        _gridDragStartPoint = e.GetPosition(null);

        // The DataGrid selects (and so opens) a row on mouse *down*, which made every drag open the ticket.
        // Take over the press and select on mouse up instead, only when the press didn't become a drag.
        e.Handled = true;
        TasksDataGrid.Focus();
    }

    private void TasksDataGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggedRow == null) return;
        var item = _draggedRow.Item;
        _draggedRow = null;
        TasksDataGrid.SelectedItem = item; // opens the details via SelectionChanged
    }

    private void TasksDataGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedRow == null) return;

        // Only start a drag after the mouse really moves, so clicking a row never triggers a reorder
        Point position = e.GetPosition(null);
        if (Math.Abs(position.X - _gridDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _gridDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var row = _draggedRow;
        _draggedRow = null; // a drag is not a click: don't select the row when the button is released
        DragDrop.DoDragDrop(row, row.Item, DragDropEffects.Move);
    }
        
    private void TasksDataGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (TasksDataGrid.ActualWidth <= 0) return;

        // Narrow list (details panel open at default size): the state pill becomes a coloured dot
        bool narrow = TasksDataGrid.ActualWidth < 620;
        StateColumn.Width = new DataGridLength(narrow ? 44 : 110);

        // The title takes whatever the fixed columns leave (minus room for the vertical scrollbar)
        double otherColumnsWidth = TasksDataGrid.Columns
            .Where(c => c != TitleColumn)
            .Sum(c => c.Width.IsAbsolute ? c.Width.Value : c.ActualWidth);
        TitleColumn.Width = new DataGridLength(Math.Max(160, TasksDataGrid.ActualWidth - otherColumnsWidth - 22));
    }

    private void TasksDataGrid_Drop(object sender, DragEventArgs e)
    {
        var droppedData = e.Data.GetData(typeof(TaskModel)) as TaskModel;
        var targetRow = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);

        if (droppedData != null && targetRow != null)
        {
            var targetData = targetRow.Item as TaskModel;
            if (targetData != null && droppedData != targetData)
            {
                int droppedIndex = _tasks.IndexOf(droppedData);
                int targetIndex = _tasks.IndexOf(targetData);

                if (droppedIndex > -1 && targetIndex > -1)
                {
                    _tasks.RemoveAt(droppedIndex);
                    _tasks.Insert(targetIndex, droppedData);
                    SaveAndRefresh();
                }
            }
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        if (child == null) return null;

        DependencyObject? parentObject = null;
        if (child is System.Windows.ContentElement contentElement)
        {
            parentObject = System.Windows.LogicalTreeHelper.GetParent(contentElement);
        }
        else if (child is System.Windows.Media.Visual || child is System.Windows.Media.Media3D.Visual3D)
        {
            parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);
        }

        if (parentObject == null) return null;

        if (parentObject is T parent)
            return parent;
        else
            return FindVisualParent<T>(parentObject);
    }

    public void MarkTaskDone(TaskModel task)
    {
        if (_currentTask != null && _currentTask.Id == task.Id)
        {
            CloseDetailsPanel();
        }
        
        task.State = "Done";
        task.IsActive = false;
        
        SaveAndRefresh();
    }

    private void Nav_Changed(object sender, RoutedEventArgs e)
    {
        if (TasksTabContent == null || AboutTabContent == null || SettingsTabContent == null) return;

        TasksTabContent.Visibility = NavTasks?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        AboutTabContent.Visibility = NavAbout?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        SettingsTabContent.Visibility = NavSettings?.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

        if (NavSettings?.IsChecked == true) LoadSettingsIntoControls();

        var page = NavTasks?.IsChecked == true ? TasksTabContent : NavAbout?.IsChecked == true ? AboutTabContent : SettingsTabContent;
        if (IsLoaded) Motion.FadeSlideIn(page, fromY: 8, milliseconds: 120);
    }

    private const double NavExpandedWidth = 220;
    private const double NavCompactWidth = 48;
    private bool _navToggledByUser;

    private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
    {
        _navToggledByUser = true;
        bool expanded = SidebarColumnDef.Width.Value > NavCompactWidth;
        SidebarColumnDef.Width = new GridLength(expanded ? NavCompactWidth : NavExpandedWidth);
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Compact navigation on narrow windows, unless the user chose a width with the ☰ button
        if (_navToggledByUser || SidebarColumnDef == null) return;
        SidebarColumnDef.Width = new GridLength(ActualWidth < 1000 ? NavCompactWidth : NavExpandedWidth);
    }

    // --- Maximize / restore and Snap Layouts ---

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Dispatcher.BeginInvoke(new Action(PositionTitleBar), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// With native caption buttons, the title bar takes their height and the theme button sits on the same line,
    /// directly to their left, at the same height.
    /// </summary>
    private void PositionTitleBar()
    {
        if (!WindowEffects.GetUsesSystemCaptionButtons(this)) return;
        if (!WindowEffects.TryGetCaptionButtonBounds(this, out Rect bounds)) return;

        double barHeight = Math.Round(bounds.Bottom, 1);
        TitleRow.Height = new GridLength(barHeight);
        var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(this);
        if (chrome != null && !chrome.IsFrozen) chrome.CaptionHeight = barHeight;

        // Distance from the right edge of the title bar to the left edge of the native buttons
        double rightOfBar = TitleBar.ActualWidth > 0 ? TitleBar.ActualWidth : ActualWidth - RootGrid.Margin.Left - RootGrid.Margin.Right;
        double offset = Math.Max(0, rightOfBar - (bounds.Left - RootGrid.Margin.Left)) + 4;
        ThemeToggleButton.VerticalAlignment = VerticalAlignment.Top;
        ThemeToggleButton.Height = bounds.Height;
        ThemeToggleButton.Margin = new Thickness(0, bounds.Top, offset, 0);
    }

    private void MaximizeRestore_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void UpdateMaximizeState()
    {
        bool maximized = WindowState == WindowState.Maximized;
        MaximizeButton.Content = maximized ? "\xE923" : "\xE922";
        MaximizeButton.ToolTip = maximized ? "Restore down" : "Maximize";
        // A maximized WindowChrome window extends past the screen by the resize border; keep content visible
        RootGrid.Margin = maximized ? new Thickness(7) : new Thickness(0);
    }

    private const int WM_NCHITTEST = 0x0084;
    private const int WM_NCMOUSEMOVE = 0x00A0;
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int WM_NCLBUTTONUP = 0x00A2;
    private const int WM_NCMOUSELEAVE = 0x02A2;
    private const int HTMAXBUTTON = 9;

    /// <summary>
    /// Reports the maximize button to Windows as the real maximize button (HTMAXBUTTON), so hovering it opens the
    /// Windows 11 Snap Layouts flyout. Because the button is then non-client area, hover and click are handled here.
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_NCHITTEST:
                if (IsOverMaximizeButton(lParam))
                {
                    handled = true;
                    return new IntPtr(HTMAXBUTTON);
                }
                break;
            case WM_NCMOUSEMOVE:
                SetMaximizeHover(wParam.ToInt32() == HTMAXBUTTON);
                break;
            case WM_NCMOUSELEAVE:
                SetMaximizeHover(false);
                break;
            case WM_NCLBUTTONDOWN:
                if (wParam.ToInt32() == HTMAXBUTTON) handled = true;
                break;
            case WM_NCLBUTTONUP:
                if (wParam.ToInt32() == HTMAXBUTTON)
                {
                    handled = true;
                    SetMaximizeHover(false);
                    MaximizeRestore_Click(this, new RoutedEventArgs());
                }
                break;
        }
        return IntPtr.Zero;
    }

    private bool IsOverMaximizeButton(IntPtr lParam)
    {
        if (MaximizeButton == null || !MaximizeButton.IsVisible) return false;
        int value = unchecked((int)lParam.ToInt64());
        var screenPoint = new Point((short)(value & 0xFFFF), (short)((value >> 16) & 0xFFFF));
        try
        {
            var local = MaximizeButton.PointFromScreen(screenPoint);
            return local.X >= 0 && local.Y >= 0 && local.X < MaximizeButton.ActualWidth && local.Y < MaximizeButton.ActualHeight;
        }
        catch (InvalidOperationException)
        {
            return false; // not yet connected to a presentation source
        }
    }

    private void SetMaximizeHover(bool hover)
    {
        if (hover) MaximizeButton.SetResourceReference(BackgroundProperty, "ControlHover");
        else MaximizeButton.ClearValue(BackgroundProperty);
    }

    private void AddNewStep()
    {
        if (_currentTask != null && !string.IsNullOrWhiteSpace(NewStepTextBox.Text))
        {
            var newStep = new TicketStep { Description = NewStepTextBox.Text.Trim() };
            _editingSteps.Add(newStep);
            StepsListView.Items.Refresh();
            StepsListView.ScrollIntoView(newStep);
            NewStepTextBox.Text = string.Empty;
            NewStepTextBox.Focus();
            UpdateStepsHeader();
        }
    }

    private void AddNewStep_Click(object sender, RoutedEventArgs e)
    {
        AddNewStep();
    }

    private void NewStepTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddNewStep();
            e.Handled = true;
        }
    }


    private void StepCheckBox_Click(object sender, RoutedEventArgs e)
    {
        // The binding updates _editingSteps; changes are saved with the task
        UpdateStepsHeader();
    }

    private void RemoveStepButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is TicketStep step)
        {
            _editingSteps.Remove(step);
            UpdateStepsHeader();
        }
    }

    private Point _stepsDragStartPoint;
    private TicketStep? _draggedStep;

    private void StepsListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _stepsDragStartPoint = e.GetPosition(null);
        
        var frameworkElement = e.OriginalSource as FrameworkElement;
        var border = frameworkElement as Border ?? FindVisualParent<Border>(frameworkElement);
        
        if (border != null && border.Cursor == Cursors.SizeAll && border.DataContext is TicketStep step)
        {
            _draggedStep = step;
        }
        else
        {
            _draggedStep = null;
        }
    }

    private void StepsListView_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && _draggedStep != null)
        {
            Point position = e.GetPosition(null);
            if (Math.Abs(position.X - _stepsDragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(position.Y - _stepsDragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                var listView = sender as ListView;
                if (listView != null)
                {
                    DragDrop.DoDragDrop(listView, _draggedStep, DragDropEffects.Move);
                    _draggedStep = null;
                }
            }
        }
    }

    private void StepsListView_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(TicketStep)))
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void StepsListView_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(TicketStep)))
        {
            var droppedStep = e.Data.GetData(typeof(TicketStep)) as TicketStep;
            if (droppedStep == null || _currentTask == null) return;

            var targetElement = e.OriginalSource as FrameworkElement;
            TicketStep? targetStep = targetElement?.DataContext as TicketStep;

            int removeIdx = _editingSteps.IndexOf(droppedStep);
            int targetIdx = targetStep != null ? _editingSteps.IndexOf(targetStep) : _editingSteps.Count - 1;

            if (removeIdx != -1 && targetIdx != -1 && removeIdx != targetIdx)
            {
                _editingSteps.RemoveAt(removeIdx);
                _editingSteps.Insert(targetIdx, droppedStep);
            }
        }
    }
}
