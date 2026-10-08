#!/usr/bin/env python3
"""Researcher controls for the M3/M4 development APKs; dimensions are centimetres."""
import argparse
import json
import math
import pathlib
import subprocess
import tempfile


DEFAULT_ADB = '/Applications/Unity/Hub/Editor/6000.6.2f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb'
FIELDS = {
    'table-height': ('tableHeight', 45, 110),
    'table-width': ('tableWidth', 60, 200),
    'table-depth': ('tableDepth', 35, 120),
    'near-edge': ('tableNearEdge', 5, 65),
    'shoulder-width': ('shoulderWidth', 25, 60),
    'shoulder-drop': ('shoulderDrop', 12, 40),
    'upper-arm': ('upperArmLength', 18, 45),
    'forearm': ('forearmLength', 18, 40),
}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--app', choices=['seated', 'mirror', 'bodyavatar'], default='mirror', help='Default: M4 mirror; seated=M3, bodyavatar=body-driven arms')
    parser.add_argument('--arm-fullness', type=float, help='Visual arm thickness multiplier, 0.8–1.8; never changes tracked hand targets')
    parser.add_argument('--table-support', choices=['on', 'off'], help='Estimated support-aware elbow pose near the table')
    parser.add_argument('--condition', choices=['normal', 'left-to-right', 'right-to-left'])
    parser.add_argument('--source-hand', choices=['show', 'hide'], help='In a mirrored condition show both hands or only the opposite hand')
    parser.add_argument('--adb', default=DEFAULT_ADB)
    parser.add_argument('--serial', help='Required when several devices are connected')
    parser.add_argument('--align', action='store_true', help='Capture body frame NOW: wearer must be seated and looking forward')
    parser.add_argument('--confirm-table', action='store_true', help='Researcher has checked alignment against a real table')
    for flag in FIELDS:
        parser.add_argument('--' + flag + '-cm', type=float)
    args = parser.parse_args()
    if args.app == 'bodyavatar':
        if args.table_support is not None or any(getattr(args, name) is not None for name in ['shoulder_width_cm','shoulder_drop_cm','upper_arm_cm','forearm_cm']):
            parser.error('Body-driven arms use provider joints, not legacy IK dimensions or table support. Table position and arm fullness remain adjustable.')
    REMOTE = "/sdcard/Android/data/org.mirrortherapy.questprototype." + args.app + "/files/seated-setup.json"
    adb = [args.adb] + (['-s', args.serial] if args.serial else [])
    def run(*parts):
        return subprocess.check_output(adb + list(parts), text=True, stderr=subprocess.STDOUT)
    try:
        data = json.loads(run('shell', 'cat', REMOTE))
        changed = False
        for flag, (key, low, high) in FIELDS.items():
            if key not in data:
                parser.error('Incomplete device settings; restart the seated app first.')
            value = getattr(args, flag.replace('-', '_') + '_cm')
            if value is not None:
                if not math.isfinite(value) or not low <= value <= high:
                    parser.error(f'{flag} must be {low}–{high} cm')
                data[key] = value / 100
                changed = True
        if changed or args.align:
            data['tableAlignmentConfirmed'] = False
        if args.confirm_table:
            data['tableAlignmentConfirmed'] = True
        appearance_changed = args.arm_fullness is not None or args.table_support is not None
        if args.arm_fullness is not None:
            if not math.isfinite(args.arm_fullness) or not .8 <= args.arm_fullness <= 1.8:
                parser.error('arm-fullness must be between 0.8 and 1.8')
            data['armFullness'] = args.arm_fullness
        if args.table_support is not None:
            data['tableSupportEnabled'] = args.table_support == 'on'
        condition_changed = args.condition is not None or args.source_hand is not None
        if condition_changed and 'movementCondition' not in data:
            parser.error('This installed app does not support movement conditions. Use the M4 mirror app.')
        if args.condition is not None:
            data['movementCondition'] = ['normal', 'left-to-right', 'right-to-left'].index(args.condition)
        if args.source_hand is not None:
            data['showSourceHand'] = args.source_hand == 'show'
        data['requestAlignment'] = args.align
        if changed or args.align or args.confirm_table or condition_changed or appearance_changed:
            with tempfile.TemporaryDirectory(prefix='quest-seated-') as tmp:
                path = pathlib.Path(tmp) / 'seated-setup.json'
                path.write_text(json.dumps(data, indent=2) + '\n')
                run('push', str(path), REMOTE + '.pending')
                # ADB-created files otherwise deny the app write access when it
                # consumes requestAlignment. Set permissions BEFORE publication.
                run('shell', 'chmod', '666', REMOTE + '.pending')
                run('shell', 'mv', REMOTE + '.pending', REMOTE)
            print('Settings delivered. Check the headset status for acceptance; delivery is not calibration verification.')
        print(json.dumps(data, indent=2))
    except (subprocess.CalledProcessError, OSError, ValueError) as exc:
        parser.exit(1, 'Could not access settings. Launch the seated app and authorize USB debugging first.\n' + str(exc) + '\n')

if __name__ == '__main__':
    main()
