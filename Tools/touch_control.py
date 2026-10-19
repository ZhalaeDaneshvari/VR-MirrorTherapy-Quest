#!/usr/bin/env python3
"""USB researcher control for the manual stroke demo. Requires the app already running."""
import argparse
import json
import pathlib
import subprocess
import tempfile
import time
import uuid

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('action', choices=['start', 'stop'])
parser.add_argument('--side', choices=['left', 'right'], default='left', help='Physical arm the clinician will touch')
parser.add_argument('--mapping', choices=['cross', 'congruent'], default='cross')
parser.add_argument('--duration', type=float, default=4)
parser.add_argument('--serial')
parser.add_argument('--adb', default='/Applications/Unity/Hub/Editor/6000.6.2f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb')
args = parser.parse_args()
if not 2 <= args.duration <= 8:
    parser.error('duration must be between 2 and 8 seconds')
base = [args.adb] + (['-s', args.serial] if args.serial else [])
folder = '/sdcard/Android/data/org.mirrortherapy.questprototype.bodyavatar/files/'
command_id = uuid.uuid4().hex
payload = dict(id=command_id, action=args.action, physicalSide=0 if args.side == 'left' else 1,
               crossLimb=args.mapping == 'cross', duration=args.duration)
with tempfile.TemporaryDirectory(prefix='quest-touch-') as directory:
    local = pathlib.Path(directory) / 'command.json'
    local.write_text(json.dumps(payload))
    subprocess.run(base + ['push', str(local), folder + 'touch-command.tmp'], check=True)
    subprocess.run(base + ['shell', 'mv', folder + 'touch-command.tmp', folder + 'touch-command.json'], check=True)
    for _ in range(20):
        result = subprocess.run(base + ['shell', 'cat', folder + 'touch-command-result.json'], capture_output=True, text=True)
        try:
            reply = json.loads(result.stdout)
        except json.JSONDecodeError:
            reply = {}
        if reply.get('id') == command_id:
            print(json.dumps(reply, indent=2))
            raise SystemExit(0 if reply.get('status') == 'accepted' else 1)
        time.sleep(.1)
raise SystemExit('No acknowledgment. Is the app running and headset awake? Do not assume a stroke started.')
