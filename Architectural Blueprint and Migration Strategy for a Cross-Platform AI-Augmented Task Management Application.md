# **Avalonia UI Porting Guide: Component-Based Architecture**

This document outlines the technical architecture and implementation steps required to port the existing Windows task-tracking application to a cross-platform environment using Avalonia UI.  
To ensure the application remains lightweight, modular, and fully prepared for the future integration of a local AI chat feature, the codebase will utilize a strict component-based MVVM (Model-View-ViewModel) architecture.

## **1\. Architectural Strategy**

Instead of a monolithic application window, the Avalonia UI port will be divided into modular, pluggable views. This prevents the existing, stable task-tracking logic from becoming entangled with the complex AI logic that will be introduced later.

* **Main Application Shell:** A lightweight, invisible window that acts solely as the host container and handles OS-level window management.  
* **Task Application Component (Phase 1):** An Avalonia UserControl containing the legacy offline functionality (capturing tickets, descriptions, and acceptance criteria).  
* **AI Chat Component (Phase 2):** A separate UserControl that will be plugged into the shell in the future, keeping the heavy Retrieval-Augmented Generation (RAG) and Large Language Model (LLM) logic completely isolated from the core task tracker.

By selecting Avalonia, the existing WPF C\# logic, XAML structure, and MVVM data bindings can be ported with minimal friction while gaining native cross-platform compatibility across Windows, macOS, and Linux.

## **2\. Main Application Shell (The Host)**

The root application window must be configured to be chromeless and transparent to recreate the floating widget feel of the original application.  
In Avalonia, this is achieved by removing system decorations and setting the background to transparent. Using Background="Transparent" rather than {x:Null} is critical, as it ensures the empty areas of the window still capture mouse clicks (hit-testing) instead of passing them through to the applications running behind it.  
**MainWindow.axaml:**

XML  
\<Window xmlns\="https://github.com/avaloniaui"  
        x:Class\="App.Views.MainWindow"  
        SystemDecorations\="None"  
        TransparencyLevelHint\="Transparent"  
        Background\="Transparent"  
        PointerPressed\="Window\_PointerPressed"\>  
    \<Grid\>  
        \<ContentControl Content\="{Binding CurrentViewComponent}" /\>  
    \</Grid\>  
\</Window\>

To allow the user to reposition this chromeless window, the application must capture the pointer pressed event and invoke Avalonia's native cross-platform drag method, BeginMoveDrag.  
**MainWindow.axaml.cs:**

C\#  
using Avalonia.Controls;  
using Avalonia.Input;

namespace App.Views;

public partial class MainWindow : Window  
{  
    public MainWindow()  
    {  
        InitializeComponent();  
    }

    private void Window\_PointerPressed(object? sender, PointerPressedEventArgs e)  
    {  
        // Initiate the drag when the left mouse button is pressed  
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)  
        {  
            this.BeginMoveDrag(e);  
        }  
    }  
}

## **3\. The Task Application Component**

The existing task capture features are ported into a standalone component. Because the Main Shell manages the routing (via the CurrentViewComponent binding), the Task View only needs to concern itself with its own specific UI layout and data capture.  
**TaskView.axaml:**

XML  
\<UserControl xmlns\="https://github.com/avaloniaui"  
             x:Class\="App.Views.TaskView"\>  
    \<Border Background\="\#2D2D30" CornerRadius\="8" Padding\="15"\>  
        \<StackPanel Spacing\="10"\>  
            \<TextBlock Text\="Active Task Ticket" FontWeight\="Bold" Foreground\="White"/\>  
              
            \<TextBox Watermark\="Task Ticket ID"   
                     Text\="{Binding TicketId}" /\>  
                       
            \<TextBox Watermark\="Description"   
                     Text\="{Binding Description}"   
                     AcceptsReturn\="True"   
                     Height\="80" /\>  
                       
            \<TextBox Watermark\="Acceptance Criteria"   
                     Text\="{Binding Criteria}"   
                     AcceptsReturn\="True"   
                     Height\="80" /\>  
                       
            \<Button Content\="Save Task"   
                    Command\="{Binding SaveCommand}"   
                    HorizontalAlignment\="Right"/\>  
        \</StackPanel\>  
    \</Border\>  
\</UserControl\>

## **4\. System Tray Integration**

Because this is primarily an offline utility designed to remain out of the way, it must minimize to the system tray rather than cluttering the user's taskbar. Avalonia provides a native TrayIcon API that functions universally across supported desktop environments.  
This is declared globally within the App.axaml file so that the icon persists regardless of which component (Task or AI) is currently active inside the Main Shell.  
**App.axaml:**

XML  
\<Application xmlns\="https://github.com/avaloniaui"  
             x:Class\="App.App"\>  
               
    \<TrayIcon.Icons\>  
        \<TrayIcons\>  
            \<TrayIcon Icon\="/Assets/app-icon.ico" ToolTipText\="Task Tracker"\>  
                \<TrayIcon.Menu\>  
                    \<NativeMenu\>  
                        \<NativeMenuItem Header\="Show Widget" Command\="{Binding ShowAppCommand}" /\>  
                        \<NativeMenuItemSeparator /\>  
                        \<NativeMenuItem Header\="Exit" Command\="{Binding ExitCommand}" /\>  
                    \</NativeMenu\>  
                \</TrayIcon.Menu\>  
            \</TrayIcon\>  
        \</TrayIcons\>  
    \</TrayIcon.Icons\>  
\</Application\>

To ensure cross-platform compatibility, the referenced icon (app-icon.ico) must be included in the project configuration as an AvaloniaResource. The properties mapped to the NativeMenu commands will allow the user to easily hide or show the application.

## **5\. Preparing for Future AI Integration**

Once this base architectural port is completed and verified, introducing the AI features will be highly streamlined.  
A new AIChatView.axaml (UserControl) will be created. The MainViewModel will simply update the CurrentViewComponent property to switch the visible UI from the Task View to the AI View. This design guarantees that the embedded SQLite vector databases, ONNX embedding runtimes, and local WebSocket clients required for the AI and context engine do not pollute the core offline task-tracking logic.