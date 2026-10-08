Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "nsDialogs.nsh"
!include "x64.nsh"

!define PRODUCT "JoystickMediaControl"
!define VERSION "1.3.1"
!define GITHUB "https://github.com/Bartek16194"
!define DISCORD "https://discord.gg/WbChuSCGHQ"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\JoystickMediaControl"

Name "${PRODUCT}"
OutFile "..\dist\JoystickMediaControl-Setup-${VERSION}.exe"
InstallDir "$LOCALAPPDATA\Programs\${PRODUCT}"
RequestExecutionLevel user
SetCompressor /SOLID lzma
ShowInstDetails show
ShowUninstDetails show
BrandingText "${PRODUCT} ${VERSION} | Bartek16194"
VIProductVersion "1.3.1.0"
VIAddVersionKey /LANG=1033 "ProductName" "${PRODUCT}"
VIAddVersionKey /LANG=1033 "ProductVersion" "${VERSION}"
VIAddVersionKey /LANG=1033 "FileVersion" "${VERSION}"
VIAddVersionKey /LANG=1033 "CompanyName" "Bartek16194"
VIAddVersionKey /LANG=1033 "LegalCopyright" "Bartek16194"
VIAddVersionKey /LANG=1033 "FileDescription" "${PRODUCT} Setup"

Var TestMode
Var RuntimeFound
Var Dialog
Var Control
Var Params

Page custom WelcomePage
!define MUI_COMPONENTSPAGE_SMALLDESC
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\JoystickMediaControl.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Launch JoystickMediaControl"
!define MUI_FINISHPAGE_LINK "GitHub - Bartek16194"
!define MUI_FINISHPAGE_LINK_LOCATION "${GITHUB}"
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH
!insertmacro MUI_LANGUAGE "English"

Function .onInit
  SetShellVarContext current
  SetRegView 64
  StrCpy $TestMode 0
  ${GetParameters} $Params
  ${GetOptions} $Params "/TESTMODE" $0
  ${IfNot} ${Errors}
    StrCpy $TestMode 1
  ${EndIf}
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "JoystickMediaControl requires 64-bit Windows."
    Abort
  ${EndIf}
  ${If} $TestMode == 0
    Call CheckRunning
    ReadRegStr $0 HKCU "${UNINSTALL_KEY}" "InstallLocation"
    ${If} $0 != ""
      StrCpy $INSTDIR $0
    ${EndIf}
  ${EndIf}
  Call CheckRuntime
FunctionEnd

Function CheckRunning
  retry:
  System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "Local\OrionMedia.SingleInstance") p .r0'
  ${If} $0 != 0
    System::Call 'kernel32::CloseHandle(p r0)'
    MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION "Exit JoystickMediaControl (or Orion Media) from its system tray menu before continuing." IDRETRY retry
    Abort
  ${EndIf}
FunctionEnd

Function CheckRuntime
  StrCpy $RuntimeFound 0
  StrCpy $1 0
  loop:
    ClearErrors
    EnumRegValue $0 HKLM "SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App" $1
    ${If} ${Errors}
      Goto done
    ${EndIf}
    ; IntOp parses the initial integer of the version, e.g. 9.0.15 -> 9.
    IntOp $2 $0 + 0
    ${If} $2 >= 7
      StrCpy $RuntimeFound 1
      Goto done
    ${EndIf}
    IntOp $1 $1 + 1
    Goto loop
  done:
FunctionEnd

Function OpenGitHub
  Pop $0
  ExecShell "open" "${GITHUB}"
FunctionEnd
Function OpenDiscord
  Pop $0
  ExecShell "open" "${DISCORD}"
FunctionEnd
Function OpenRuntime
  Pop $0
  ExecShell "open" "https://dotnet.microsoft.com/download/dotnet"
FunctionEnd

Function WelcomePage
  !insertmacro MUI_HEADER_TEXT "JoystickMediaControl ${VERSION}" "Setup for the current Windows user"
  nsDialogs::Create 1018
  Pop $Dialog
  ${If} $Dialog == error
    Abort
  ${EndIf}
  ${NSD_CreateLabel} 0 0 100% 35u "Install JoystickMediaControl to control media with joystick and throttle buttons."
  Pop $Control
  ${NSD_CreateLabel} 0 38u 100% 35u "The next pages let you choose shortcuts, Windows startup, and the installation folder. Existing bindings will be kept."
  Pop $Control
  ${NSD_CreateLink} 0 78u 100% 14u "GitHub - Bartek16194"
  Pop $Control
  ${NSD_OnClick} $Control OpenGitHub
  ${NSD_CreateLink} 0 98u 100% 14u "Discord community"
  Pop $Control
  ${NSD_OnClick} $Control OpenDiscord
  ${If} $RuntimeFound == 1
    ${NSD_CreateLabel} 0 128u 100% 28u ".NET Desktop Runtime x64: detected."
    Pop $Control
  ${Else}
    ${NSD_CreateLabel} 0 122u 100% 28u ".NET Desktop Runtime x64 (7 or newer) is required to run the app. Install it separately if it is not already available."
    Pop $Control
    ${NSD_CreateLink} 0 157u 100% 14u "Download .NET Desktop Runtime for Windows x64"
    Pop $Control
    ${NSD_OnClick} $Control OpenRuntime
  ${EndIf}
  nsDialogs::Show
FunctionEnd

Section "Application and Start Menu shortcuts" CoreSection
  SectionIn RO
  ${If} $TestMode == 0
    Call CheckRunning
  ${EndIf}
  SetOutPath "$INSTDIR"
  File "..\App\JoystickMediaControl.exe"
  File "..\App\JoystickMediaControl.dll"
  File "..\App\JoystickMediaControl.deps.json"
  File "..\App\JoystickMediaControl.runtimeconfig.json"
  File "..\README.md"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  ${If} $TestMode == 0
    CreateDirectory "$SMPROGRAMS\${PRODUCT}"
    CreateShortcut "$SMPROGRAMS\${PRODUCT}\JoystickMediaControl.lnk" "$INSTDIR\JoystickMediaControl.exe"
    CreateShortcut "$SMPROGRAMS\${PRODUCT}\Uninstall.lnk" "$INSTDIR\Uninstall.exe"
    WriteINIStr "$SMPROGRAMS\${PRODUCT}\GitHub.url" "InternetShortcut" "URL" "${GITHUB}"
    WriteINIStr "$SMPROGRAMS\${PRODUCT}\Discord.url" "InternetShortcut" "URL" "${DISCORD}"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "${PRODUCT}"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "Publisher" "Bartek16194"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\JoystickMediaControl.exe"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
    WriteRegStr HKCU "${UNINSTALL_KEY}" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
    WriteRegStr HKCU "${UNINSTALL_KEY}" "URLInfoAbout" "${GITHUB}"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "HelpLink" "${DISCORD}"
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
    ; Preserve an existing startup preference when upgrading or moving from portable.
    ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "JoystickMediaControl"
    ReadRegStr $1 HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "OrionMedia"
    ${If} $0 != ""
    ${OrIf} $1 != ""
      WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "JoystickMediaControl" '$\"$INSTDIR\JoystickMediaControl.exe$\"'
      DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "OrionMedia"
    ${EndIf}
  ${EndIf}
SectionEnd

Section /o "Desktop shortcut" DesktopSection
  ${If} $TestMode == 0
    CreateShortcut "$DESKTOP\JoystickMediaControl.lnk" "$INSTDIR\JoystickMediaControl.exe"
  ${EndIf}
SectionEnd

Section /o "Start with Windows" StartupSection
  ${If} $TestMode == 0
    WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "JoystickMediaControl" '$\"$INSTDIR\JoystickMediaControl.exe$\"'
  ${EndIf}
SectionEnd

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
!insertmacro MUI_DESCRIPTION_TEXT ${CoreSection} "Install the application, Start Menu shortcuts, GitHub/Discord links and uninstaller."
!insertmacro MUI_DESCRIPTION_TEXT ${DesktopSection} "Create an application shortcut on your desktop."
!insertmacro MUI_DESCRIPTION_TEXT ${StartupSection} "Automatically start JoystickMediaControl when you sign in to Windows."
!insertmacro MUI_FUNCTION_DESCRIPTION_END

Function un.onInit
  SetShellVarContext current
  SetRegView 64
  StrCpy $TestMode 0
  ${GetParameters} $Params
  ${GetOptions} $Params "/TESTMODE" $0
  ${IfNot} ${Errors}
    StrCpy $TestMode 1
  ${EndIf}
  ${If} $TestMode == 0
    retry:
    System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "Local\OrionMedia.SingleInstance") p .r0'
    ${If} $0 != 0
      System::Call 'kernel32::CloseHandle(p r0)'
      MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION "Exit JoystickMediaControl from its system tray menu before uninstalling." IDRETRY retry
      Abort
    ${EndIf}
  ${EndIf}
FunctionEnd

Section "Uninstall"
  ${If} $TestMode == 0
    ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "JoystickMediaControl"
    ${If} $0 == '$\"$INSTDIR\JoystickMediaControl.exe$\"'
      DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "JoystickMediaControl"
    ${EndIf}
    Delete "$DESKTOP\JoystickMediaControl.lnk"
    Delete "$SMPROGRAMS\${PRODUCT}\JoystickMediaControl.lnk"
    Delete "$SMPROGRAMS\${PRODUCT}\Uninstall.lnk"
    Delete "$SMPROGRAMS\${PRODUCT}\GitHub.url"
    Delete "$SMPROGRAMS\${PRODUCT}\Discord.url"
    RMDir "$SMPROGRAMS\${PRODUCT}"
    DeleteRegKey HKCU "${UNINSTALL_KEY}"
  ${EndIf}
  ; Remove only files installed by this package; retain user settings and other files.
  Delete "$INSTDIR\JoystickMediaControl.exe"
  Delete "$INSTDIR\JoystickMediaControl.dll"
  Delete "$INSTDIR\JoystickMediaControl.deps.json"
  Delete "$INSTDIR\JoystickMediaControl.runtimeconfig.json"
  Delete "$INSTDIR\README.md"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
SectionEnd
