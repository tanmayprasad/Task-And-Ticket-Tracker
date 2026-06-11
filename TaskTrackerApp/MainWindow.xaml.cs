using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TaskTrackerApp.Data;
using TaskTrackerApp.Models;

namespace TaskTrackerApp;

public partial class MainWindow : Window
{
    private readonly TaskRepository _repository;
    private List<TaskModel> _tasks;
    private TaskModel? _currentTask;
    private System.Collections.ObjectModel.ObservableCollection<TicketStep> _editingSteps = new();
    private WidgetView? _widgetView;
    private ContextAwareEngine _contextEngine;
    private NotificationService _notificationService;
    private SettingsManager _settingsManager;

    // For drag and drop
    private DataGridRow? _draggedRow;

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, UIntPtr minimumWorkingSetSize, UIntPtr maximumWorkingSetSize);

    public MainWindow()
    {
        InitializeComponent();
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
        Application.Current.Shutdown();
    }

    private void MenuItemShow_Click(object sender, RoutedEventArgs e)
    {
        RestoreFromTray();
    }

    private void MenuItemCenterWidget_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsManager.LoadSettings();
        settings.WidgetLeft = null;
        settings.WidgetTop = null;
        _settingsManager.SaveSettings(settings);

        if (_widgetView != null && _widgetView.IsVisible)
        {
            _widgetView.CenterWindow();
        }
    }

    private void MenuItemExit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsManager.LoadSettings();
        NotificationsToggle.IsChecked = settings.NotificationsEnabled;
        AlertTimerTextBox.Text = settings.AlertTimerHours.ToString();
        MaxActiveTasksTextBox.Text = settings.MaxActiveTasks.ToString();
        WidgetTextSizeTextBox.Text = settings.WidgetTextSize.ToString();
        WidgetTextBoldToggle.IsChecked = settings.WidgetTextBold;
        WidgetOpacitySlider.Value = settings.WidgetOpacity;
        
        switch (settings.Theme)
        {
            case "Light": ThemeComboBox.SelectedIndex = 2; break;
            case "Dark": ThemeComboBox.SelectedIndex = 1; break;
            default: ThemeComboBox.SelectedIndex = 0; break;
        }

        switch (settings.WidgetTheme)
        {
            case "Light": WidgetThemeComboBox.SelectedIndex = 1; break;
            default: WidgetThemeComboBox.SelectedIndex = 0; break;
        }

        TasksTabContent.Visibility = Visibility.Collapsed;
        AboutTabContent.Visibility = Visibility.Collapsed;
        SettingsTabContent.Visibility = Visibility.Visible;
        
        // Uncheck sidebar navigation if they are toggle buttons
        if (NavTasks != null) NavTasks.IsChecked = false;
        if (NavAbout != null) NavAbout.IsChecked = false;
    }

    private void NumericOnly_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        e.Handled = new System.Text.RegularExpressions.Regex("[^0-9.]+").IsMatch(e.Text);
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        bool hasError = false;

        if (!int.TryParse(MaxActiveTasksTextBox.Text, out int maxTasks) || maxTasks <= 0)
        {
            if (MaxActiveTasksErrorText != null) MaxActiveTasksErrorText.Visibility = Visibility.Visible;
            hasError = true;
        }
        else
        {
            if (MaxActiveTasksErrorText != null) MaxActiveTasksErrorText.Visibility = Visibility.Collapsed;
        }

        if (!double.TryParse(AlertTimerTextBox.Text, out double hours) || hours < 0)
        {
            if (AlertTimerErrorText != null) AlertTimerErrorText.Visibility = Visibility.Visible;
            hasError = true;
        }
        else
        {
            if (AlertTimerErrorText != null) AlertTimerErrorText.Visibility = Visibility.Collapsed;
        }

        if (!int.TryParse(WidgetTextSizeTextBox.Text, out int textSize) || textSize < 8 || textSize > 72)
        {
            if (WidgetTextSizeErrorText != null) WidgetTextSizeErrorText.Visibility = Visibility.Visible;
            hasError = true;
        }
        else
        {
            if (WidgetTextSizeErrorText != null) WidgetTextSizeErrorText.Visibility = Visibility.Collapsed;
        }

        if (hasError) return;

        var settings = _settingsManager.LoadSettings();
        settings.NotificationsEnabled = NotificationsToggle.IsChecked ?? true;
        settings.AlertTimerHours = hours;
        settings.MaxActiveTasks = maxTasks;
        settings.WidgetTextSize = textSize;
        
        settings.WidgetTextBold = WidgetTextBoldToggle.IsChecked ?? true;
        settings.WidgetOpacity = WidgetOpacitySlider.Value;
        
        var selectedTheme = (ThemeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (selectedTheme != null)
            settings.Theme = selectedTheme;

        var selectedWidgetTheme = (WidgetThemeComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (selectedWidgetTheme != null)
            settings.WidgetTheme = selectedWidgetTheme;

        App.ApplyTheme(settings.Theme);

        _settingsManager.SaveSettings(settings);
        
        if (_widgetView != null)
        {
            _widgetView.ApplySettings(settings);
        }
        SettingsTabContent.Visibility = Visibility.Collapsed;
        if (NavTasks != null) NavTasks.IsChecked = true;
        else TasksTabContent.Visibility = Visibility.Visible;
    }

    private void CancelSettings_Click(object sender, RoutedEventArgs e)
    {
        SettingsTabContent.Visibility = Visibility.Collapsed;
        if (NavTasks != null) NavTasks.IsChecked = true;
        else TasksTabContent.Visibility = Visibility.Visible;
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsManager.LoadSettings();
        if (settings.Theme == "Dark")
        {
            settings.Theme = "Light";
            settings.WidgetTheme = "Light";
            if (ThemeIconTextBlock != null) ThemeIconTextBlock.Text = "\xE708"; // Sun icon
        }
        else
        {
            settings.Theme = "Dark";
            settings.WidgetTheme = "Dark";
            if (ThemeIconTextBlock != null) ThemeIconTextBlock.Text = "\xE706"; // Moon icon
        }
        _settingsManager.SaveSettings(settings);
        App.ApplyTheme(settings.Theme);
        if (_widgetView != null)
        {
            _widgetView.ApplySettings(settings);
        }

        // Sync ComboBoxes if Settings is open
        if (ThemeComboBox != null)
        {
            switch (settings.Theme)
            {
                case "Light": ThemeComboBox.SelectedIndex = 2; break;
                case "Dark": ThemeComboBox.SelectedIndex = 1; break;
                default: ThemeComboBox.SelectedIndex = 0; break;
            }
        }
        if (WidgetThemeComboBox != null)
        {
            switch (settings.WidgetTheme)
            {
                case "Light": WidgetThemeComboBox.SelectedIndex = 1; break;
                default: WidgetThemeComboBox.SelectedIndex = 0; break;
            }
        }
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WidgetThemeComboBox != null && ThemeComboBox.SelectedItem is ComboBoxItem item)
        {
            var selectedTheme = item.Content.ToString();
            // Match widget theme unless user changes it later (which they can by using WidgetThemeComboBox directly)
            if (selectedTheme == "Dark")
            {
                WidgetThemeComboBox.SelectedIndex = 0; // Dark
            }
            else if (selectedTheme == "Light")
            {
                WidgetThemeComboBox.SelectedIndex = 1; // Light
            }
            else if (selectedTheme == "System")
            {
                // We'll leave the widget theme alone or default to dark
                WidgetThemeComboBox.SelectedIndex = 0; // Dark
            }
        }
    }

    private void LoadTasks()
    {
        // Load and sort by priority
        _tasks = _repository.LoadTasks().OrderBy(t => t.Priority).ToList();
        
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
        if (FilterPlaceholder != null)
        {
            FilterPlaceholder.Visibility = string.IsNullOrEmpty(FilterVstsTextBox.Text) ? Visibility.Visible : Visibility.Hidden;
        }
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
            TaskDetailsGrid.Margin = new Thickness(50, 10, 50, 20);

            MaximizeDetailsButton.Content = "\xE73F";
            MaximizeDetailsButton.ToolTip = "Restore Panel";

            DescriptionTextBox.MinHeight = 150;
            DescriptionTextBox.Height = double.NaN;
            AcTextBox.MinHeight = 150;
            AcTextBox.Height = double.NaN;

            // Apply Split Layout
            DetailsRightColumn.Width = new GridLength(35, GridUnitType.Star);
            DetailsFormGrid.ColumnDefinitions[0].Width = new GridLength(65, GridUnitType.Star);
            RightColumnStack.Visibility = Visibility.Visible;
            ContentStack.Children.Remove(MetadataSection1);
            ContentStack.Children.Remove(MetadataSection2);
            RightColumnStack.Children.Add(MetadataSection1);
            RightColumnStack.Children.Add(MetadataSection2);
        }
        else
        {
            TaskListColumn.Width = new GridLength(1, GridUnitType.Star);
            TasksDividerColumn.Width = GridLength.Auto;
            TaskDetailsColumn.Width = GridLength.Auto;
            TaskDetailsGrid.Width = 350;
            TaskDetailsGrid.Margin = new Thickness(30, 10, 30, 20);

            MaximizeDetailsButton.Content = "\xE740";
            MaximizeDetailsButton.ToolTip = "Toggle Full Screen";

            DescriptionTextBox.Height = 100;
            DescriptionTextBox.MinHeight = 0;
            AcTextBox.Height = 100;
            AcTextBox.MinHeight = 0;

            // Restore Layout
            DetailsRightColumn.Width = new GridLength(0);
            DetailsFormGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            RightColumnStack.Visibility = Visibility.Collapsed;
            RightColumnStack.Children.Clear();
            if (!ContentStack.Children.Contains(MetadataSection1))
            {
                ContentStack.Children.Insert(0, MetadataSection1);
                ContentStack.Children.Insert(3, MetadataSection2);
            }
        }
    }

    private void NewTaskButton_Click(object sender, RoutedEventArgs e)
    {
        _currentTask = new TaskModel();
        TasksDataGrid.SelectedItem = null;
        PopulateForm(_currentTask);
        DetailsPanel.Visibility = Visibility.Visible;
        ButtonPanel.Visibility = Visibility.Visible;
        ApplyMaximizeState();
    }

    private void TasksDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TasksDataGrid.SelectedItem is TaskModel selected)
        {
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
        VstsTextBox.Text = task.VstsNumber;
        TitleTextBox.Text = task.Title;
        DescriptionTextBox.Text = task.Description;
        AcTextBox.Text = task.AcceptanceCriteria;
        
        if (task.TargetDate.HasValue)
        {
            TargetDatePicker.SelectedDate = task.TargetDate.Value.Date;
            
            var hourStr = task.TargetDate.Value.ToString("HH");
            foreach (ComboBoxItem item in HourComboBox.Items)
                if (item.Content.ToString() == hourStr) HourComboBox.SelectedItem = item;

            var minStr = task.TargetDate.Value.ToString("mm");
            foreach (ComboBoxItem item in MinuteComboBox.Items)
                if (item.Content.ToString() == minStr) MinuteComboBox.SelectedItem = item;
        }
        else
        {
            TargetDatePicker.SelectedDate = null;
            HourComboBox.SelectedIndex = 0;
            MinuteComboBox.SelectedIndex = 0;
        }
        
        foreach (ComboBoxItem item in StateComboBox.Items)
        {
            if ((item.Tag?.ToString() ?? item.Content.ToString()) == task.State)
            {
                StateComboBox.SelectedItem = item;
                break;
            }
        }
        
        PriorityTextBox.Text = task.Priority.ToString();
        
        if (task.Steps == null)
            task.Steps = new System.Collections.ObjectModel.ObservableCollection<TicketStep>();
            
        _editingSteps = new System.Collections.ObjectModel.ObservableCollection<TicketStep>(
            task.Steps.Select(s => new TicketStep { IsDone = s.IsDone, Description = s.Description })
        );
        StepsListView.ItemsSource = _editingSteps;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentTask == null) return;

        bool hasError = false;

        if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
        {
            if (TitleErrorText != null) TitleErrorText.Visibility = Visibility.Visible;
            hasError = true;
        }
        else
        {
            if (TitleErrorText != null) TitleErrorText.Visibility = Visibility.Collapsed;
        }

        if (string.IsNullOrWhiteSpace(VstsTextBox.Text))
        {
            int maxI = 0;
            foreach (var t in _tasks)
            {
                if (!string.IsNullOrEmpty(t.VstsNumber) && t.VstsNumber.StartsWith("i", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(t.VstsNumber.Substring(1), out int num))
                    {
                        if (num > maxI) maxI = num;
                    }
                }
            }
            VstsTextBox.Text = $"i{maxI + 1}";
        }

        if (_editingSteps.Any(s => string.IsNullOrWhiteSpace(s.Description)))
        {
            if (StepsErrorText != null) StepsErrorText.Visibility = Visibility.Visible;
            hasError = true;
        }
        else
        {
            if (StepsErrorText != null) StepsErrorText.Visibility = Visibility.Collapsed;
        }

        if (hasError) return;

        _currentTask.VstsNumber = VstsTextBox.Text;
        _currentTask.Title = TitleTextBox.Text;
        _currentTask.Description = DescriptionTextBox.Text;
        _currentTask.AcceptanceCriteria = AcTextBox.Text;
        
        _currentTask.Steps = new System.Collections.ObjectModel.ObservableCollection<TicketStep>(
            _editingSteps.Select(s => new TicketStep { IsDone = s.IsDone, Description = s.Description })
        );
        
        if (TargetDatePicker.SelectedDate.HasValue)
        {
            var date = TargetDatePicker.SelectedDate.Value.Date;
            int hour = 0;
            int min = 0;
            if (HourComboBox.SelectedItem is ComboBoxItem hItem) int.TryParse(hItem.Content.ToString(), out hour);
            if (MinuteComboBox.SelectedItem is ComboBoxItem mItem) int.TryParse(mItem.Content.ToString(), out min);
            
            _currentTask.TargetDate = date.AddHours(hour).AddMinutes(min);
        }
        else
        {
            _currentTask.TargetDate = null;
        }
        
        if (StateComboBox.SelectedItem is ComboBoxItem stateItem)
        {
            _currentTask.State = stateItem.Tag?.ToString() ?? stateItem.Content.ToString() ?? "To Do";
        }
        
        if (_currentTask.State != "In progress")
        {
            _currentTask.IsActive = false;
        }

        if (!_tasks.Any(t => t.Id == _currentTask.Id))
        {
            // Give it the lowest priority (bottom of list) initially
            _currentTask.Priority = _tasks.Count > 0 ? _tasks.Max(t => t.Priority) + 1 : 1;
            _tasks.Add(_currentTask);
        }

        SaveAndRefresh();
        CloseDetailsPanel();
    }
        
    private void TargetDateGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        TargetDatePicker.IsDropDownOpen = true;
        e.Handled = true;
    }
        
    private void CloseDetailsPanel()
    {
        DetailsPanel.Visibility = Visibility.Collapsed;
        ButtonPanel.Visibility = Visibility.Collapsed;
        TasksDataGrid.SelectedItem = null;
        _currentTask = null;
        ApplyMaximizeState();
    }

    private void ViewDocs_Click(object sender, RoutedEventArgs e)
    {
        var docWindow = new DocumentationWindow();
        docWindow.Owner = this;
        docWindow.ShowDialog();
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
            MessageBox.Show($"You can only have up to {settings.MaxActiveTasks} active tasks at a time.", "Limit Reached", MessageBoxButton.OK, MessageBoxImage.Warning);
            
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
        
        var result = MessageBox.Show($"Are you sure you want to delete '{_currentTask.Title}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            _tasks.RemoveAll(t => t.Id == _currentTask.Id);
            CloseDetailsPanel();
            SaveAndRefresh();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CloseDetailsPanel();
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

        _repository.SaveTasks(_tasks);
        RefreshGrid();
        
        _notificationService?.UpdateTasks(_tasks);
        UpdateWidgetIfActive();
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (this.WindowState == WindowState.Minimized)
        {
            this.Hide();
            
            if (_widgetView == null)
            {
                _widgetView = new WidgetView(this);
                _widgetView.OnSkipRequested += WidgetView_OnSkipRequested;
                _widgetView.OnResetRequested += WidgetView_OnResetRequested;
                _widgetView.OnTaskRequested += WidgetView_OnTaskRequested;
            }
            
            _widgetView.ApplySettings(_settingsManager.LoadSettings());
            _widgetView.SetActiveTasks(_tasks.Where(t => t.IsActive).ToList());
            _widgetView.Show();
        }
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
        
        _repository.SaveTasks(_tasks);
        TasksDataGrid.Items.Refresh();
        
        if (_widgetView != null)
        {
            _widgetView.SetActiveTasks(_tasks.Where(t => t.IsActive).ToList());
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
        
        _repository.SaveTasks(_tasks);
        TasksDataGrid.Items.Refresh();
        
        if (_widgetView != null)
        {
            _widgetView.SetActiveTasks(_tasks.Where(t => t.IsActive).ToList());
        }
    }

    public void RestoreFromTray()
    {
        this.Show();
        this.WindowState = WindowState.Normal;
        this.Activate();
        
        if (_widgetView != null)
        {
            _widgetView.Hide();
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
        if (_widgetView != null && _widgetView.IsVisible)
        {
            _widgetView.SetActiveTasks(_tasks.Where(t => t.IsActive).ToList());
        }
    }

    // --- Drag and Drop Logic ---

    private void TasksDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Find the DataGridRow
        var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row != null && !row.IsEditing)
        {
            _draggedRow = row;
        }
    }

    private void TasksDataGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && _draggedRow != null)
        {
            DragDrop.DoDragDrop(_draggedRow, _draggedRow.Item, DragDropEffects.Move);
            _draggedRow = null; // Reset after drop starts
        }
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
        
        if (NavTasks?.IsChecked == true)
        {
            TasksTabContent.Visibility = Visibility.Visible;
            AboutTabContent.Visibility = Visibility.Collapsed;
            SettingsTabContent.Visibility = Visibility.Collapsed;
        }
        else if (NavAbout?.IsChecked == true)
        {
            TasksTabContent.Visibility = Visibility.Collapsed;
            AboutTabContent.Visibility = Visibility.Visible;
            SettingsTabContent.Visibility = Visibility.Collapsed;
        }
    }

    private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
    {
        if (SidebarColumnDef.Width.Value == 200)
        {
            SidebarColumnDef.Width = new GridLength(60);
        }
        else
        {
            SidebarColumnDef.Width = new GridLength(200);
        }
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
        // Just let the binding update the _editingSteps, no need to save immediately.
    }

    private void RemoveStepButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is TicketStep step)
        {
            _editingSteps.Remove(step);
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

public class LessThanConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is double actual && double.TryParse(parameter?.ToString(), out double limit))
        {
            return actual < limit;
        }
        return false;
    }

    public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new System.NotImplementedException();
    }
}
