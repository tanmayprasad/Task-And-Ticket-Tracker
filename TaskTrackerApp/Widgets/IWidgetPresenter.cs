using System;
using System.Collections.Generic;
using TaskTrackerApp.Models;

namespace TaskTrackerApp.Widgets;

/// <summary>A surface that shows the active tasks while the manager window is minimized.</summary>
public interface IWidgetPresenter
{
    event EventHandler<TaskModel>? OnSkipRequested;
    event EventHandler? OnResetRequested;
    event EventHandler<TaskModel>? OnTaskRequested;

    bool IsVisible { get; }

    void SetActiveTasks(List<TaskModel> tasks);
    void ApplySettings(SettingsModel settings);
    void Show();
    void Hide();
    void Close();

    /// <summary>Returns the presenter to its default position (tray "Reset Widget Position").</summary>
    void ResetPosition();
}
