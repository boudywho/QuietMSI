; Stable AppId is required for in-place upgrades. Never include a version in it.
#ifndef PayloadDir
  #define PayloadDir "..\artifacts\release\payload"
#endif
#ifndef OutputPath
  #define OutputPath "..\artifacts"
#endif
#ifndef AppVersion
  #define AppVersion GetFileVersion(PayloadDir + "\QuietMSI.exe")
#endif
#ifndef AppIdentity
  #define AppIdentity "{{16358337-25C5-4CC4-9E08-63D4F7C5538B}"
#endif
#ifndef OutputName
  #define OutputName "QuietMSI-" + AppVersion + "-Setup-x64"
#endif
#ifndef ProductName
  #define ProductName "QuietMSI"
#endif
#ifndef IntegrationArguments
  #define IntegrationArguments "--setup-integration"
#endif

[Setup]
AppId={#AppIdentity}
AppName={#ProductName}
AppVersion={#AppVersion}
AppPublisher=QuietMSI contributors
AppComments=Independent MSI laptop controls
DefaultDirName={autopf}\QuietMSI
DefaultGroupName=QuietMSI
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputPath}
OutputBaseFilename={#OutputName}
SetupIconFile=..\src\QuietMSI\Assets\QuietMSI.ico
UninstallDisplayIcon={app}\QuietMSI.exe
LicenseFile=..\LICENSE
WizardStyle=modern dynamic
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
#ifndef VerificationBuild
AppMutex=Local\QuietMSI.TrayApplication
#endif
SetupMutex=Global\QuietMSI.Setup.v1
Uninstallable=yes
UninstallDisplayName={#ProductName}
VersionInfoVersion={#AppVersion}
VersionInfoProductName=QuietMSI Setup
VersionInfoDescription=QuietMSI installer
DisableWelcomePage=no
DisableDirPage=auto
ChangesEnvironment=no

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\QuietMSI.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\SECURITY.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\docs\*"; DestDir: "{app}\docs"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PayloadDir}\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#ProductName}"; Filename: "{app}\QuietMSI.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\{#ProductName}"; Filename: "{app}\QuietMSI.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\QuietMSI.exe"; Description: "Open QuietMSI"; Flags: postinstall nowait skipifsilent runascurrentuser

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  InstalledVersion, IncomingVersion: Int64;
begin
  Result := '';
  if GetPackedVersion(ExpandConstant('{app}\QuietMSI.exe'), InstalledVersion) and
     StrToVersion('{#AppVersion}', IncomingVersion) then
    if ComparePackedVersion(InstalledVersion, IncomingVersion) > 0 then
      Result := 'A newer version of QuietMSI is already installed. This installer cannot downgrade it.';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then begin
    if not Exec(ExpandConstant('{app}\QuietMSI.exe'), '{#IntegrationArguments}', ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ResultCode) then
      RaiseException('QuietMSI hardware support setup could not start. Run Setup again to repair the installation.');
    if ResultCode <> 0 then
      RaiseException('QuietMSI hardware support or startup migration failed. Details are in the QuietMSI application log. Run Setup again to repair the installation.');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then begin
    if FileExists(ExpandConstant('{app}\QuietMSI.exe')) then begin
      if not Exec(ExpandConstant('{app}\QuietMSI.exe'), '--uninstall-integration', ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ResultCode) then
        RaiseException('QuietMSI startup cleanup could not start. No application files were removed.');
      if ResultCode <> 0 then
        RaiseException('QuietMSI startup cleanup failed. No application files were removed.');
    end;
  end;
end;
