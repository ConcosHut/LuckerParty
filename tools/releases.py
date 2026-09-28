#!/usr/bin/env python3
"""Prepare and publish complete, versioned GitHub releases."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
REPOSITORY = 'ConcosHut/LuckerParty'

def gh(*args, capture=False):
    return subprocess.run(['gh',*args],cwd=ROOT,check=True,text=True,capture_output=capture)

def releases():
    return json.loads(gh('api',f'repos/{REPOSITORY}/releases?per_page=100',capture=True).stdout)

def metadata(branch, number):
    base = json.loads((ROOT/'version.json').read_text())['version']
    channel = 'stable' if branch == 'release' else 'beta'
    version = base if channel == 'stable' else f'{base}-beta.{number}'
    if os.environ.get('CHECKS_ONLY') != 'true' and any(r['tag_name'] == 'v'+version for r in releases()):
        raise RuntimeError(f'v{version} already exists. Advance version.json before Stable promotion.')
    return channel,version

def seed(channel, platform, output):
    output.mkdir(parents=True,exist_ok=True)
    prior = next((r for r in releases() if not r['draft'] and r['prerelease'] == (channel=='beta') and any(a['name']==f'releases.{platform}-{channel}.json' for a in r['assets'])),None)
    if prior:
        # One previous full package is enough to generate the next delta.
        assets = [a for a in prior['assets'] if a['name'].endswith(f'-{platform}-{channel}-full.nupkg')]
        if assets:
            newest = max(assets,key=lambda a:a['id'])
            gh('release','download',prior['tag_name'],'--repo',REPOSITORY,'--pattern',newest['name'],'--dir',str(output))
            gh('release','download',prior['tag_name'],'--repo',REPOSITORY,'--pattern',f'releases.{platform}-{channel}.json','--dir',str(output))

def publish(channel, version, commit, directory):
    assets = [p for p in directory.rglob('*') if p.is_file()]
    names = [p.name for p in assets]
    if len(set(names)) != len(names): raise RuntimeError('Duplicate cross-platform release asset names')
    for platform in ['win-x64','linux-x64']:
        suffix = 'Windows' if platform=='win-x64' else 'Linux'
        installer = f'LuckerParty.{suffix}-{platform}-{channel}' + ('-Setup.exe' if platform=='win-x64' else '.AppImage')
        required = [f'releases.{platform}-{channel}.json', f'LuckerParty.{suffix}-{version}-{platform}-{channel}-full.nupkg', installer]
        for name in required:
            if name not in names: raise RuntimeError('Missing release asset: '+name)
    checksums = directory/'SHA256SUMS.txt'
    checksums.write_text(''.join(f'{hashlib.sha256(p.read_bytes()).hexdigest()}  {p.name}\n' for p in sorted(assets)),encoding='utf-8')
    tag = 'v'+version
    if any(r['tag_name']==tag for r in releases()): raise RuntimeError('Release already exists: '+tag)
    gh('release','create',tag,'--repo',REPOSITORY,'--target',commit,'--draft','--title',f'Lucker Party {version} ({channel.title()})','--notes-file',str(ROOT/'docs/release-notes.md'),*(['--prerelease'] if channel=='beta' else []))
    gh('release','upload',tag,'--repo',REPOSITORY,*[str(p) for p in assets],str(checksums))
    uploaded=json.loads(gh('api',f'repos/{REPOSITORY}/releases/tags/{tag}',capture=True).stdout)
    expected={p.name:p.stat().st_size for p in [*assets,checksums]}
    actual={a['name']:a['size'] for a in uploaded['assets']}
    if actual != expected: raise RuntimeError('Draft asset verification failed; draft retained for inspection')
    gh('release','edit',tag,'--repo',REPOSITORY,'--draft=false','--latest='+('false' if channel=='beta' else 'true'))

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('command',choices=['metadata','seed','publish'])
    p.add_argument('--branch',choices=['master','release'],default='master')
    p.add_argument('--run-number',default='0')
    p.add_argument('--channel',choices=['stable','beta'])
    p.add_argument('--version')
    p.add_argument('--commit')
    p.add_argument('--platform',choices=['win-x64','linux-x64'])
    p.add_argument('--directory',type=Path)
    a=p.parse_args()
    if a.command=='metadata':
        channel,version=metadata(a.branch,a.run_number)
        print(f'channel={channel}\nversion={version}')
    elif a.command=='seed':seed(a.channel,a.platform,a.directory)
    else:publish(a.channel,a.version,a.commit,a.directory)
