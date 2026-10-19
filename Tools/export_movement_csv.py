#!/usr/bin/env python3
"""Export a session's head and all physical/mapped/displayed hand joint poses as long-format CSV."""
import argparse
import csv
import json
from pathlib import Path

def export(session, output):
    if not (session/'closed.json').exists():
        raise ValueError('Session is still recording or interrupted. Inspect/recover it before exporting.')
    metadata=json.loads((session/'session.json').read_text())
    with output.open('x',newline='') as stream:
        fields=['participant','session','sample','monotonic_seconds','delta_seconds','stage','movement','layer','side','joint','valid','x_m','y_m','z_m','qx','qy','qz','qw']
        writer=csv.DictWriter(stream,fieldnames=fields);writer.writeheader()
        for path in sorted(session.glob('movement-*.jsonl')):
            with path.open() as records:
                for line in records:
                    row=json.loads(line)
                    if row.get('type')!='sample':continue
                    common=dict(participant=row['participant'],session=row['session'],sample=row['sample'],monotonic_seconds=row['monotonicSeconds'],delta_seconds=row['deltaSeconds'],stage=row['stage'],movement=row['movement'])
                    def pose(layer,side,joint,p):
                        valid=p['valid'];position=p['position'];rotation=p['rotation']
                        writer.writerow(dict(common,layer=layer,side=side,joint=joint,valid=valid,
                            x_m=position['x'] if valid else '',y_m=position['y'] if valid else '',z_m=position['z'] if valid else '',
                            qx=rotation['x'] if valid else '',qy=rotation['y'] if valid else '',qz=rotation['z'] if valid else '',qw=rotation['w'] if valid else ''))
                    pose('physical','head','head',row['headInTracking'])
                    for layer in ('physical','mapped','displayed'):
                        for side in ('Left','Right'):
                            for name,p in zip(metadata['handJointNames'],row[layer+side]['joints']):pose(layer,side,name,p)
    print(output)

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('session',type=Path);parser.add_argument('--output',type=Path)
    args=parser.parse_args();export(args.session,args.output or args.session/'movement-joints.csv')
