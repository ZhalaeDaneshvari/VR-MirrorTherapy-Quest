#!/usr/bin/env python3
"""Build separate probe; restore exact existing pilot assets/settings, including local edits."""
import pathlib, subprocess, tempfile
root = pathlib.Path(__file__).resolve().parents[1]
editor = '/Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app/Contents/MacOS/Unity'
# Refuse an open editor: it could save settings while the snapshot is restored.
processes = subprocess.check_output(['ps', '-axo', 'command'], text=True)
if any('/Unity.app/Contents/MacOS/Unity ' in p and str(root) in p for p in processes.splitlines()):
    raise SystemExit('Close this project in Unity before building the isolated probe.')
paths = list((root/'ProjectSettings').glob('*.asset'))
for folder in ['Assets/XR', 'Assets/Oculus', 'Assets/Scenes', 'Assets/Materials']:
    paths += [p for p in (root/folder).rglob('*') if p.is_file()]
probe_files = {'Assets/Scenes/QuestClinicianControllerProbe.unity', 'Assets/Scenes/QuestClinicianControllerProbe.unity.meta', 'Assets/Materials/ClinicianProbeMarker.mat', 'Assets/Materials/ClinicianProbeMarker.mat.meta'}
snapshot = {p: p.read_bytes() for p in paths if str(p.relative_to(root)) not in probe_files}
backup = pathlib.Path(tempfile.mkdtemp(prefix='controller-probe-settings-'))
for p, data in snapshot.items():
    dest=backup/p.relative_to(root); dest.parent.mkdir(parents=True, exist_ok=True); dest.write_bytes(data)
print('Recovery snapshot:', backup, flush=True)
try:
    result=subprocess.run([editor,'-batchmode','-quit','-projectPath',str(root),'-buildTarget','Android',
        '-executeMethod','QuestClinicianControllerSetup.BuildIsolatedProbe','-logFile','/private/tmp/controller-probe-build.log'],cwd=root)
finally:
    for p,data in snapshot.items(): p.write_bytes(data)
    assert all(p.read_bytes()==data for p,data in snapshot.items())
    print('Original pilot assets and settings restored byte-for-byte.',flush=True)
raise SystemExit(result.returncode)
