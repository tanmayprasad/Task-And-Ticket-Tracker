using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TaskTrackerApp.Data;
using TaskTrackerApp.Models;

namespace TaskTrackerApp;

public partial class MainWindow : Window
{
    private readonly TaskRepository _repository;
    private List<TaskModel> _tasks;
    private TaskModel? _currentTask;
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
        
        // Setup System Tray icon image
        MyNotifyIcon.Icon = System.Drawing.SystemIcons.Information;

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

    private void MenuItemTest_Click(object sender, RoutedEventArgs e)
    {
        var active = _tasks.FirstOrDefault(t => t.IsActive);
        _notificationService.ShowTaskReminder("Task Tracker", active?.Title ?? "No active task");
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
        
        foreach (ComboBoxItem item in ThemeComboBox.Items)
        {
            if (item.Content.ToString() == settings.Theme)
            {
                ThemeComboBox.SelectedItem = item;
                break;
            }
        }
        
        SettingsOverlay.Visibility = Visibility.Visible;
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsManager.LoadSettings();
        settings.NotificationsEnabled = NotificationsToggle.IsChecked ?? true;
        if (double.TryParse(AlertTimerTextBox.Text, out double hours))
        {
            settings.AlertTimerHours = hours;
        }
        if (int.TryParse(MaxActiveTasksTextBox.Text, out int maxActive))
        {
            settings.MaxActiveTasks = maxActive;
        }
        if (int.TryParse(WidgetTextSizeTextBox.Text, out int size))
        {
            settings.WidgetTextSize = size;
        }
        settings.WidgetTextBold = WidgetTextBoldToggle.IsChecked ?? true;
        
        if (ThemeComboBox.SelectedItem is ComboBoxItem themeItem)
        {
            settings.Theme = themeItem.Content.ToString() ?? "Dark";
            App.ApplyTheme(settings.Theme);
        }
        
        _settingsManager.SaveSettings(settings);
        
        if (_widgetView != null)
        {
            _widgetView.ApplySettings(settings);
        }
        SettingsOverlay.Visibility = Visibility.Collapsed;
    }

    private void CancelSettings_Click(object sender, RoutedEventArgs e)
    {
        SettingsOverlay.Visibility = Visibility.Collapsed;
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsManager.LoadSettings();
        settings.Theme = settings.Theme == "Dark" ? "Light" : "Dark";
        App.ApplyTheme(settings.Theme);
        _settingsManager.SaveSettings(settings);
        
        ThemeToggleButton.Content = settings.Theme == "Dark" ? "\uE706" : "\uE708"; // Sun/Moon
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

        TasksDataGrid.ItemsSource = null;
        TasksDataGrid.ItemsSource = filtered.ToList();
    }

    private void NewTaskButton_Click(object sender, RoutedEventArgs e)
    {
        _currentTask = new TaskModel();
        TasksDataGrid.SelectedItem = null;
        PopulateForm(_currentTask);
        DetailsPanel.Visibility = Visibility.Visible;
        ButtonPanel.Visibility = Visibility.Visible;
    }

    private void TasksDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TasksDataGrid.SelectedItem is TaskModel selected)
        {
            _currentTask = selected;
            PopulateForm(selected);
            DetailsPanel.Visibility = Visibility.Visible;
            ButtonPanel.Visibility = Visibility.Visible;
        }
        else
        {
            DetailsPanel.Visibility = Visibility.Collapsed;
            ButtonPanel.Visibility = Visibility.Collapsed;
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
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentTask == null) return;

        if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
        {
            MessageBox.Show("Title is a mandatory field.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
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

        _currentTask.VstsNumber = VstsTextBox.Text;
        _currentTask.Title = TitleTextBox.Text;
        _currentTask.Description = DescriptionTextBox.Text;
        _currentTask.AcceptanceCriteria = AcTextBox.Text;
        
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
        
        // Close task details on save
        DetailsPanel.Visibility = Visibility.Collapsed;
        ButtonPanel.Visibility = Visibility.Collapsed;
        TasksDataGrid.SelectedItem = null;
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
            _currentTask = null;
            DetailsPanel.Visibility = Visibility.Collapsed;
            ButtonPanel.Visibility = Visibility.Collapsed;
            TasksDataGrid.SelectedItem = null;
            SaveAndRefresh();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DetailsPanel.Visibility = Visibility.Collapsed;
        ButtonPanel.Visibility = Visibility.Collapsed;
        TasksDataGrid.SelectedItem = null;
        _currentTask = null;
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

    private void SaveAndRefresh()
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
            }
            
            _widgetView.ApplySettings(_settingsManager.LoadSettings());
            _widgetView.SetActiveTasks(_tasks.Where(t => t.IsActive).ToList());
            _widgetView.Show();
        }
    }

    private void WidgetView_OnSkipRequested(object? sender, TaskModel currentTask)
    {
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
        DependencyObject? parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);

        if (parentObject == null) return null;

        if (parentObject is T parent)
            return parent;
        else
            return FindVisualParent<T>(parentObject);
    }

    public void MarkTaskDone(TaskModel task)
    {
        task.State = "Done";
        task.IsActive = false;
        
        SaveAndRefresh();
    }

    private void Nav_Changed(object sender, RoutedEventArgs e)
    {
        if (TasksTabContent == null || AboutTabContent == null) return;
        
        if (NavTasks?.IsChecked == true)
        {
            TasksTabContent.Visibility = Visibility.Visible;
            AboutTabContent.Visibility = Visibility.Collapsed;
        }
        else if (NavAbout?.IsChecked == true)
        {
            TasksTabContent.Visibility = Visibility.Collapsed;
            AboutTabContent.Visibility = Visibility.Visible;
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
