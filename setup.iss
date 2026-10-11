[Setup]
AppName=Task And Ticket Tracker
AppVersion=1.6.0.0
DefaultDirName={autopf}\TaskTrackerApp
DefaultGroupName=TaskTrackerApp
OutputDir=Releases
OutputBaseFilename=TaskTrackerApp-win-Setup-v1.6.0.0
Compression=lzma
SolidCompression=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=force
AppMutex=TaskTrackerAppMutex_OneApp
UninstallDisplayIcon={app}\TaskTrackerApp.exe
SetupIconFile=TaskTrackerApp\app.ico

[Files]
Source: "publish_inno\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\TaskTrackerApp"; Filename: "{app}\TaskTrackerApp.exe"
Name: "{autodesktop}\TaskTrackerApp"; Filename: "{app}\TaskTrackerApp.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Registry]
; "Start with Windows" is written by the app (StartupLauncher); only remove it on uninstall
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "TaskAndTicketTracker"; ValueType: none; Flags: uninsdeletevalue dontcreatekey

[Run]
Filename: "{app}\TaskTrackerApp.exe"; Description: "{cm:LaunchProgram,TaskTrackerApp}"; Flags: nowait postinstall skipifsilent
