using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace TaskTrackerApp;

public class ContextAwareEngine
{
    private bool _isInTeamsCall = false;
    private bool _isFocusAssistActive = false;
    
    public event EventHandler<bool> DistractionStateChanged;
    
    public bool IsInDistractionState => _isInTeamsCall || _isFocusAssistActive;

    public void StartMonitoring()
    {
        // 1. Registry Polling for Focus Assist (Do Not Disturb)
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        timer.Tick += (s, e) => CheckFocusAssistState();
        timer.Start();

        // 2. Try to connect to Teams WebSocket silently in background
        _ = ConnectToTeamsWebSocketAsync();
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

    private async Task ConnectToTeamsWebSocketAsync()
    {
        // For demonstration of the lightest approach.
        // Requires user to enable Third-Party API in Teams and get a token.
        // We'll just attempt connection. If it fails, it silently retries later.
        
        while (true)
        {
            try
            {
                using var ws = new ClientWebSocket();
                // We'd normally need a token here: ws://localhost:8124?token=xyz
                // Since we don't have one, we just demonstrate the architecture.
                await ws.ConnectAsync(new Uri("ws://localhost:8124?protocol-version=2.0.0"), CancellationToken.None);
                
                var buffer = new byte[1024];
                while (ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        // Simplified check without heavy JSON parsing to keep it light
                        if (json.Contains("\"meetingUpdate\"") && json.Contains("\"canToggleMute\":true"))
                        {
                            SetTeamsCallState(true);
                        }
                        else if (json.Contains("\"meetingUpdate\"") && json.Contains("\"canToggleMute\":false"))
                        {
                            SetTeamsCallState(false);
                        }
                    }
                }
            }
            catch
            {
                // Teams might not be running or API is disabled. 
                // We use exponential backoff or simple delay in real implementation.
            }
            
            SetTeamsCallState(false);
            await Task.Delay(TimeSpan.FromMinutes(1)); // Retry every minute
        }
    }
    
    private void SetTeamsCallState(bool inCall)
    {
        if (_isInTeamsCall != inCall)
        {
            _isInTeamsCall = inCall;
            DistractionStateChanged?.Invoke(this, IsInDistractionState);
        }
    }
}
