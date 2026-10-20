#!/bin/zsh
# Read-only USB display streaming; no recording or device input.
export ADB="${ADB:-/Applications/Unity/Hub/Editor/6000.6.2f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb}"
SCRCPY="${SCRCPY:-/opt/homebrew/bin/scrcpy}"
if [[ ! -x "$ADB" || ! -x "$SCRCPY" ]]; then
  echo "Missing adb or scrcpy. See RESEARCHER_LIVE_VIEW.md for setup."
  read "?Press Return to close. "
  exit 1
fi
echo "Wake and wear the Quest; connect USB and allow USB debugging."
echo "This window shows the actual headset display. Close it to stop viewing."
echo "No video/audio is recorded. Participant controls remain in the headset."
"$SCRCPY" --select-usb --no-control --no-audio --crop=2064:2208:0:0 --max-size=1200 --hwdec=disabled --render-driver=software --window-x=100 --window-y=80 --window-width=700 --window-height=749 --max-fps=30 --window-title="Quest Researcher Live View" "$@"
result=$?
if (( result != 0 )); then
  echo "Check that exactly one headset is connected, awake and USB-authorized."
  read "?Press Return to close. "
fi
exit $result
