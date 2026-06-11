[Setup]
AppName=Task And Ticket Tracker
AppVersion=1.4.1.0
DefaultDirName={autopf}\TaskTrackerApp
DefaultGroupName=TaskTrackerApp
OutputDir=Releases
OutputBaseFilename=TaskTrackerApp-win-Setup-v1.4.1.0
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

[Run]
Filename: "{app}\TaskTrackerApp.exe"; Description: "{cm:LaunchProgram,TaskTrackerApp}"; Flags: nowait postinstall skipifsilent
