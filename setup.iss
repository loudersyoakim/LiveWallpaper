[Setup]
AppName=LiveWallpaper
AppVersion=3.1
AppPublisher=MockingCLOWN
AppPublisherURL=https://github.com/loudersyoakim
AppCopyright=Copyright (C) 2026 MockingCLOWN
DefaultDirName={localappdata}\Programs\LiveWallpaper
DefaultGroupName=LiveWallpaper
DisableProgramGroupPage=yes
OutputDir=installer
OutputBaseFilename=LiveWallpaper_Setup_v3.1
SetupIconFile=app_icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; Gunakan gambar yang ukurannya sesuai untuk Wizard, atau hapus baris ini jika tidak ada
WizardSmallImageFile=app.bmp
ShowLanguageDialog=no
PrivilegesRequired=lowest
MinVersion=10.0
UninstallDisplayName=LiveWallpaper
UninstallDisplayIcon={app}\LiveWallpaper.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Pastikan folder publish ini benar-benar ada setelah kamu menjalankan 'dotnet publish'
Source: "bin\Release\net9.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
Source: "app_icon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\LiveWallpaper"; Filename: "{app}\LiveWallpaper.exe"; IconFilename: "{app}\app.ico"
Name: "{autodesktop}\LiveWallpaper"; Filename: "{app}\LiveWallpaper.exe"; IconFilename: "{app}\app.ico"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "LiveWallpaper"; ValueData: """{app}\LiveWallpaper.exe"""; Flags: uninsdeletevalue

[Run]
; Menjalankan aplikasi setelah install
Filename: "{app}\LiveWallpaper.exe"; Description: "Launch LiveWallpaper now"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{app}\livewallpaper.log"
Type: filesandordirs; Name: "{localappdata}\LiveWallpaper"

[Code]
// Fungsi untuk mematikan aplikasi jika sedang berjalan saat install/uninstall
function KillApp(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('taskkill', '/f /im LiveWallpaper.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    KillApp();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    KillApp();
end;