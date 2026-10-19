#!/usr/bin/env python3
"""Install this laptop's local-only Quest data copier; requires Python 3.11+."""
import os
from pathlib import Path
import plistlib
import shlex
import shutil
import subprocess
import sys

home=Path.home()
destination=home/'Desktop'/'Research Movement Data Demo'
support=home/'Library'/'Application Support'/'Quest Research Data'
agent=home/'Library'/'LaunchAgents'/'org.questresearch.movement-sync.plist'
label='org.questresearch.movement-sync'
for directory in (destination,support,agent.parent):
    directory.mkdir(parents=True,exist_ok=True)
script=support/'sync_movement_data.py'
shutil.copyfile(Path(__file__).with_name('sync_movement_data.py'),script)
python=str(Path(sys.executable).resolve())
args=[python,str(script),'--destination',str(destination)]
config={'Label':label,'ProgramArguments':args,'RunAtLoad':True,'StartInterval':30,
        'StandardOutPath':str(destination/'transfer.log'),'StandardErrorPath':str(destination/'transfer-errors.log')}
if agent.exists():
    subprocess.run(['launchctl','bootout',f'gui/{os.getuid()}',str(agent)],capture_output=True)
agent.write_bytes(plistlib.dumps(config))
subprocess.run(['launchctl','enable',f'gui/{os.getuid()}/{label}'],check=True)
subprocess.run(['launchctl','bootstrap',f'gui/{os.getuid()}',str(agent)],check=True)
command=destination/'Sync Quest Data Now.command'
command.write_text('#!/bin/zsh\n'+shlex.join(args)+'\nresult=$?\necho "Transfer exit code: $result. Headset originals are retained."\necho "Press Return to close."\nread\n')
command.chmod(0o755)
(destination/'READ ME.txt').write_text('''QUEST MOVEMENT DATA DEMO

Select P01-P20 in the headset before starting. Each run has its own dated, unique session folder.
The local USB copier checks every 30 seconds while you are logged into this Mac.
Keep the Quest awake, connected with a data USB cable, and USB debugging authorized.
Disconnected? Data stays on the headset and transfers after reconnection.
Double-click "Sync Quest Data Now.command" to request an immediate copy.

P01/<session>/session.json: ID, software, coordinate system, joint names, timing and limitations.
movement-000001.jsonl etc: timestamped movement samples and experiment events.
closed.json: recording ended, reason, record count. "finished" differs from interrupted.
TRANSFER_VERIFIED.json: laptop checksums and record count verified. A failed recording is still marked failed.
A folder without closed.json is still recording or was interrupted. Do not call it complete.

Includes head direction, not eye tracking. Quest 3 does not track eyes.
Raw physical hand joints, remapped targets and displayed hand joints are separate.
Body joints are estimates. Nominal recording rate is 30 Hz; inspect actual timestamps/gaps.
Data is kept locally, not uploaded to GitHub. Back up this folder under your lab's approved process.
Never put participant names or identifying notes in the GitHub repository.

Transfer logs: transfer.log and transfer-errors.log.
Full instructions and optional CSV export: MOVEMENT_DATA.md in the project repository.
''')
print(f'Automatic local USB transfer installed: {destination}')
