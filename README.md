# JoystickMediaControl

Lightweight Windows application that maps joystick and throttle buttons to media controls and TeamSpeak 3 hotkeys.

**[Download the Windows installer](https://github.com/Bartek16194/JoystickMediaControl/releases/latest/download/JoystickMediaControl-Setup-1.3.2.exe)** · **[All releases](https://github.com/Bartek16194/JoystickMediaControl/releases)**

Author: [Bartek16194](https://github.com/Bartek16194) · Community: [Discord](https://discord.gg/WbChuSCGHQ)

## Features

- Play/pause, next/previous track, stop, system volume and mute.
- Separate play and pause commands for Spotify desktop, subject to Spotify support.
- TeamSpeak 3 push-to-talk while holding a button, microphone mute/unmute/toggle and speaker mute/unmute/toggle.
- Automatic TS3 setup prompts, configurable hotkeys and delayed key sending for setup and testing.
- Manual button selection or detection by pressing a controller button.
- Multiple HID controllers, including button numbers above 32.
- Profiles, automatic settings saving, system tray controls and optional Windows startup.
- Per-user installer with Start Menu shortcuts, optional desktop shortcut and an uninstaller.

HID enumeration and synthetic reports were tested against a WINWING Orion Joystick Base 2 + JGRIP-F16 (42 buttons) and an Orion Throttle Base II + F18 HANDLE (111 buttons). Physical joystick input, Spotify playback and TS3 client behavior have not yet been verified end to end.

## Install and use

1. Download the installer from Releases and exit any running copy of the application from its tray menu.
2. Install it. The default folder is `%LOCALAPPDATA%\Programs\JoystickMediaControl` and can be changed.
3. Select your controller, choose an action and a button, then click **Apply binding**. Alternatively use **Detect button**.

The app requires Windows 10/11 x64 and .NET Desktop Runtime x64 7 or newer. Use a supported Desktop Runtime from [Microsoft](https://dotnet.microsoft.com/download/dotnet). The runtime is not bundled; the installer checks for it and offers a download link if needed. Binaries are currently unsigned.

Media actions use Windows media keys, which Windows routes to a responding player. TS3 controls use matching keyboard hotkeys that must be set up once in TS3. The app opens a setup guide for unconfigured actions and records your confirmation separately for each action. It does not automatically add hotkeys to TS3 or read its mute state.

Closing the main window minimizes to the tray. Choose **Exit** from the tray menu to stop the program. Settings are retained on upgrade and uninstall.

See the [full user guide](docs/USER_GUIDE.md) for profiles, TeamSpeak configuration, troubleshooting and upgrade behavior.

## Build

Requirements: .NET SDK 7 and NSIS 3.13 for the installer. The project uses C# WinForms and Windows APIs with no third-party application libraries.

```powershell
# Build the app and run self-tests
./build.ps1

# Build the installer too
./build.ps1 -NsisCompiler 'C:\path\to\NSIS\makensis.exe'
```

The app is written to `App/`, verification results to `TestResults/`, and the installer to `dist/`. Build products and personal settings are excluded from the repository.

The v1.3.2 build passed 94 checks covering input press/release, manual bindings, profiles, PTT key handling, setup confirmation and synthetic HID reports. Installer extraction and uninstall cleanup were checked in an isolated directory without writing installation registry entries or shortcuts. The media scan-code regression checks do not emit live keys. FaceTrackNoIR compatibility requires user verification. Full TS3/Spotify behavior and normal installer integration still need a live check.
