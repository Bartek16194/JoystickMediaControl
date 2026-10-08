# Changelog

## 1.3.2

- Send global media commands with physical scan codes and extended-key flags to address interference with keyboard listeners such as FaceTrackNoIR.
- Preserve support for all media players; existing bindings and profiles remain compatible.
- Add regression checks for all seven global media keys without emitting live keyboard input.

## 1.3.1

- Add an automatic setup guide when selecting, assigning or testing an unconfigured TeamSpeak 3 action.
- Add a button to open a running TS3 client's options and delayed hotkey sending for capture and testing.
- Track setup confirmation separately for each action and invalidate it when its hotkey changes.
- Notify from the tray when an unconfigured TS3 binding is used in the background.
- Retain media controls, manual joystick bindings, profile support and the per-user Windows installer.

## 1.3.0

- Add TeamSpeak 3 push-to-talk and microphone/speaker hotkey actions.
- Release PTT on physical release, disconnect/refresh, disable, reassignment, profile import, setup, suspend and normal exit.

## 1.2.0

- Add the per-user installer, Start Menu shortcuts, optional desktop shortcut and author/community links.
