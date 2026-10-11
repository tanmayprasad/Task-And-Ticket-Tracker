using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using TaskTrackerApp.Data;
using TaskTrackerApp.Dialogs;
using TaskTrackerApp.Models;

namespace TaskTrackerApp;

/// <summary>
/// Profiles (each with its own tasks, active-task limit and reminders) and per-profile import / export.
/// The open profile's tasks are <c>_tasks</c>; the other profiles' tasks are kept loaded for reminders and counts.
/// </summary>
public partial class MainWindow
{
    private readonly ProfileStore _profiles;
    private readonly Dictionary<Guid, List<TaskModel>> _otherProfileTasks = new();

    // ------------------------------------------------------------------ State

    /// <summary>Loads every profile except the open one, so reminders cover all profiles and Settings can show counts.</summary>
    private void LoadOtherProfiles()
    {
        _otherProfileTasks.Clear();
        foreach (var profile in _profiles.Profiles)
        {
            if (profile.Id == _profiles.Active.Id)
            {
                _notificationService.UpdateProfile(profile, _tasks);
                continue;
            }
            var tasks = _profiles.RepositoryFor(profile).LoadTasks();
            _otherProfileTasks[profile.Id] = tasks;
            _notificationService.UpdateProfile(profile, tasks);
        }
        _notificationService.KeepProfiles(_profiles.Profiles.Select(p => p.Id));
    }

    private List<TaskModel> TasksOf(ProfileModel profile) =>
        profile.Id == _profiles.Active.Id ? _tasks
        : _otherProfileTasks.TryGetValue(profile.Id, out var tasks) ? tasks
        : _profiles.RepositoryFor(profile).LoadTasks();

    /// <summary>The navigation item, tray tooltip and the Profiles page reflect the open profile.</summary>
    private void UpdateProfileUi()
    {
        var name = _profiles.Active.Name;
        ProfileNameText.Text = name;
        ProfileInitialText.Text = InitialOf(name);
        NavProfiles.ToolTip = $"Profiles · {name}";
        MyNotifyIcon.ToolTipText = $"Task And Ticket Tracker — {name}";
        if (ProfilesTabContent.Visibility == Visibility.Visible) BuildProfilesPage();
    }

    private static string InitialOf(string name) =>
        name.Trim().Length > 0 ? char.ToUpperInvariant(name.Trim()[0]).ToString() : "?";

    /// <summary>Opens the Profiles page (e.g. from "Change limit" or Settings → Manage profiles).</summary>
    private void OpenProfilesPage()
    {
        if (NavProfiles.IsChecked == true) BuildProfilesPage();
        else NavProfiles.IsChecked = true; // Nav_Changed shows the page and fills it
    }

    private void ManageProfiles_Click(object sender, RoutedEventArgs e) => OpenProfilesPage();

    private void UpdateProfileSetting(Action<ProfileModel> change)
    {
        if (_loadingSettings) return;
        _profiles.Update(_profiles.Active.Id, change);
    }

    // ------------------------------------------------------------------ Switch / create / rename / delete

    internal void SwitchProfile(Guid id)
    {
        var target = _profiles.Find(id);
        if (target == null || id == _profiles.Active.Id) return;

        RunAfterUnsavedCheck(() =>
        {
            CloseDetailsPanel();
            _profiles.SetActive(id);
            _repository = _profiles.RepositoryFor(target);
            LoadTasks();
            LoadOtherProfiles();
            UpdateProfileUi();
            if (SettingsTabContent.Visibility == Visibility.Visible) LoadSettingsIntoControls();
            UpdateWidgetIfActive();
            ShowSnackbar($"Switched to “{target.Name}”.");
        });
    }

    private void NewProfile()
    {
        var name = TextPromptDialog.Show(this, "New profile",
            "Each profile has its own tasks, active-task limit and reminders. Its settings start as a copy of your newest profile.",
            "Create", string.Empty, ProfileStore.MaxNameLength, n => _profiles.ValidateName(n, except: null));
        if (name == null) return;

        var profile = _profiles.Create(name);
        LoadOtherProfiles();
        SwitchProfile(profile.Id);
    }

    private void RenameProfile(ProfileModel profile)
    {
        var name = TextPromptDialog.Show(this, "Rename profile", $"A new name for “{profile.Name}”.", "Rename", profile.Name,
            ProfileStore.MaxNameLength, n => _profiles.ValidateName(n, except: profile.Id));
        if (name == null || name == profile.Name) return;
        _profiles.Rename(profile.Id, name);
        UpdateProfileUi();
    }

    private void DeleteProfile(ProfileModel profile)
    {
        if (_profiles.Profiles.Count == 1) return;

        int count = TasksOf(profile).Count;
        int choice = ChoiceDialog.Show(this, $"Delete “{profile.Name}”?",
            $"Its {count} {(count == 1 ? "task" : "tasks")} will be removed from the app. The task file is kept in the app's data folder (profiles\\.deleted), " +
            "but the easiest way to keep a copy you can import later is to export the profile first.",
            cancelIndex: 2, new Choice("Export first"), new Choice("Delete", ChoiceKind.Danger), new Choice("Cancel"));
        if (choice == 0) { ExportTasks(profile); return; }
        if (choice != 1) return;

        bool wasActive = profile.Id == _profiles.Active.Id;
        void Delete()
        {
            if (wasActive) CloseDetailsPanel();
            try
            {
                _profiles.Delete(profile.Id);
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
            {
                ShowError("Couldn't delete the profile", ex.Message);
                return;
            }
            if (wasActive)
            {
                _repository = _profiles.RepositoryFor(_profiles.Active);
                LoadTasks();
                UpdateWidgetIfActive();
            }
            LoadOtherProfiles();
            UpdateProfileUi();
            ShowSnackbar(wasActive ? $"Deleted “{profile.Name}”. Switched to “{_profiles.Active.Name}”." : $"Deleted “{profile.Name}”.");
        }

        if (wasActive) RunAfterUnsavedCheck(Delete);
        else Delete();
    }

    // ------------------------------------------------------------------ Import / export

    /// <param name="asCsv">Preselects CSV (spreadsheet) instead of JSON in the save dialog.</param>
    private void ExportTasks(ProfileModel profile, bool asCsv = false)
    {
        var tasks = TasksOf(profile);
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = $"Export tasks from “{profile.Name}”",
            Filter = "Task file (*.json)|*.json|Spreadsheet (*.csv)|*.csv",
            FilterIndex = asCsv ? 2 : 1,
            DefaultExt = asCsv ? ".csv" : ".json",
            FileName = TaskTransfer.SuggestedFileName(profile.Name, DateTime.Today, asCsv ? ".csv" : ".json"),
            AddExtension = true,
        };
        if (dialog.ShowDialog(this) != true) return;

        var path = dialog.FileName;
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        bool csv = ext == ".csv" || (ext != ".json" && dialog.FilterIndex == 2);
        try
        {
            if (csv) System.IO.File.WriteAllText(path, TaskTransfer.ExportCsv(tasks), new UTF8Encoding(true)); // BOM: Excel reads UTF-8
            else System.IO.File.WriteAllText(path, TaskTransfer.ExportJson(tasks, profile.Name, DateTime.Now), new UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            ShowError("Export failed", ex.Message);
            return;
        }

        ShowSnackbar($"Exported {tasks.Count} {(tasks.Count == 1 ? "task" : "tasks")} from “{profile.Name}”.", "Show file",
            () => System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\""));
    }

    private void ImportTasks(ProfileModel profile)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = $"Import tasks into “{profile.Name}”",
            Filter = "Task files (*.json; *.csv)|*.json;*.csv|Task file (*.json)|*.json|Spreadsheet (*.csv)|*.csv",
        };
        if (dialog.ShowDialog(this) != true) return;

        ImportFile file;
        try
        {
            file = TaskTransfer.Import(System.IO.File.ReadAllText(dialog.FileName), System.IO.Path.GetExtension(dialog.FileName));
        }
        catch (Exception ex) when (ex is FormatException or System.IO.IOException or UnauthorizedAccessException)
        {
            ShowError("Couldn't import this file", ex.Message);
            return;
        }
        if (file.Tasks.Count == 0)
        {
            ShowError("No tasks to import", "The file doesn't contain any tasks." + Notes(file.Warnings));
            return;
        }

        var current = TasksOf(profile);
        int n = file.Tasks.Count;
        string title = $"Import {n} {(n == 1 ? "task" : "tasks")} into “{profile.Name}”?";
        int choice;
        if (current.Count == 0)
        {
            // Nothing to replace in an empty profile: adding and replacing are the same thing
            choice = ChoiceDialog.Show(this, title, "This profile has no tasks yet. You can undo this." + Notes(file.Warnings),
                cancelIndex: 1, new Choice("Import", ChoiceKind.Primary), new Choice("Cancel"));
            if (choice != 0) return;
        }
        else
        {
            choice = ChoiceDialog.Show(this, title,
                $"Add them to the {current.Count} {(current.Count == 1 ? "task" : "tasks")} already there: a task whose ticket number you already have " +
                "is updated from the file instead of added twice. Or replace the whole list. You can undo this." + Notes(file.Warnings),
                cancelIndex: 2, new Choice("Add to list", ChoiceKind.Primary), new Choice("Replace list", ChoiceKind.Danger), new Choice("Cancel"));
            if (choice is not (0 or 1)) return;
        }

        var before = Clone(current);
        // Ids used by other profiles get a new Id, so a task never exists twice across profiles
        var usedIds = _profiles.Profiles.Where(p => p.Id != profile.Id).SelectMany(p => TasksOf(p).Select(t => t.Id)).ToHashSet();
        var merge = TaskTransfer.Merge(choice == 0 ? current : new List<TaskModel>(), file.Tasks, usedIds);
        TaskTransfer.CapActive(merge.Tasks, profile.MaxActiveTasks);

        if (!ReplaceTasks(profile, merge.Tasks)) return;
        var parts = new List<string> { $"{merge.Added} added" };
        if (merge.Updated > 0) parts.Add($"{merge.Updated} updated");
        if (merge.Unchanged > 0) parts.Add($"{merge.Unchanged} unchanged");
        if (merge.Skipped > 0) parts.Add($"{merge.Skipped} skipped (repeated ticket)");
        ShowSnackbar($"Imported into “{profile.Name}”: {string.Join(" · ", parts)}.", "Undo",
            () => ReplaceTasks(profile, before), seconds: 10);
    }

    /// <summary>Replaces a profile's whole task list (open profile: through the normal save path).</summary>
    private bool ReplaceTasks(ProfileModel profile, List<TaskModel> tasks)
    {
        if (profile.Id == _profiles.Active.Id)
        {
            CloseDetailsPanel();
            _tasks = tasks;
            SaveAndRefresh();
            return true;
        }

        for (int i = 0; i < tasks.Count; i++) tasks[i].Priority = i + 1;
        try
        {
            _profiles.RepositoryFor(profile).SaveTasks(tasks);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            ShowError("Couldn't save the tasks", ex.Message);
            return false;
        }
        LoadOtherProfiles();
        if (ProfilesTabContent.Visibility == Visibility.Visible) BuildProfilesPage();
        return true;
    }

    private static List<TaskModel> Clone(List<TaskModel> tasks) =>
        JsonSerializer.Deserialize<List<TaskModel>>(JsonSerializer.Serialize(tasks)) ?? new List<TaskModel>();

    private static string Notes(List<string> warnings) =>
        warnings.Count == 0 ? string.Empty
        : "\n\nNote: " + string.Join(" ", warnings.Take(3)) + (warnings.Count > 3 ? $" (and {warnings.Count - 3} more)" : string.Empty);

    private void ShowError(string heading, string message) =>
        ChoiceDialog.Show(this, heading, message, cancelIndex: 0, new Choice("OK", ChoiceKind.Primary));

    // ------------------------------------------------------------------ Profiles page

    private void RenameCurrentProfile_Click(object sender, RoutedEventArgs e) => RenameProfile(_profiles.Active);
    private void ExportJson_Click(object sender, RoutedEventArgs e) => ExportTasks(_profiles.Active, asCsv: false);
    private void ExportCsv_Click(object sender, RoutedEventArgs e) => ExportTasks(_profiles.Active, asCsv: true);
    private void ImportCurrent_Click(object sender, RoutedEventArgs e) => ImportTasks(_profiles.Active);

    private void CurrentProfileMore_Click(object sender, RoutedEventArgs e)
    {
        var profile = _profiles.Active;
        var menu = new ContextMenu { PlacementTarget = CurrentProfileMoreButton, Placement = PlacementMode.Bottom };
        menu.Items.Add(MenuItemFor("Delete profile…", () => DeleteProfile(profile), enabled: _profiles.Profiles.Count > 1));
        menu.IsOpen = true;
    }

    private static MenuItem MenuItemFor(string header, Action action, bool enabled = true)
    {
        var item = new MenuItem { Header = header, IsEnabled = enabled };
        item.Click += (s, e) => action();
        return item;
    }

    /// <summary>"22 tasks · 2 active · 1 overdue · 5 done" (parts that are zero are left out, except the task count).</summary>
    private static string StatsOf(List<TaskModel> tasks, bool detailed)
    {
        var now = DateTime.Now;
        int active = tasks.Count(t => t.IsActive);
        int overdue = tasks.Count(t => t.State != "Done" && t.TargetDate is DateTime d && d < now);
        int done = tasks.Count(t => t.State == "Done");
        var parts = new List<string> { $"{tasks.Count} {(tasks.Count == 1 ? "task" : "tasks")}" };
        if (active > 0) parts.Add($"{active} active");
        if (detailed && overdue > 0) parts.Add($"{overdue} overdue");
        if (detailed && done > 0) parts.Add($"{done} done");
        return string.Join(" · ", parts);
    }

    /// <summary>Fills the Profiles page: the open profile, a card per profile, and the import / export texts.</summary>
    private void BuildProfilesPage()
    {
        var current = _profiles.Active;
        CurrentProfileInitial.Text = InitialOf(current.Name);
        CurrentProfileName.Text = current.Name;
        CurrentProfileStats.Text = StatsOf(_tasks, detailed: true);
        ProfileSettingsCaption.Text = $"Focus and reminders for “{current.Name}”";
        ExportDescText.Text = $"Save all tasks from “{current.Name}” to a file. JSON keeps everything (backups, another PC); CSV opens in Excel.";
        ImportDescText.Text = $"Add tasks to “{current.Name}” from a JSON or CSV file. A task whose ticket number you already have is updated from the file. You can undo.";

        ProfileCards.Children.Clear();
        foreach (var profile in _profiles.Profiles.OrderBy(p => p.CreatedAt))
            ProfileCards.Children.Add(ProfileCard(profile));
        ProfileCards.Children.Add(NewProfileCard());
    }

    private const double CardWidth = 300, CardHeight = 112;

    private Border ProfileCard(ProfileModel profile)
    {
        bool open = profile.Id == _profiles.Active.Id;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Avatar: accent for the open profile, neutral for the others
        var initial = new TextBlock
        {
            Text = InitialOf(profile.Name),
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        initial.SetResourceReference(TextBlock.ForegroundProperty, open ? "TextOnAccent" : "TextPrimary");
        var avatar = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(18), VerticalAlignment = VerticalAlignment.Top, Child = initial };
        avatar.SetResourceReference(Border.BackgroundProperty, open ? "PrimaryBackground" : "ControlSelected");
        grid.Children.Add(avatar);

        var text = new StackPanel { Margin = new Thickness(14, 0, 0, 0) };
        var name = new TextBlock { Text = profile.Name, Style = (Style)FindResource("BodyStrongText"), TextTrimming = TextTrimming.CharacterEllipsis };
        text.Children.Add(name);
        text.Children.Add(new TextBlock { Text = StatsOf(TasksOf(profile), detailed: false), Style = (Style)FindResource("CaptionText"), Margin = new Thickness(0, 3, 0, 0) });
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        // Bottom row: Switch (or "Open now") and the ⋯ menu
        var actions = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        Grid.SetRow(actions, 1);
        Grid.SetColumnSpan(actions, 2);
        if (open)
        {
            var chipText = new TextBlock { Text = "Open now", FontSize = 12, FontWeight = FontWeights.SemiBold };
            chipText.SetResourceReference(TextBlock.ForegroundProperty, "SuccessColor");
            var chip = new Border
            {
                Style = (Style)FindResource("Chip"),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Child = chipText,
            };
            chip.SetResourceReference(Border.BackgroundProperty, "SuccessBackground");
            actions.Children.Add(chip);
        }
        else
        {
            var switchButton = new Button { Content = "Switch", Style = (Style)FindResource("PrimaryButton"), HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 88 };
            System.Windows.Automation.AutomationProperties.SetName(switchButton, $"Switch to {profile.Name}");
            switchButton.Click += (s, e) => SwitchProfile(profile.Id);
            actions.Children.Add(switchButton);
        }

        var more = new Button { Content = "\uE712", Style = (Style)FindResource("IconButton"), HorizontalAlignment = HorizontalAlignment.Right, ToolTip = "More options" };
        System.Windows.Automation.AutomationProperties.SetName(more, $"More options for {profile.Name}");
        var menu = new ContextMenu { PlacementTarget = more, Placement = PlacementMode.Bottom };
        menu.Items.Add(MenuItemFor("Rename…", () => RenameProfile(profile)));
        menu.Items.Add(MenuItemFor("Import tasks…", () => ImportTasks(profile)));
        menu.Items.Add(MenuItemFor("Export as JSON…", () => ExportTasks(profile, asCsv: false)));
        menu.Items.Add(MenuItemFor("Export as CSV…", () => ExportTasks(profile, asCsv: true)));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItemFor("Delete…", () => DeleteProfile(profile), enabled: _profiles.Profiles.Count > 1));
        more.Click += (s, e) => menu.IsOpen = true;
        actions.Children.Add(more);
        grid.Children.Add(actions);

        var card = new Border
        {
            Style = (Style)FindResource("Card"),
            Width = CardWidth,
            MinHeight = CardHeight,
            Margin = new Thickness(0, 0, 12, 12),
            Child = grid,
        };
        // The open profile's card carries an accent outline
        if (open) card.SetResourceReference(Border.BorderBrushProperty, "PrimaryBackground");
        return card;
    }

    /// <summary>A dashed "+ New profile" card at the end of the list.</summary>
    private FrameworkElement NewProfileCard()
    {
        var outline = new System.Windows.Shapes.Rectangle { RadiusX = 8, RadiusY = 8, StrokeThickness = 1.5, StrokeDashArray = new System.Windows.Media.DoubleCollection { 4, 3 } };
        outline.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "BorderMedium");

        var label = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var plus = new TextBlock { Text = "\uE710", FontFamily = (System.Windows.Media.FontFamily)FindResource("IconFont"), FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        plus.SetResourceReference(TextBlock.ForegroundProperty, "AccentText");
        label.Children.Add(plus);
        label.Children.Add(new TextBlock { Text = "New profile", FontSize = 14, VerticalAlignment = VerticalAlignment.Center });

        var button = new Button
        {
            Style = (Style)FindResource("SubtleButton"),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Content = label,
            ToolTip = "Create a profile with its own tasks. Its settings start as a copy of your newest profile.",
        };
        System.Windows.Automation.AutomationProperties.SetName(button, "New profile");
        button.Click += (s, e) => NewProfile();

        var grid = new Grid { Width = CardWidth, MinHeight = CardHeight, Margin = new Thickness(0, 0, 12, 12) };
        grid.Children.Add(outline);
        grid.Children.Add(button);
        return grid;
    }
}
