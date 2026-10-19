import csv
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
spec=importlib.util.spec_from_file_location('exporter',Path(__file__).with_name('export_movement_csv.py'))
exporter=importlib.util.module_from_spec(spec);spec.loader.exec_module(exporter)
class ExportTest(unittest.TestCase):
    def test_invalid_poses_are_blank_and_output_not_overwritten(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory);(root/'closed.json').write_text('{}');(root/'session.json').write_text(json.dumps({'handJointNames':[str(i) for i in range(26)]}))
            pose={'valid':False,'position':{'x':0,'y':0,'z':0},'rotation':{'x':0,'y':0,'z':0,'w':1}}
            record=dict(type='sample',participant='P01',session='test',sample=1,monotonicSeconds=1,deltaSeconds=.033,stage='Running',movement='Normal',headInTracking=pose)
            for layer in ('physical','mapped','displayed'):
                for side in ('Left','Right'):record[layer+side]={'joints':[pose]*26}
            (root/'movement-000001.jsonl').write_text(json.dumps(record)+'\n');output=root/'test.csv';exporter.export(root,output)
            with output.open() as stream:rows=list(csv.DictReader(stream))
            self.assertEqual(len(rows),157);self.assertTrue(all(row['x_m']=='' for row in rows))
            with self.assertRaises(FileExistsError):exporter.export(root,output)
if __name__=='__main__':unittest.main()
