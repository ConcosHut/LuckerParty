#!/usr/bin/env python3
"""Exercise real development installations and HTTP updates. Uses Dev package IDs only."""
import argparse
import functools
import http.server
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import threading
import time

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--feed', type=Path, required=True)
parser.add_argument('--initial', type=Path, required=True, help='Older AppImage or Windows Setup.exe')
parser.add_argument('--initial-version', required=True)
parser.add_argument('--stable-version', required=True)
parser.add_argument('--beta-version', required=True)
parser.add_argument('--install-dir', type=Path, required=True)
args = parser.parse_args()
feed = args.feed.resolve()
windows = os.name == 'nt'
platform = 'win-x64' if windows else 'linux-x64'
package = 'LuckerParty.Dev.Windows' if windows else 'LuckerParty.Dev.Linux'
data = (Path(os.environ['LOCALAPPDATA']) if windows else Path(os.environ.get('XDG_DATA_HOME',str(Path.home()/'.local/share')))) / 'LuckerParty' / package
args.install_dir.mkdir(parents=True, exist_ok=True)
if windows:
    subprocess.run([str(args.initial.resolve()), '--silent', '--installto', str(args.install_dir.resolve())],check=True,timeout=120)
    if not (args.install_dir / 'Lucker Party Dev.exe').is_file():
        raise RuntimeError('Installer did not create the stable shortcut entry point')
    # The shortcut stub returns before the child and does not propagate its
    # exit code. Use current for bounded assertions, retaining the same update
    # installation layout; ordinary players still use the shortcut.
    executable = args.install_dir.resolve() / 'current/LuckerParty.Launcher.exe'
else:
    executable = args.install_dir.resolve() / 'Lucker Party.AppImage'
    shutil.copy2(args.initial, executable)
    executable.chmod(0o755)

class Handler(http.server.SimpleHTTPRequestHandler):
    mode = 'normal'
    def log_message(self, *_): pass
    def do_GET(self):
        if self.path.endswith('.nupkg') and self.mode != 'normal':
            self.send_response(200)
            self.send_header('Content-Length','1024' if self.mode == 'interrupt' else '8')
            self.end_headers()
            self.wfile.write(b'notazip!')
            self.wfile.flush()
            if self.mode == 'interrupt': self.connection.shutdown(socket.SHUT_RDWR)
            self.connection.close()
            return
        super().do_GET()
server = http.server.ThreadingHTTPServer(('127.0.0.1',0),functools.partial(Handler,directory=str(feed)))
threading.Thread(target=server.serve_forever,daemon=True).start()
url = f'http://127.0.0.1:{server.server_port}'

def invoke(*extra, expected=0, version=None, ready_version=None):
    log = data / 'launcher.log'
    offset = len(log.read_text(encoding='utf-8')) if log.exists() else 0
    game_log = data / 'game.log'
    game_offset = len(game_log.read_text(encoding='utf-8')) if game_log.exists() else 0
    result = subprocess.run([str(executable),'--headless',*extra],capture_output=True,text=True,errors='replace',timeout=120)
    if result.returncode != expected: raise RuntimeError(f'Unexpected exit {result.returncode}: {result.stdout} {result.stderr}')
    if ready_version:
        end = time.monotonic()+90
        while time.monotonic() < end:
            text = log.read_text(encoding='utf-8')[offset:] if log.exists() else ''
            if f'LAUNCHER_READY version={ready_version} ' in text:
                if 'GAME_STARTED' in text: raise RuntimeError('Startup preparation launched a game')
                print(text.strip(),flush=True)
                return
            time.sleep(.25)
        raise RuntimeError('Updated launcher failed to become ready: '+text)
    elif version:
        end = time.monotonic()+90
        while time.monotonic() < end:
            text = log.read_text(encoding='utf-8')[offset:] if log.exists() else ''
            if f'GAME_EXITED version={version} code=0' in text:
                if text.count(f'GAME_STARTED version={version} ') != 1: raise RuntimeError('Game launched more than once')
                game_text = game_log.read_text(encoding='utf-8')[game_offset:]
                if f'GAME_BUILD: version={version}' not in game_text: raise RuntimeError('In-game display version differs from the installed package')
                print(text.strip(),flush=True)
                return
            time.sleep(.25)
        raise RuntimeError('Updated game failed to complete: '+text)
    else: print((log.read_text(encoding='utf-8')[offset:] if log.exists() else result.stdout).strip(),flush=True)

try:
    invoke('--no-update','--game-smoke',version=args.initial_version)
    if windows:
        # The exported Windows GUI executable must also expose its redirected pipe.
        subprocess.run([sys.executable, str(Path(__file__).with_name('test_launcher_control.py')),
                        str(args.install_dir.resolve() / 'current/game/LuckerParty.exe'),
                        '--headless', '--', '--launcher-control'], check=True, timeout=45)
    preferences_path = data / 'preferences.json'
    preferences = json.loads(preferences_path.read_text(encoding='utf-8'))
    preferences['allowMultipleInstances'] = True
    preferences_path.write_text(json.dumps(preferences),encoding='utf-8')
    sentinel = data / 'saved-sentinel.txt'
    sentinel.write_text('preserve-me')
    invoke('--feed','http://127.0.0.1:1','--check-only',expected=1)
    # Fresh package identities avoid a previously cached, valid B skipping the
    # simulated failure; rerun with a fresh initial/version set when needed.
    Handler.mode = 'interrupt'
    invoke('--feed',url,'--check-only',expected=1)
    Handler.mode = 'corrupt'
    invoke('--feed',url,'--check-only',expected=1)
    invoke('--no-update','--game-smoke',version=args.initial_version)
    Handler.mode = 'normal'
    invoke('--feed',url,'--channel','stable','--prepare-only',ready_version=args.stable_version)
    invoke('--no-update','--game-smoke',version=args.stable_version)
    invoke('--feed',url,'--channel','beta','--game-smoke',version=args.beta_version)
    invoke('--feed',url,'--channel','stable','--game-smoke',version=args.stable_version)
    preferences = json.loads((data/'preferences.json').read_text(encoding='utf-8'))
    if preferences['channel'] != 'stable' or not preferences.get('allowMultipleInstances') or sentinel.read_text() != 'preserve-me': raise RuntimeError('Preferences/saved data changed')
    if preferences.get('pendingLaunch') is not None or preferences.get('game') is not None or preferences.get('games'): raise RuntimeError('Session intent was not consumed')
    print('INSTALLED_UPDATE_CHECK_PASS: install, offline, interrupted/corrupt download, HTTP A->B, Stable->Beta->older Stable, one launch and retained settings',flush=True)
finally:
    server.shutdown()
