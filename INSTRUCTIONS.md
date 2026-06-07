# Task Tracker - User Instructions

Welcome to your new lightweight, context-aware Task Tracker! Follow these simple steps to start using the application.

## 1. Running the Application

You can launch the application in two ways:

### Option A: Using Visual Studio
1. Open the solution file `TaskTracker.sln` located in `d:\One App\Tasks\` using Visual Studio.
2. Ensure `TaskTrackerApp` is set as your Startup Project.
3. Click the **Start (F5)** button or press `F5` on your keyboard to compile and run the app.

### Option B: Using the Command Line (Terminal)
1. Open PowerShell or Command Prompt.
2. Navigate to the application directory:
   ```bash
   cd "d:\One App\Tasks\TaskTrackerApp"
   ```
3. Run the application:
   ```bash
   dotnet run
   ```

## 2. Using the Widget

When the application launches, a small, dark floating widget will appear on your screen.

*   **Move the Widget:** Click and hold anywhere on the dark border surrounding the widget, then drag it to your preferred location on the screen.
*   **Track a Task:** Click inside the text box that says "Enter your current task..." and type the task you are focusing on right now.
*   **Minimize:** Click the small `✕` button in the top right corner of the widget. This will hide the widget from your screen, but the application will continue running silently in the background.

## 3. The System Tray

When minimized, the application resides in your Windows System Tray (the small icons next to your clock in the bottom right corner of the screen).

*   **Look for the Icon:** You will see a standard Information icon (or a blank square, depending on your Windows theme) representing the Task Tracker.
*   **Restore the Widget:** Double-click the icon to bring your task widget back to the screen instantly.
*   **Context Menu:** Right-click the system tray icon to reveal a menu where you can:
    *   **Show Widget:** Restores the widget.
    *   **Test Notification:** Manually trigger a Windows Toast notification to see how the app reminds you of your task.
    *   **Exit:** Completely close the application.

## 4. Context Awareness (How it protects you)

The application constantly monitors your environment in the background without using heavy system resources.

*   **Do Not Disturb Mode:** If you turn on Windows "Do Not Disturb" (Focus Assist), the text at the bottom of your widget will change to orange, indicating "DND Active (Muted)". 
*   **Notification Scrubbing:** If you trigger a notification (or if the app sends one automatically) while you are in Do Not Disturb or on a Microsoft Teams call, the app will **hide your specific task details**. Instead of showing your task text on screen, the Windows Toast notification will simply say "Background Task Active" to ensure privacy if you happen to be sharing your screen!
