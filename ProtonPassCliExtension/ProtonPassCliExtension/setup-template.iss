; Inno Setup script for the Proton Pass (unofficial) Command Palette extension.
; Invoked by build-exe.ps1 / the release workflow with /DMyAppVersion and /DArchitecturesAllowed.

#define MyAppName "Proton Pass (unofficial)"
#define MyAppExeName "ProtonPassCliExtension.exe"
#define MyAppFileName "ProtonPassCliExtension"
#define MyAppPublisher "kud3n013"
#define MyAppURL "https://github.com/kud3n013/command-palette-pass-cli-extention"
; Must match the [Guid] on ProtonPassCliExtension.cs
; Leading "{{" is Inno's escape for a literal "{".
#define MyAppCLSID "{{c312420f-a811-4569-b608-4764363418cd}"

[Setup]
AppId={#MyAppCLSID}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
OutputBaseFilename={#MyAppFileName}_{#MyAppVersion}_{#ArchitecturesAllowed}
ArchitecturesAllowed={#ArchitecturesAllowed}
ArchitecturesInstallIn64BitMode={#ArchitecturesAllowed}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
OutputDir=Installer

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; The SDK also copies project files (including earlier Installer\ output) into publish\; keep them out.
Source: "publish\*"; DestDir: "{app}"; Excludes: "*.pdb,*.iss,*.ps1,Installer,Properties,Package.appxmanifest"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
; Register the out-of-process COM server so Command Palette can discover and launch the extension.
Root: HKCU; Subkey: "Software\Classes\CLSID\{#MyAppCLSID}"; ValueType: string; ValueName: ""; ValueData: "{#MyAppName}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\CLSID\{#MyAppCLSID}\LocalServer32"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" -RegisterProcessAsComServer"; Flags: uninsdeletekey
