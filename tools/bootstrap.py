#!/usr/bin/env python3
"""Install the pinned Godot .NET editor and templates; requires the .NET SDK."""
import hashlib
import os
from pathlib import Path
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
VERSION = '4.5.2-stable'
ASSETS = {
    'linux_x86_64.zip': '9197371d3c4ceebb3ce750f8f4274a1b6f3eb75f25f17eb94fea9c1746081154',
    'win64.zip': '1d7062418ae29cbd29f5f1ad95e6f13f8ac6cb2d6effd673d3be1b3398380887',
    'export_templates.tpz': 'ed196ee69204a829176129878e48916fc30370941340558ba38db8210ff92dcb',
}

def download(suffix):
    name = f'Godot_v{VERSION}_mono_{suffix}'
    path = ROOT / '.tools/downloads' / name
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists():
        urllib.request.urlretrieve(f'https://github.com/godotengine/godot-builds/releases/download/{VERSION}/{name}', path)
    if hashlib.sha256(path.read_bytes()).hexdigest() != ASSETS[suffix]:
        raise RuntimeError(f'Godot download checksum failed: {name}')
    return path

if __name__ == '__main__':
    target = 'win64.zip' if os.name == 'nt' else 'linux_x86_64.zip'
    editor = ROOT / '.tools/godot'
    with zipfile.ZipFile(download(target)) as archive:
        archive.extractall(editor)
    executable = next(editor.glob('**/*mono_win64_console.exe' if os.name == 'nt' else '**/*mono_linux.x86_64'))
    if os.name != 'nt': executable.chmod(0o755)
    data = Path(os.environ['APPDATA']) if os.name == 'nt' else Path(os.environ.get('XDG_DATA_HOME', str(Path.home()/'.local/share')))
    templates = data / 'Godot/export_templates/4.5.2.stable.mono'
    templates.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(download('export_templates.tpz')) as archive:
        for item in archive.infolist():
            if item.is_dir(): continue
            (templates / Path(item.filename).name).write_bytes(archive.read(item))
    print(f'GODOT_BIN={executable}')
