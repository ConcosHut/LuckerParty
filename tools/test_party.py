#!/usr/bin/env python3
"""Bounded, real-process ENet party checks; optional rendered UI captures."""
import argparse
import json
import os
from pathlib import Path
import socket
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--godot')
parser.add_argument('--executable')
parser.add_argument('--graphical', action='store_true')
parser.add_argument('--output', type=Path, default=ROOT / 'artifacts/party-checks')
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)
(args.output / 'result.json').unlink(missing_ok=True)
if args.executable:
    base = [str(Path(args.executable).resolve())]
elif args.godot:
    base = [args.godot, '--path', str(ROOT / 'src/LuckerParty.Godot')]
else:
    parser.error('Provide --godot or --executable')
with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
    sock.bind(('127.0.0.1', 0))
    port = sock.getsockname()[1]
peers = []
checks = []


class Peer:
    def __init__(self, label):
        self.label = label
        self.graphical = args.graphical and label == 'host'
        self.control = args.output / (label + '-control.json')
        self.probe = args.output / (label + '-probe.json')
        self.revision = 0
        for path in (self.control, self.probe):
            path.unlink(missing_ok=True)
        self.log = (args.output / (label + '.log')).open('w', encoding='utf-8')
        self.process = subprocess.Popen([*base, *([] if self.graphical else ['--headless']), '--',
            '--control-file', str(self.control.resolve()), '--probe-file', str(self.probe.resolve()),
            '--exit-after', '120'], stdout=self.log, stderr=subprocess.STDOUT)
        peers.append(self)

    def send(self, **values):
        self.revision += 1
        values.update(revision=self.revision, port=port)
        temporary = self.control.with_suffix('.tmp')
        temporary.write_text(json.dumps(values), encoding='utf-8')
        os.replace(temporary, self.control)

    def command(self, command, value=0, **extra):
        self.send(action='party', command=command, value=value, **extra)

    def click(self, label):
        self.send(action='ui-click', command=label)

    def read(self):
        try:
            return json.loads(self.probe.read_text(encoding='utf-8'))
        except (FileNotFoundError, PermissionError, json.JSONDecodeError):
            return {}

    def state(self):
        return self.read().get('party') or {}

    def capture(self, name):
        if not self.graphical:
            return
        path = args.output / (name + '.png')
        path.unlink(missing_ok=True)
        self.send(action='capture', capturePath=str(path.resolve()))
        wait('Render ' + name, lambda: path.is_file())

    def stop(self):
        if self.process.poll() is None:
            self.send(action='quit')
            try:
                self.process.wait(timeout=8)
            except subprocess.TimeoutExpired:
                if os.name == 'nt':
                    # The Godot console executable owns a graphical child.
                    # Kill only this scenario's process tree, including that child.
                    subprocess.run(['taskkill', '/PID', str(self.process.pid), '/T', '/F'], capture_output=True, check=False)
                else:
                    self.process.kill()
                self.process.wait(timeout=8)
        self.log.close()


def wait(description, condition, timeout=15):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if condition():
            checks.append(description)
            print('PARTY_CHECK_PASS: ' + description, flush=True)
            return
        if any(peer.process.poll() not in (None, 0) for peer in peers):
            raise RuntimeError('A scenario process crashed; see party-checks logs')
        time.sleep(.04)
    raise RuntimeError(description + ' timed out\n' + json.dumps({p.label: p.read() for p in peers}, indent=2))


def member(peer, name):
    return next((m for m in peer.state().get('members', []) if m['name'] == name and m['connected']), {})


def phase(peer, value):
    return peer.state().get('phase') == value


try:
    host, alice, bob, observer = [Peer(name) for name in ('host', 'alice', 'bob', 'observer')]
    host.send(action='host', name='Host', party=True, fastParty=not args.graphical)
    wait('Party host opens a real lobby without a sandbox avatar', lambda: phase(host, 0) and len(host.read().get('players', [])) == 1 and host.read()['players'][0]['x'] is None)
    alice.send(action='join', name='Alice')
    bob.send(action='join', name='Bob')
    wait('Three peers agree on the party roster', lambda: all(len(p.state().get('members', [])) == 3 for p in (host, alice, bob)))
    alice.command('configure', 1)
    wait('A client cannot change host settings', lambda: alice.read().get('revision') == alice.revision and alice.state().get('throwCount') == 3)
    alice.command('color', 0)
    wait('A client cannot take another player color', lambda: alice.read().get('revision') == alice.revision and member(host, 'Alice').get('colorIndex') != 0)
    alice.command('role', 1)
    wait('Own spectator role replicates', lambda: member(host, 'Alice').get('role') == 1)
    alice.command('ready', 1)
    wait('Spectators cannot ready as players', lambda: alice.read().get('revision') == alice.revision and not member(host, 'Alice').get('ready'))
    alice.command('role', 0)
    wait('A spectator can rejoin players in lobby', lambda: member(host, 'Alice').get('role') == 0)
    for peer in (host, alice, bob):
        peer.click('Ready up')
    wait('Ready states agree across peers', lambda: all(m['ready'] for m in host.state()['members']) and not host.state()['startBlocker'])
    host.capture('lobby')
    alice.command('start')
    wait('A ready client still cannot start the party', lambda: alice.read().get('revision') == alice.revision and phase(host, 0))
    host.click('Start party')
    if args.graphical:
        wait('Instructions precede the first throw', lambda: phase(host, 2))
        host.capture('instructions')
    wait('Server advances all peers to the first RPS throw', lambda: all(phase(p, 3) for p in (host, alice, bob)))
    party_id = host.state()['partyId']
    host.click('Rock')
    wait('One locked hand remains private on every other peer', lambda: member(alice, 'Host').get('submitted') and host.state().get('yourChoice') == 0 and
         alice.state().get('yourChoice') is None and all(m['choice'] is None for m in alice.state()['members']))
    observer.send(action='join', name='Observer')
    wait('Mid-game join gets complete state as a spectator', lambda: phase(observer, 3) and member(observer, 'Observer').get('role') == 1 and len(observer.state().get('members', [])) == 4)
    observer.command('choice', 1)
    alice.command('choice', 1, partyId=party_id - 1)
    wait('Spectator and obsolete-party choices are rejected', lambda: observer.read().get('revision') == observer.revision and alice.read().get('revision') == alice.revision and
         not member(host, 'Observer').get('submitted') and not member(host, 'Alice').get('submitted'))
    host.capture('hidden-choice')
    alice.command('choice', 0)
    bob.command('choice', 2)
    wait('All hands reveal together after valid submissions', lambda: all(phase(p, 4) for p in (host, alice, bob)) and member(alice, 'Host').get('choice') == 0)
    for turn in (2, 3):
        wait('Shared RPS throw ' + str(turn), lambda: phase(host, 3) and host.state().get('throw') == turn)
        for peer, choice in ((host, 0), (alice, 0), (bob, 2)):
            peer.command('choice', choice)
        wait('Shared reveal ' + str(turn), lambda: all(phase(p, 4) for p in (host, alice, bob)))
    wait('Every peer agrees on points and the joint champions', lambda: all(phase(p, 6) and [member(p, n).get('score') for n in ('Host', 'Alice', 'Bob')] == [10, 10, 3] and
         len(p.state().get('winners', [])) == 2 for p in (host, alice, bob, observer)))
    host.capture('final-podium')
    alice.command('again')
    wait('Only host can return the party to lobby', lambda: alice.read().get('revision') == alice.revision and phase(host, 6))
    host.command('again')
    wait('Replay keeps connections and clears readiness', lambda: all(phase(p, 0) for p in (host, alice, bob, observer)) and all(not m['ready'] for m in host.state()['members']))
    host.command('configure', 1)
    wait('Host configures the next party', lambda: host.state().get('throwCount') == 1)
    for peer in (host, alice, bob):
        peer.command('ready', 1)
    wait('Second party is ready', lambda: not host.state().get('startBlocker', 'waiting'))
    host.command('start')
    wait('Second party resets scores with a fresh identity', lambda: phase(host, 3) and host.state()['partyId'] > party_id and all(m['score'] == 0 for m in host.state()['members']))
    host.command('choice', 1)
    alice.command('choice', 0)
    # Bob deliberately never submits. Server timers still progress with no player input.
    wait('Timeout completes and missing participation receives zero', lambda: all(phase(p, 6) for p in (host, alice, bob)) and
         [member(host, n)['score'] for n in ('Host', 'Alice', 'Bob')] == [10, 6, 0], timeout=25 if args.graphical else 15)
    host.command('again')
    wait('Second replay returns to lobby', lambda: phase(host, 0))
    for peer in (host, alice, bob):
        peer.command('ready', 1)
    wait('Third party is ready', lambda: not host.state().get('startBlocker', 'waiting'))
    host.command('start')
    wait('Third party starts without old choices', lambda: phase(host, 3) and all(not m['submitted'] for m in host.state()['members']))
    bob.send(action='leave')
    wait('Disconnect does not stall the remaining participants', lambda: not member(host, 'Bob') and phase(host, 3))
    alice.send(action='leave')
    wait('Below two active players cancels safely without scoring', lambda: phase(host, 0) and all(m['score'] == 0 for m in host.state()['members']))
    host.send(action='leave')
    wait('Host departure returns a spectator to the main menu', lambda: not observer.read().get('active', True))
    host.capture('main-menu')
    (args.output / 'result.json').write_text(json.dumps({'passed': True, 'checks': checks}, indent=2), encoding='utf-8')
    print('PARTY_NETWORK_CHECK_PASS: ' + str(len(checks)) + ' scenarios', flush=True)
finally:
    for peer in peers:
        peer.stop()
