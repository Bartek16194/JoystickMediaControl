# JoystickMediaControl

Lightweight Windows utility for assigning joystick and throttle buttons to media controls.

Author: [Bartek16194 on GitHub](https://github.com/Bartek16194). Community: [Discord](https://discord.gg/WbChuSCGHQ).

## Installer (1.3.1)

Run `JoystickMediaControl-Setup-1.3.1.exe`. The default installation folder is `%LOCALAPPDATA%\Programs\JoystickMediaControl`; you can change it. Installation is per user and does not request administrator access.

The installer creates a Start Menu folder with application, uninstall, GitHub and Discord shortcuts. Desktop shortcut and Windows startup are optional. The app appears in Windows Installed apps and can be removed there or through its uninstaller. Existing settings are preserved, including after uninstalling.

The welcome page includes clickable GitHub and Discord links and checks for .NET Desktop Runtime x64 (7 or newer). If absent, it offers the official download page; the runtime is not bundled or installed automatically. Choose a supported Desktop Runtime for Windows x64. Close the running app from its tray menu before installing or upgrading.

The installer and application are unsigned. A Windows publisher verification warning may appear. No signing certificate is included.

## Use

Run `App/JoystickMediaControl.exe`. Keep all files in the App folder together.

1. Select your controller.
2. Select a media action.
3. Choose a button from the **Button** list and click **Apply binding**, or click **Detect button** and press the button on your controller.

Applying an action to an already assigned button updates that assignment. To edit a binding, select its row, choose another action and click **Apply binding**. To assign another button, choose it from the list first. Changes save automatically.

**Test action** sends the selected media command without requiring a joystick. **Remove selected** deletes the selected binding. Profiles can be exported and imported; importing replaces existing bindings.

Closing the window minimizes the app to the system tray. Double-click the tray icon to open it. Choose **Exit** from the tray menu to stop the app. **Enable bindings** pauses or resumes media control.

## Upgrade from the previous version

Exit Orion Media before starting JoystickMediaControl. Existing settings are read automatically from `%LOCALAPPDATA%\OrionMedia\settings.json` when no new settings file exists. New settings save to `%LOCALAPPDATA%\JoystickMediaControl\settings.json`. Old profiles with Polish action names can also be imported.

If Windows startup was enabled in the old version, turn **Start with Windows** off and on in this version to update the startup entry. Keep the app in its final location before enabling startup.

## Media controls

Play / Pause, Next track, Previous track and Stop send standard Windows media keys. Windows chooses the responding media application; stop playback in other apps or browser tabs if Spotify does not respond. Volume up, Volume down and Mute adjust system volume.

Play (Spotify) and Pause (Spotify) send separate commands to Spotify desktop windows. Support depends on Spotify's implementation; use Play / Pause if they do not work. A sent command does not confirm playback changed.

## TeamSpeak 3 controls

TS3 actions send configurable keyboard hotkeys. Set the same hotkeys in the TeamSpeak 3 desktop client. This does not require ClientQuery, an API key or a plugin, and does not change Windows microphone privacy settings or mute other applications' microphones.

Open **TeamSpeak settings** in JoystickMediaControl. Choose distinct hotkeys for each action and save. Available keys: F13-F24 or Ctrl+Shift+F1-F12. Defaults:

### Setup prompts

Selecting a TS3 action for the first time opens the setup guide automatically. The app also prompts before assigning, detecting or testing an unconfigured TS3 action. **Open TS3 options** brings an already running TS3 desktop window forward and sends Alt+P after verifying that TS3 has focus. It does not edit TS3's database or automatically add hotkeys.

The setup guide explains how to add and test each hotkey. **Send in 3s** gives you time to focus TS3. PTT holds its key for two seconds before releasing it; other actions send a tap. After confirming that the action works in TS3, tick **Configured in TS3** beside that action and click **Save setup**. Only the actions you confirm are enabled. This is your confirmation, not automatic detection of TS3 state. Changing a hotkey clears that action's confirmation.

If an unconfigured TS3 binding is pressed while the app is in the background, a tray notification offers to open setup. No keyboard command is sent for that action. The reminder is shown once per session, or again after saving setup. Existing TS3 bindings from older versions also need this one-time confirmation. Media bindings continue working.

| Action | Default hotkey |
| --- | --- |
| Push-to-talk (hold) | F13 |
| Toggle microphone mute | F14 |
| Mute microphone | F15 |
| Unmute microphone | F16 |
| Toggle speakers mute | F17 |
| Mute speakers | F18 |
| Unmute speakers | F19 |

In TS3, open **Tools > Options > Hotkeys**, choose the hotkey profile used by your connection, and add the matching microphone/speaker actions. For push-to-talk, also select **Push-To-Talk** in the Capture settings/profile and assign F13 (or your chosen key). Configure the PTT shortcut as a held key; do not assign a microphone toggle to that key. Mute/unmute actions use the corresponding activate/deactivate mute commands; toggle actions use toggle mute.

Most keyboards have no F13-F24 keys. To register them in TS3, start recording the hotkey in TS3, click **Send in 3s** beside the corresponding action in this app, and focus the TS3 capture dialog within three seconds. Ensure the TS3 dialog is actively recording; outside that dialog it can trigger an already configured action. Alternatively choose one of the Ctrl+Shift shortcuts and enter it with your keyboard.

Next, select a TS3 action from the main **Action** list and assign a joystick button using **Apply binding** or **Detect button**. PTT sends key-down when you press and key-up when you release. It releases held keys on controller refresh/disconnect, disabling bindings, reassigning/removing a held binding, importing a profile, opening TS3 settings, system suspend or normal app exit. If multiple buttons hold the same PTT key, the key remains held until all are released. PTT uses press/release edges rather than media debounce, so a quick release and repress is accepted. A force-killed/crashed process cannot guarantee a key-up; press and release the configured key or restart TS3 if necessary.

**Test action** holds PTT for one second and releases it; other TS3 tests send one key tap. Tests can change mute state or transmit audio when connected to TS3. The app reports sent hotkeys, not the actual TS3 mute/transmit state. Do not rely on the app's status label as a mute indicator.

TS3 must be running with the matching active hotkey and capture profiles. Synthetic hotkey handling depends on the TS3 hotkey backend and application permissions; verify it in TS3 before using it in a game. The keys are sent globally, so avoid assigning the same keyboard shortcut to an unrelated action in another app. If TS3 also sees the original joystick button, remove the duplicate TS3 joystick binding or disable the joystick hotkey support add-on for testing.

Configuration references: [TeamSpeak hotkey/profile troubleshooting](https://community.teamspeak.com/t/hotkeys-are-not-working-push-to-talk-is-not-working-troubleshooting/13840).

## Controller buttons

The manual list contains buttons declared by the selected controller's HID descriptor, including button numbers above 32. Report and collection identifiers distinguish controls when needed. HID numbers can differ from SimAppPro numbering.

The first report establishes the initial state. Release and press again if a switch was already held. Held buttons trigger once; debounce limits rapid repeat presses. Axes and POV hats reported as axes are not assignable. Controls remain available to games at the same time.

## Requirements

Windows 10/11 x64 and .NET Desktop Runtime x64 7 or newer. The app uses the newest installed major runtime. Download a supported Windows x64 Desktop Runtime from https://dotnet.microsoft.com/download/dotnet if needed.

## Source and verification

The Source folder contains the C# WinForms project. No external libraries or Spotify login are required.

```powershell
dotnet restore Source/JoystickMediaControl.csproj --configfile Source/NuGet.Config
dotnet publish Source/JoystickMediaControl.csproj --no-restore -c Release --self-contained false -o App
```

To rebuild the installer with NSIS 3.13, run `./build.ps1 -NsisCompiler 'C:\path\to\NSIS\makensis.exe'`. Its output is placed in `dist/`. Installer test mode `/S /TESTMODE /D=<test-folder>` extracts files and creates the uninstaller without writing registry entries or shortcuts. Uninstall with `/S /TESTMODE` to remove only package files in that test installation. Test mode is for verification, not user installation.

Automated tests cover button press/release edges, separate reports, high button numbers, manual binding creation and replacement, invalid button rejection, legacy profile migration, TS3 hotkey profiles, PTT key-down/key-up, shared keys, modifier order, failed-input rollback, cleanup, and synthetic reports based on detected Winwing controller descriptors. Physical button presses, Spotify playback and behavior in the TS3 client require a live check.
