#!/usr/bin/env python3
"""Manage box's private Windows integration VM (Linux host tooling only)."""
import argparse
import concurrent.futures
import errno
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import socket
import subprocess
import sys
import time
import zipfile
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_HOME = Path.home() / '.local/share/lucker-party/windows-vm'
TEMPLATES = ROOT / 'tools/windows-vm'


def run(command, **kwargs):
    return subprocess.run([str(x) for x in command], check=True, **kwargs)


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


class WindowsVm:
    def __init__(self, home):
        self.home = home.resolve()
        self.home.mkdir(parents=True, exist_ok=True, mode=0o700)
        self.env = os.environ.copy()
        local = self.home / 'host-tools/usr'
        self.env['PATH'] = f'{local / "bin"}:{self.env["PATH"]}'
        self.env['LD_LIBRARY_PATH'] = f'{local / "lib"}:{local / "lib/swtpm"}:' + self.env.get('LD_LIBRARY_PATH', '')
        self.env['QEMU_MODULE_DIR'] = str(local / 'lib/qemu')
        self.env['PYTHONPATH'] = ':'.join(str(p) for p in (local / 'lib').glob('python*/site-packages'))
        self.config = json.loads((self.home / 'config.json').read_text()) if (self.home / 'config.json').exists() else None

    def tool(self, name):
        result = shutil.which(name, path=self.env['PATH'])
        if not result:
            raise RuntimeError(f'Missing {name}; install host packages or run host-tools first.')
        return result

    def execute(self, command, **kwargs):
        return run(command, env=self.env, **kwargs)

    def host_tools(self):
        """Extract verified Arch packages locally; never change the package database."""
        downloads = self.home / 'downloads'
        downloads.mkdir(exist_ok=True)
        destination = self.home / 'host-tools'
        destination.mkdir(exist_ok=True)
        urls = run(['pacman', '-Sp', 'qemu-system-x86', 'qemu-img', 'swtpm', 'cdrkit', 'virt-firmware'], capture_output=True, text=True).stdout.splitlines()

        def fetch(url):
            package = downloads / url.rsplit('/', 1)[-1]
            if url.startswith('file://'):
                package = Path(url[7:])
            else:
                for suffix in ('', '.sig'):
                    target = Path(str(package) + suffix)
                    if not target.exists():
                        temporary = Path(str(target) + '.part')
                        run(['curl', '-fsSL', '--retry', '3', '-o', temporary, url + suffix])
                        temporary.replace(target)
            # Verify cached packages too, against the host's trusted pacman keyring.
            run(['pacman-key', '--verify', str(package) + '.sig', package], capture_output=True)
            return package

        with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
            packages = list(pool.map(fetch, urls))
        for package in packages:
            run(['bsdtar', '-xf', package, '-C', destination])
        self.execute([self.tool('qemu-system-x86_64'), '--version'])

    def prepare(self, iso, expected_hash, assets, ssh_port, vnc_port):
        if self.config or (self.home / 'windows.qcow2').exists():
            raise RuntimeError('VM already prepared; existing disks and credentials are preserved.')
        iso = iso.resolve()
        if digest(iso) != expected_hash.lower():
            raise RuntimeError('Windows ISO SHA-256 mismatch.')
        seed = self.home / 'seed'
        seed.mkdir(exist_ok=True, mode=0o700)
        for name in ('client-key', 'host-key'):
            key = self.home / name
            if not key.exists():
                run(['ssh-keygen', '-q', '-t', 'ed25519', '-N', '', '-f', key])
        password = secrets.token_urlsafe(30)
        xml = (TEMPLATES / 'autounattend.xml').read_text().replace('@PASSWORD@', escape(password))
        (seed / 'autounattend.xml').write_text(xml)
        (seed / 'autounattend.xml').chmod(0o600)
        for name in ('Provision.ps1', 'Run-Integration.ps1'):
            shutil.copy2(TEMPLATES / name, seed / name)
        shutil.copy2(self.home / 'client-key.pub', seed / 'authorized_keys')
        shutil.copy2(self.home / 'host-key', seed / 'ssh_host_ed25519_key')
        shutil.copy2(self.home / 'host-key.pub', seed / 'ssh_host_ed25519_key.pub')
        names = ['python-3.11.9-amd64.exe', 'dotnet-sdk-8.0.425-win-x64.zip',
                 'Godot_v4.5.2-stable_mono_win64.zip', 'templates.tpz', 'MinGit-2.56.0-64-bit.zip']
        hashes = {}
        for name in names:
            source = (assets / name).resolve()
            hashes[name] = digest(source)
            target = seed / name
            if not target.exists():
                try:
                    os.link(source, target)
                except OSError as error:
                    if error.errno != errno.EXDEV:
                        raise
                    shutil.copy2(source, target)
        (seed / 'assets.json').write_text(json.dumps(hashes))
        self.execute([self.tool('mkisofs'), '-quiet', '-J', '-r', '-V', 'LUCKER_SEED', '-o', self.home / 'seed.iso', seed])
        firmware = self.home / 'host-tools/usr/share/edk2/x64'
        if not firmware.exists():
            firmware = Path('/usr/share/edk2/x64')
        self.execute([self.tool('virt-fw-vars'), '--input', firmware / 'OVMF_VARS.4m.fd',
                      '--output', self.home / 'OVMF_VARS.fd', '--enroll-microsoft', '--secure-boot'])
        self.execute([self.tool('qemu-img'), 'create', '-f', 'qcow2', self.home / 'windows.qcow2', '100G'])
        public_key = (self.home / 'host-key.pub').read_text().split()
        (self.home / 'known_hosts').write_text(f'[127.0.0.1]:{ssh_port} {public_key[0]} {public_key[1]}\n')
        self.config = {'iso': str(iso), 'iso_sha256': expected_hash.lower(), 'firmware': str(firmware),
                       'cpus': 4, 'memory_mb': 8192, 'ssh_port': ssh_port, 'vnc_port': vnc_port,
                       'user': 'vmrunner', 'password': password}
        (self.home / 'config.json').write_text(json.dumps(self.config, indent=2))
        (self.home / 'config.json').chmod(0o600)
        print('Prepared Windows VM. Credentials remain in the private VM directory.')

    def qmp(self, command, arguments=None):
        with socket.socket(socket.AF_UNIX) as connection:
            connection.settimeout(10)
            connection.connect(str(self.home / 'qmp.sock'))
            stream = connection.makefile('rwb')
            greeting = stream.readline()
            if not greeting:
                raise ConnectionError('QEMU disconnected.')
            json.loads(greeting)
            for request in [{'execute': 'qmp_capabilities'}, {'execute': command, 'arguments': arguments or {}}]:
                stream.write(json.dumps(request).encode() + b'\n')
                stream.flush()
                while True:
                    line = stream.readline()
                    if not line:
                        raise ConnectionError('QEMU disconnected.')
                    response = json.loads(line)
                    if 'error' in response:
                        raise RuntimeError(response['error'])
                    if 'return' in response:
                        break
            return response['return']

    def start(self, install):
        if not self.config:
            raise RuntimeError('Prepare the VM first.')
        if install and self.config.get('provisioned'):
            raise RuntimeError('This VM is provisioned; start without --install to preserve its installation.')
        if (self.home / 'qmp.sock').exists():
            try:
                print(self.qmp('query-status'))
                return
            except (ConnectionError, FileNotFoundError):
                pass
        # No host bridge, system daemon, GPU passthrough, or LAN-facing listener.
        tpm = self.home / 'tpm'
        tpm.mkdir(exist_ok=True)
        self.execute([self.tool('swtpm'), 'socket', '--tpm2', '--tpmstate', f'dir={tpm}',
                      '--ctrl', f'type=unixio,path={self.home / "tpm.sock"},terminate',
                      '--pid', f'file={self.home / "tpm.pid"}', '--daemon'])
        c = self.config
        command = [self.tool('qemu-system-x86_64'), '-name', 'lucker-party-windows',
                   '-machine', 'q35,accel=kvm,smm=on', '-cpu', 'host', '-smp', str(c['cpus']), '-m', str(c['memory_mb']),
                   '-global', 'driver=cfi.pflash01,property=secure,value=on',
                   '-drive', f'if=pflash,format=raw,readonly=on,file={c["firmware"]}/OVMF_CODE.secboot.4m.fd',
                   '-drive', f'if=pflash,format=raw,file={self.home / "OVMF_VARS.fd"}',
                   '-drive', f'file={self.home / "windows.qcow2"},format=qcow2,if=ide',
                   '-chardev', f'socket,id=chrtpm,path={self.home / "tpm.sock"}',
                   '-tpmdev', 'emulator,id=tpm0,chardev=chrtpm', '-device', 'tpm-tis,tpmdev=tpm0',
                   '-netdev', f'user,id=net0,hostfwd=tcp:127.0.0.1:{c["ssh_port"]}-:22', '-device', 'e1000e,netdev=net0',
                   '-device', 'qemu-xhci', '-device', 'usb-tablet', '-vga', 'std', '-display', 'none',
                   '-vnc', f'127.0.0.1:{c["vnc_port"] - 5900}',
                   '-qmp', f'unix:{self.home / "qmp.sock"},server=on,wait=off',
                   '-pidfile', self.home / 'qemu.pid', '-D', self.home / 'qemu.log', '-daemonize',
                   '-L', self.home / 'host-tools/usr/share/qemu' if (self.home / 'host-tools').exists() else '/usr/share/qemu']
        if install:
            command += ['-drive', f'file={c["iso"]},media=cdrom,readonly=on',
                        '-drive', f'file={self.home / "seed.iso"},media=cdrom,readonly=on', '-boot', 'order=d,once=d']
        self.execute(command)
        if install:
            # Microsoft's DVD boot loader asks for a key before reading the answer file.
            for _ in range(20):
                time.sleep(.5)
                self.qmp('send-key', {'keys': [{'type': 'qcode', 'data': 'ret'}]})
        print(f'VM started; private console at 127.0.0.1:{c["vnc_port"]}, SSH at 127.0.0.1:{c["ssh_port"]}.')

    def ssh(self, command, capture=False):
        return run(['ssh', '-p', str(self.config['ssh_port']), '-i', self.home / 'client-key',
                    '-o', 'LogLevel=ERROR', '-o', 'IdentitiesOnly=yes', '-o', 'BatchMode=yes', '-o', 'ConnectTimeout=5',
                    '-o', 'StrictHostKeyChecking=yes', '-o', f'UserKnownHostsFile={self.home / "known_hosts"}',
                    'vmrunner@127.0.0.1', command], capture_output=capture, text=True)

    def scp(self, source, destination):
        run(['scp', '-P', str(self.config['ssh_port']), '-i', self.home / 'client-key', '-o', 'LogLevel=ERROR', '-o', 'IdentitiesOnly=yes',
             '-o', 'BatchMode=yes', '-o', 'StrictHostKeyChecking=yes',
             '-o', f'UserKnownHostsFile={self.home / "known_hosts"}', source, destination])

    def wait_ready(self, timeout=600):
        print('Waiting for Windows desktop provisioning and SSH...', flush=True)
        end = time.monotonic() + timeout
        while time.monotonic() < end:
            try:
                result = self.ssh('powershell.exe -NoProfile -Command "Get-Content C:\\LuckerParty\\provision-status.txt"', capture=True)
                if 'PROVISION_PASS' in result.stdout:
                    self.config['provisioned'] = True
                    (self.home / 'config.json').write_text(json.dumps(self.config, indent=2))
                    print('Windows provisioning and pinned SSH verified.', flush=True)
                    return
            except subprocess.CalledProcessError:
                pass
            time.sleep(5)
        raise RuntimeError('Windows is not ready; inspect the console and C:\\LuckerParty\\provision.log.')

    def assert_idle(self):
        self.ssh('powershell.exe -NoProfile -Command "if ((Get-ScheduledTask LuckerParty-Integration).State -eq \'Running\') { Write-Error \'Integration task is still running\'; exit 1 }"')

    def screenshot(self, output):
        output = output.resolve()
        output.parent.mkdir(parents=True, exist_ok=True)
        self.qmp('screendump', {'filename': str(output), 'format': 'png'})
        print(f'Windows VM desktop screenshot: {output}')

    def stop(self):
        with (self.home / 'run.lock').open('a') as lock:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            self.shutdown()

    def shutdown(self):
        if self.config.get('provisioned'):
            self.assert_idle()
        self.qmp('system_powerdown')
        print('Requested graceful Windows shutdown...', flush=True)
        end = time.monotonic() + 120
        while time.monotonic() < end:
            try:
                self.qmp('query-status')
            except (ConnectionError, FileNotFoundError):
                print('Windows and QEMU stopped; disk, firmware and TPM state preserved.')
                return
            time.sleep(1)
        raise RuntimeError('Shutdown is still pending; VM was left running.')

    def check(self, suite, timeout):
        # Serialize runs across every checkout that uses this VM.
        with (self.home / 'run.lock').open('w') as lock:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            self.wait_ready()
            self.assert_idle()
            # ISO copies retain ReadOnly on the first provisioned runner.
            self.ssh('powershell.exe -NoProfile -Command "(Get-Item C:\\LuckerParty\\Run-Integration.ps1).IsReadOnly = $false"')
            self.scp(TEMPLATES / 'Run-Integration.ps1', 'vmrunner@127.0.0.1:C:/LuckerParty/Run-Integration.ps1')
            identity = f'run-{time.time_ns()}'
            output = ROOT / 'artifacts/windows-vm' / identity
            output.mkdir(parents=True)
            source = output / 'source.zip'
            bundle = output / 'source.bundle'
            run(['git', 'bundle', 'create', bundle, 'HEAD'], cwd=ROOT)
            paths = run(['git', 'ls-files', '-z', '--cached', '--others', '--exclude-standard'], cwd=ROOT, capture_output=True).stdout.split(b'\0')
            with zipfile.ZipFile(source, 'w', zipfile.ZIP_DEFLATED) as archive:
                for name in sorted(set(os.fsdecode(p) for p in paths if p)):
                    if (ROOT / name).is_file():
                        archive.write(ROOT / name, name)
            request = {'id': identity, 'suite': suite, 'sha256': digest(source), 'bundleSha256': digest(bundle)}
            (output / 'request.json').write_text(json.dumps(request))
            self.scp(source, f'vmrunner@127.0.0.1:C:/LuckerParty/incoming/{identity}.zip')
            self.scp(bundle, f'vmrunner@127.0.0.1:C:/LuckerParty/incoming/{identity}.bundle')
            self.scp(output / 'request.json', 'vmrunner@127.0.0.1:C:/LuckerParty/request.json')
            self.ssh('schtasks.exe /Run /TN LuckerParty-Integration')
            print(f'Windows integration run {identity}; logs: {output}', flush=True)
            self.collect_result(identity, output, timeout)

    def collect(self, identity, timeout):
        if not re.fullmatch(r'run-[0-9]+', identity):
            raise RuntimeError('Invalid run ID.')
        output = ROOT / 'artifacts/windows-vm' / identity
        if not (output / 'request.json').is_file():
            raise RuntimeError('No matching run in this checkout.')
        with (self.home / 'run.lock').open('a') as lock:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            self.collect_result(identity, output, timeout)

    def collect_result(self, identity, output, timeout):
        end = time.monotonic() + timeout
        previous_phase = None
        # The writer replaces this snapshot atomically. Share deletion explicitly
        # so polling cannot block Windows from publishing the final result.
        status_command = (
            'powershell.exe -NoProfile -Command "'
            f"$path='C:\\LuckerParty\\runs\\{identity}\\status.json'; "
            'if (Test-Path -LiteralPath $path) { '
            '$stream=[System.IO.File]::Open($path,[System.IO.FileMode]::Open,'
            '[System.IO.FileAccess]::Read,([System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete)); '
            '$reader=[System.IO.StreamReader]::new($stream); '
            'try { $reader.ReadToEnd() } finally { $reader.Dispose() } }; exit 0"'
        )
        while time.monotonic() < end:
            result = self.ssh(status_command, capture=True)
            if result.stdout.strip():
                status = json.loads(result.stdout.lstrip('\ufeff'))
                if status.get('phase') != previous_phase:
                    previous_phase = status.get('phase')
                    print(f'Windows test phase: {previous_phase}', flush=True)
                if status['state'] in ('passed', 'failed'):
                    names = ['status.json', 'integration.log']
                    for phase in ('game', 'distribution'):
                        if f'{phase}ExitCode' in status:
                            names += [f'{phase}.stdout.log', f'{phase}.stderr.log']
                    for name in names:
                        self.scp(f'vmrunner@127.0.0.1:C:/LuckerParty/runs/{identity}/{name}', output / name)
                    print(json.dumps(status, indent=2))
                    print((output / 'integration.log').read_text(encoding='utf-8-sig', errors='replace'))
                    if status['state'] != 'passed':
                        raise RuntimeError(f'Windows integration failed; see {output}')
                    return
            time.sleep(5)
        raise RuntimeError(f'Timed out; the Windows task remains running. Collect {identity} before starting another run.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--vm-home', type=Path, default=DEFAULT_HOME)
    commands = parser.add_subparsers(dest='command', required=True)
    commands.add_parser('host-tools')
    prepare = commands.add_parser('prepare')
    prepare.add_argument('--iso', type=Path, required=True)
    prepare.add_argument('--sha256', required=True)
    prepare.add_argument('--assets', type=Path, required=True)
    prepare.add_argument('--ssh-port', type=int, default=2222)
    prepare.add_argument('--vnc-port', type=int, default=5907)
    start = commands.add_parser('start')
    start.add_argument('--install', action='store_true')
    commands.add_parser('status')
    commands.add_parser('ready')
    commands.add_parser('stop')
    screenshot = commands.add_parser('screenshot')
    screenshot.add_argument('--output', type=Path, default=ROOT / 'artifacts/windows-vm/desktop.png')
    check = commands.add_parser('check')
    check.add_argument('--suite', choices=['game', 'distribution', 'all'], default='all')
    check.add_argument('--timeout', type=int, default=2700)
    collect = commands.add_parser('collect')
    collect.add_argument('run_id')
    collect.add_argument('--timeout', type=int, default=2700)
    ssh = commands.add_parser('ssh')
    ssh.add_argument('remote_command')
    args = parser.parse_args()
    vm = WindowsVm(args.vm_home)
    if args.command not in ('host-tools', 'prepare') and not vm.config:
        raise RuntimeError('Prepare the VM first.')
    if args.command == 'host-tools':
        vm.host_tools()
    elif args.command == 'prepare':
        if not (1024 <= args.ssh_port <= 65535 and 5900 <= args.vnc_port <= 65535) or args.ssh_port == args.vnc_port:
            parser.error('Choose distinct SSH/VNC ports in their valid unprivileged ranges.')
        vm.prepare(args.iso, args.sha256, args.assets, args.ssh_port, args.vnc_port)
    elif args.command == 'start':
        vm.start(args.install)
    elif args.command == 'status':
        print(json.dumps(vm.qmp('query-status'), indent=2))
    elif args.command == 'ready':
        vm.wait_ready()
    elif args.command == 'stop':
        vm.stop()
    elif args.command == 'screenshot':
        vm.screenshot(args.output)
    elif args.command == 'ssh':
        vm.ssh(args.remote_command)
    elif args.command == 'check':
        vm.check(args.suite, args.timeout)
    elif args.command == 'collect':
        vm.collect(args.run_id, args.timeout)


if __name__ == '__main__':
    try:
        main()
    except BlockingIOError:
        print('Windows VM: another integration run or shutdown holds the VM lock.', file=sys.stderr)
        sys.exit(1)
    except (RuntimeError, OSError, subprocess.CalledProcessError) as error:
        print(f'Windows VM: {error}', file=sys.stderr)
        sys.exit(1)
