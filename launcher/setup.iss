#define AppVersion "0.3.0"
[Setup]
AppId={{D7FCA782-2BCB-48D8-9D05-A45305ED1750}
AppName=305Edits FX Collection
AppVersion={#AppVersion}
AppPublisher=305Edits
DefaultDirName={localappdata}\Programs\305Edits FX Collection
DefaultGroupName=305Edits
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\..\..\outputs
OutputBaseFilename=305Edits-FX-Collection-Setup-0.3.0
SetupIconFile=..\assets\305Edits.ico
UninstallDisplayIcon={app}\305Edits FX Collection.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern dark
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no
VersionInfoVersion=0.3.0.0
VersionInfoProductName=305Edits FX Collection
VersionInfoCompany=305Edits
WizardSizePercent=110

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Ярлыки:"; Flags: unchecked

[Files]
Source: "..\..\..\outputs\305Edits FX Collection.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\assets\305Edits.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\305Edits\305Edits FX Collection"; Filename: "{app}\305Edits FX Collection.exe"
Name: "{userdesktop}\305Edits FX Collection"; Filename: "{app}\305Edits FX Collection.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\305Edits FX Collection.exe"; Description: "Запустить 305Edits FX Collection"; Flags: nowait postinstall skipifsilent
