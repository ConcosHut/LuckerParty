#!/usr/bin/env python3
"""Build three Dev packages and exercise the actual installed updater on this OS."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--target',choices=['windows','linux'],default='windows' if os.name=='nt' else 'linux')
a=p.parse_args()
if (a.target=='windows') != (os.name=='nt'):p.error('Run installation tests on the matching OS (or use the Windows SSH workflow).')
base = ROOT/'artifacts/update-checks'/str(time.time_ns())
feed = base/'feed'
for version,channel in [('0.0.1','stable'),('0.0.2','stable'),('0.1.0-beta.1','beta')]:
    subprocess.run([sys.executable,str(ROOT/'tools/dev.py'),'pack','--target',a.target,'--version',version,'--channel',channel,'--feed',str(feed),'--output-dir',str(feed)],check=True)
    if version=='0.0.1':
        source=next(feed.glob('*Setup.exe' if os.name=='nt' else '*.AppImage'))
        initial=base/source.name
        shutil.copy2(source,initial)
subprocess.run([sys.executable,str(ROOT/'tools/test_launcher_updates.py'),'--feed',str(feed),'--initial',str(initial),'--initial-version','0.0.1','--stable-version','0.0.2','--beta-version','0.1.0-beta.1','--install-dir',str(base/'installed with spaces')],check=True)
