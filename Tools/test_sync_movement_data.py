"""Synthetic transfer tests: no participant records or connected headset required."""
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
from unittest.mock import patch
spec=importlib.util.spec_from_file_location('sync',Path(__file__).with_name('sync_movement_data.py'))
sync=importlib.util.module_from_spec(spec);spec.loader.exec_module(sync)
class TransferTests(unittest.TestCase):
    def test_verified_idempotent_transfer_and_conflict(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);source=root/'source';source.mkdir();dest=root/'laptop'
            session='20261019T123456123Z-'+'a'*32
            remote=sync.APP_ROOT+'/P01/'+session+'/'
            files={'session.json':'{"participant":"P01"}', 'movement-000001.jsonl':'{"type":"sample"}\n',
                   'closed.json':json.dumps(dict(status='closed',reason='test',records=1,chunks=1)),'movement-000002.jsonl.partial':'unfinished'}
            for name,text in files.items():(source/name).write_text(text)
            def fake(args,**kwargs):
                if args[1:]==['devices']:text='List of devices attached\ntest\tdevice\n'
                elif 'find' in args:text='\n'.join(remote+name for name in files)
                elif 'sha256sum' in args:text=hashlib.sha256((source/Path(args[-1]).name).read_bytes()).hexdigest()+' file'
                elif 'pull' in args:shutil.copyfile(source/Path(args[-2]).name,args[-1]);text=''
                else:raise AssertionError(args)
                return subprocess.CompletedProcess(args,0,text,'')
            with patch.object(sync.subprocess,'run',fake):
                sync.sync('adb',dest);sync.sync('adb',dest)
                target=dest/'P01'/session
                self.assertTrue((target/'TRANSFER_VERIFIED.json').exists())
                self.assertFalse(any(target.glob('*.partial')))
                (target/'movement-000001.jsonl').write_text('user edit')
                with self.assertRaises(ValueError):sync.sync('adb',dest)
                self.assertEqual((target/'movement-000001.jsonl').read_text(),'user edit')
if __name__=='__main__':unittest.main()
