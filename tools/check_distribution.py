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
# Unique versions keep an earlier valid cache from bypassing failure scenarios.
stamp = int(time.time())
day, iteration = stamp // 86400, (stamp % 86400) // 2
# Each component also fits the .NET assembly version's 16-bit fields.
initial_version, stable_version, beta_version = f'0.{day}.{iteration}', f'0.{day}.{iteration+1}', f'0.{day+1}.{iteration}-beta.1'
for version,channel in [(initial_version,'stable'),(stable_version,'stable'),(beta_version,'beta')]:
    subprocess.run([sys.executable,str(ROOT/'tools/dev.py'),'pack','--target',a.target,'--version',version,'--channel',channel,'--feed',str(feed),'--output-dir',str(feed)],check=True)
    if version==initial_version:
        source=next(feed.glob('*Setup.exe' if os.name=='nt' else '*.AppImage'))
        initial=base/source.name
        shutil.copy2(source,initial)
subprocess.run([sys.executable,str(ROOT/'tools/test_launcher_updates.py'),'--feed',str(feed),'--initial',str(initial),'--initial-version',initial_version,'--stable-version',stable_version,'--beta-version',beta_version,'--install-dir',str(base/'installed with spaces')],check=True)
