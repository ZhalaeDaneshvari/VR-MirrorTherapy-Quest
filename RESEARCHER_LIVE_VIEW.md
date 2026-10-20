# Researcher live view and interview handoff

The standalone Quest app runs on the headset. Unity does not need to be open.
Use the Desktop **Quest Live View.command** shortcut to watch its USB display on this Mac.

1. Connect the Quest with a USB data cable. Wake it and allow USB debugging if prompted.
2. Double-click **Quest Live View.command** on the Desktop.
3. Wear the headset and open **Quest Body Driven Avatar**. The researcher watches the live-view window.
4. Close that window to stop viewing. This does not stop the participant app or its data recording.

The launcher is read-only, uses no audio forwarding, and saves no video. Movement-data recording and the existing Desktop sync remain separate. It uses the same Unity Android SDK adb as the data copier. Keep the headset awake/worn; sleeping headsets can show a blank or frozen display. Disconnecting USB ends the stream; reconnect and relaunch the shortcut. Avoid running multiple copies.

## Setup on another Apple Silicon Mac

Install Homebrew, then `brew install scrcpy`. Install Unity 6000.6.2f1 Android support, or set the `ADB` environment variable to your Android SDK adb executable. Copy `Tools/Quest Live View.command` to Desktop and make it executable (`chmod +x`). Override `SCRCPY` if installed elsewhere.

The default captures the left eye (`2064:2208:0:0`), verified against this Quest 3's 4128×2208 awake display, at maximum dimension 1600 and up to 30fps. Quest firmware may expose both eye images or black borders. If firmware changes display geometry, update/remove the crop argument in the launcher. No system display-size override is applied.

This is a streamed compositor image, not eye tracking or a reproduction of binocular perception. It has streaming delay and may omit protected/system content; do not use its timing to synchronize experimental touch. Check headset smoothness while streaming before a participant session.

## Finish screen, version 0.12.1 (24)

**Thank you for participating**

> The demo is complete.
> Please remove your headset.
> Your researcher will now conduct a brief interview about your experience.

Selecting Finish still closes the movement-data session before the interview. The app does not record interview audio.

Official tool documentation: https://github.com/Genymobile/scrcpy and https://github.com/Genymobile/scrcpy/blob/master/doc/video.md
