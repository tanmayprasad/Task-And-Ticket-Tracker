using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace TaskTrackerApp;

public class ContextAwareEngine
{
    private bool _isFocusAssistActive = false;

    public event EventHandler<bool>? DistractionStateChanged;

    public bool IsInDistractionState => _isFocusAssistActive;

    public void StartMonitoring()
    {
        // Registry Polling for Focus Assist (Do Not Disturb)
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        timer.Tick += (s, e) => CheckFocusAssistState();
        timer.Start();
    }

    private void CheckFocusAssistState()
    {
        bool wasDistracted = IsInDistractionState;

        try
        {
            // Fallback for Windows 10/11 using HKCU (No admin required)
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings");
            if (key != null)
            {
                var toastsEnabled = key.GetValue("NOC_GLOBAL_SETTING_TOASTS_ENABLED");
                if (toastsEnabled is int val && val == 0)
                {
                    _isFocusAssistActive = true;
                }
                else
                {
                    _isFocusAssistActive = false;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to check Focus Assist: {ex.Message}");
        }

        if (wasDistracted != IsInDistractionState)
        {
            DistractionStateChanged?.Invoke(this, IsInDistractionState);
        }
    }
}
