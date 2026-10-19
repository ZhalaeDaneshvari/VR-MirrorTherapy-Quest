#!/usr/bin/env python3
"""Copy only this Quest app's movement data to the Desktop; never delete headset originals."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile
import fcntl

APP_ROOT = '/sdcard/Android/data/org.mirrortherapy.questprototype.bodyavatar/files/MovementData'
DEFAULT_ADB = '/Applications/Unity/Hub/Editor/6000.6.2f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb'
SESSION = re.compile(r'^\d{8}T\d{9}Z-[0-9a-f]{32}$')
FILES = re.compile(r'^(session\.json|closed\.json|movement-\d{6}\.jsonl)$')

def checked_record_count(path):
    count = 0
    with path.open(encoding='utf-8') as stream:
        for line in stream:
            record = json.loads(line)
            if not isinstance(record, dict):
                raise ValueError('Non-object record')
            count += 1
    return count

def sync(adb, destination, serial=None):
    destination.mkdir(parents=True, exist_ok=True)
    with (destination / '.sync.lock').open('w') as lock:
        try:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            return
        def run(args):
            return subprocess.run([adb] + args, check=True, capture_output=True, text=True, timeout=90).stdout
        devices = [line.split()[0] for line in run(['devices']).splitlines()[1:] if len(line.split()) >= 2 and line.split()[1] == 'device']
        if serial:
            devices = [item for item in devices if item == serial]
        for device in devices:
            base = ['-s', device]
            try:
                names = run(base + ['shell', 'find', APP_ROOT, '-type', 'f']).splitlines()
            except subprocess.CalledProcessError:
                continue  # No recording folder on this device yet.
            sessions = set()
            # Immutable, closed chunks only. An active writer's .partial is never called complete.
            for remote in sorted(names):
                if not remote.startswith(APP_ROOT + '/'):
                    continue
                parts = remote[len(APP_ROOT) + 1:].split('/')
                if len(parts) != 3 or not re.fullmatch(r'P(0[1-9]|1[0-9]|20)', parts[0]) or not SESSION.fullmatch(parts[1]) or not FILES.fullmatch(parts[2]):
                    continue
                local = destination.joinpath(*parts)
                sessions.add(local.parent)
                digest = run(base + ['shell', 'sha256sum', remote]).split()[0]
                if not re.fullmatch(r'[0-9a-f]{64}', digest):
                    raise ValueError('Invalid device checksum')
                if local.exists():
                    with local.open('rb') as existing:
                        local_digest = hashlib.file_digest(existing, 'sha256').hexdigest()
                    if local_digest != digest:
                        raise ValueError(f'Local/device conflict; kept existing file: {local}')
                    continue
                local.parent.mkdir(parents=True, exist_ok=True)
                fd, tempname = tempfile.mkstemp(prefix='.transfer-', dir=local.parent)
                os.close(fd)
                temp = Path(tempname)
                try:
                    run(base + ['pull', remote, str(temp)])
                    with temp.open('rb') as stream:
                        actual = hashlib.file_digest(stream, 'sha256').hexdigest()
                    if actual != digest:
                        raise ValueError('Transfer checksum mismatch; retry next sync')
                    if local.suffix == '.jsonl':
                        checked_record_count(temp)
                    else:
                        json.loads(temp.read_text())
                    with temp.open('rb') as stream:
                        os.fsync(stream.fileno())
                    os.replace(temp, local)
                    print(f'Copied {local.relative_to(destination)}', flush=True)
                finally:
                    temp.unlink(missing_ok=True)
            for folder in sessions:
                marker = folder / 'closed.json'
                if not marker.exists():
                    continue
                manifest = json.loads(marker.read_text())
                expected = [folder / f'movement-{i:06d}.jsonl' for i in range(1, manifest['chunks'] + 1)]
                if all(path.exists() for path in expected):
                    records = sum(checked_record_count(path) for path in expected)
                    if records != manifest['records']:
                        raise ValueError(f'Record count mismatch: {folder}')
                    status = {'transfer': 'verified', 'recordingStatus': manifest['status'], 'records': records, 'chunks': len(expected), 'reason': manifest['reason']}
                    result = folder / 'TRANSFER_VERIFIED.json'
                    if not result.exists():
                        result.write_text(json.dumps(status, indent=2) + '\n')

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--destination', type=Path, default=Path.home() / 'Desktop' / 'Research Movement Data Demo')
    parser.add_argument('--adb', default=DEFAULT_ADB)
    parser.add_argument('--serial')
    args = parser.parse_args()
    try:
        sync(args.adb, args.destination, args.serial)
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        raise SystemExit(f'Transfer incomplete; originals retained. {error}')
