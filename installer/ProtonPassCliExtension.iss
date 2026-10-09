; Inno Setup script for "Pass CLI for Command Palette (unofficial)".
; Adapted from Microsoft's Command Palette extension template
; (https://learn.microsoft.com/windows/powertoys/command-palette/publish-extension-winget).
;
; Build with scripts/build-installer.ps1, which passes:
;   /DAppVersion=<x.y.z>  /DArch=x64|arm64  /DPublishDir=<dotnet publish output>  /DOutputDir=<dir>

#ifndef AppVersion
  #error AppVersion must be passed on the command line (/DAppVersion=1.0.0)
#endif
#ifndef Arch
  #error Arch must be passed on the command line (/DArch=x64 or /DArch=arm64)
#endif
#ifndef PublishDir
  #error PublishDir must be passed on the command line
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

#define AppName "Pass CLI for Command Palette (unofficial)"
#define AppPublisher "kud3n013"
#define AppURL "https://github.com/kud3n013/command-palette-pass-cli-extention"
#define ExeName "ProtonPassCliExtension.exe"

; AppId: stable forever, NOT the COM CLSID. Inno registers the uninstall entry as "{AppId}_is1",
; which is the ProductCode used in the winget installer manifest. The leading "{{" is Inno's escape for "{".
#define AppId "{{2A158EE8-5834-42CE-9837-A00C5987E3D6}"

; COM CLSID of the extension: must match [Guid] on ProtonPassCliExtension.cs and the CLSID in Package.appxmanifest.
#define ExtensionClsid "{{c312420f-a811-4569-b608-4764363418cd}"

#if Arch == "arm64"
  #define ArchAllowed "arm64"
#else
  #define ArchAllowed "x64compatible"
#endif

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
VersionInfoVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
DefaultDirName={autopf}\ProtonPassCliExtension
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed={#ArchAllowed}
ArchitecturesInstallIn64BitMode={#ArchAllowed}
MinVersion=10.0.19041
OutputDir={#OutputDir}
OutputBaseFilename=ProtonPassCliExtension-{#AppVersion}-{#Arch}
SetupIconFile=..\branding\icon\icon.ico
UninstallDisplayIcon={app}\icon.ico
UninstallDisplayName={#AppName}
InfoBeforeFile=info.txt
Compression=lzma
SolidCompression=yes
WizardStyle=modern
; Command Palette keeps the extension process alive, so we stop it ourselves (see [Code]).
CloseApplications=no
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; The SDK also copies project files into the publish folder; keep them out of the install.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,*.iss,*.ps1,Installer,Properties,Package.appxmanifest"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\branding\icon\icon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
; Out-of-process COM server registration, exactly as in Microsoft's template. Command Palette launches
; the exe with -RegisterProcessAsComServer. Both keys are removed on uninstall.
Root: HKCU; Subkey: "Software\Classes\CLSID\{#ExtensionClsid}"; ValueType: string; ValueName: ""; ValueData: "{#AppName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\CLSID\{#ExtensionClsid}\LocalServer32"; ValueType: string; ValueName: ""; ValueData: """{app}\{#ExeName}"" -RegisterProcessAsComServer"; Flags: uninsdeletekey

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
procedure StopExtension();
var
  ResultCode: Integer;
begin
  // Exit code 128 just means "no such process"; ignore the result.
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#ExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopExtension();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  StopExtension();
  Result := True;
end;
