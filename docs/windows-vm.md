# Windows integration VM on box

The dedicated Windows 11 Enterprise evaluation VM runs locally on `box` using
QEMU/KVM. It runs native Windows integration tests without involving the other
Windows PC. The existing [PC playtest connection](windows-playtesting.md) remains
available for real GPU, rendering, controls, and high-refresh testing.

## Normal test loop

From any checkout on box:

```text
python tools/windows_vm.py start
python tools/windows_vm.py check
python tools/windows_vm.py stop
```

`check` sends tracked and non-ignored untracked source, including uncommitted
edits, into a fresh guest directory, alongside a Git bundle of HEAD. It verifies
both hashes, imports HEAD and its index without checking out files, then overlays
the current tree. This preserves build commit/dirty metadata and deleted files.
It runs
`python tools/dev.py check` and
`python tools/check_distribution.py --target windows` through the on-demand
`LuckerParty-Integration` desktop task. The task uses an interactive logon token,
limited privileges, no stored task password, and no execution time limit. Update
replacement/restart therefore survives the SSH command ending. It uses Dev
package IDs, keeping test installs separate from player installations.

Use `check --suite party` for focused party rules/network/UI checks with one
rendered host and headless clients. Captures stay in the guest run's
`source/artifacts/party-checks/`; phase logs and status are collected locally.
Use `check --suite game` or `check --suite distribution` for the other focused runs.
The VM helper's `game` label runs the complete `dev.py check`, including launcher
checks; it is distinct from `dev.py check --suite game`. Routine launcher-only
edits normally use the local launcher suite and a targeted native desktop trial
when Windows-specific behavior changes. Do not repeat the VM's full suites just
to review layout changes; see [testing strategy](testing-strategy.md).
Runs are serialized across checkouts. A timeout leaves the guest task running;
inspect it before retrying. Status and the integration transcript are saved under
`artifacts/windows-vm/run-<id>/`. The guest retains build artifacts and additional
launcher/game logs for diagnosis.
Use `python tools/windows_vm.py collect run-<id>` to resume monitoring and
retrieve results after a host interruption or timeout, without restarting tests.

```text
python tools/windows_vm.py status
python tools/windows_vm.py ready
python tools/windows_vm.py ssh "powershell.exe -NoProfile -Command Get-Content C:\LuckerParty\provision.log"
python tools/windows_vm.py ssh "schtasks.exe /Query /TN LuckerParty-Integration /V /FO LIST"
```

`status` reports QEMU state. Inspect guest test results at
`C:\LuckerParty\runs\run-<id>\status.json` and `integration.log` over SSH.
Do not stop the VM during an active test. `stop` requests graceful shutdown;
wait for Windows and QEMU to exit before backing up or moving VM files.

## Desktop inspection and screenshots

```text
python tools/windows_vm.py screenshot
python tools/windows_vm.py screenshot --output artifacts/windows-vm/launcher.png
```

The helper captures the VM's current console through QEMU as a PNG, including
custom window chrome. It does not capture the physical PC. Open the saved image
with the agent's image viewer. For live interaction, connect a VNC viewer to
`127.0.0.1:5907` (`127.0.0.1:7` in viewers using display notation).

SSH processes run outside the visible desktop. Launching an application directly
from SSH is not evidence that its desktop window renders. Run GUI work through
an on-demand task with `vmrunner`, `Interactive`, and `Limited`, as the existing
integration task does. Do not reuse or replace `LuckerParty-Integration` for an
ad hoc GUI script. Wait for integration tests to finish, use a separate temporary
task, capture its window, close only processes started by that task, and unregister
the temporary task. Preserve existing applications and active test runs.

Guest source and packages live under `C:\LuckerParty\runs\run-<id>\source`.
Distribution checks deliberately install into
`artifacts\update-checks\<id>\installed with spaces\current`, **not** the usual
`%LOCALAPPDATA%\LuckerParty.Dev.Windows\current`. Locate the executable in the
specific completed run before preparing a desktop trial.

For complex PowerShell commands, prefer UTF-16LE `-EncodedCommand`; shell quotes,
pipes and `$` otherwise pass through both the Linux shell and Windows `cmd.exe`.
This pattern uses the helper's pinned SSH options without exposing credentials:

```python
import base64
from tools.windows_vm import DEFAULT_HOME, WindowsVm

vm = WindowsVm(DEFAULT_HOME)
code = r"Get-ScheduledTask LuckerParty-Integration | Select-Object State | ConvertTo-Json"
encoded = base64.b64encode(code.encode('utf-16le')).decode()
print(vm.ssh('powershell.exe -NoProfile -EncodedCommand ' + encoded, capture=True).stdout)
```

## Troubleshooting a stalled or failed run

Read the run's `status.json`, `game.stdout.log`, `game.stderr.log`, and equivalent
distribution files. A failed run can still leave useful evidence; `collect`
downloads it and returns a failure exit code. Do not treat a passed export as a
passed native suite. Do not start a second test to bypass the lock.

Windows `Start-Process -Wait` waits for descendants too. A persistent Roslyn
compiler server can therefore hold the task open after Python and Godot have
exited. The runner sets `UseSharedCompilation=false` and
`DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1` before either suite. For an older runner,
inspect its process tree and try `C:\Tools\dotnet\dotnet.exe build-server shutdown`.
If necessary, stop only a confirmed leftover compiler process after verifying
the actual suite and game processes have exited, then collect the original run.
Do not terminate a live suite or game merely because status is still running.

If the task is `Ready` but `status.json` still says `running`, inspect
`status.tmp` and the end of `integration.log`. A completed pending snapshot can
contain both exit codes and the final timestamp even when publication failed.
Recover that snapshot only after checking the task is idle and its results agree
with the transcript. Do not invent a passed result. The runner now publishes
through atomic `File.Replace`, retries transient sharing failures, and keeps the
transcript open through publication. Polling readers share deletion explicitly.
In PowerShell 5.1, use `[NullString]::Value` for the null backup-path argument to
`File.Replace`; `$null` is coerced to an empty string and raises a path error.

The multiplayer driver atomically replaces JSON control files. Windows readers
must allow `FileShare.Delete` while reading a snapshot; otherwise the driver's
`os.replace` can fail with `PermissionError` / WinError 5. `NetworkAutomation`
now shares deletion explicitly. Normal game launches do not read these files.

During the party UI iteration, a fresh editor import faulted with `0xc0000005`
while importing the first font. A clean baseline source imported normally.
Changing eager static Godot `Color` fields in the new UI helper to computed
properties made the modified source import and pass on the same VM. Avoid
constructing Godot values in static field initializers of new editor-scanned
scripts; verify a clean Windows import when adding such helpers.

## Configuration

- VM home: `~/.local/share/lucker-party/windows-vm`, shared by box's checkouts.
  Override with `--vm-home` before the subcommand for independent setup.
- 4 virtual CPUs, 8 GiB RAM, 100 GiB sparse QCOW2 disk.
- UEFI Secure Boot with Microsoft certificates and persistent software TPM 2.0.
- User-mode NAT; SSH forwarded only at `127.0.0.1:2222`.
- VNC console only at `127.0.0.1:5907` (display `:7` in a VNC viewer).
- No libvirt daemon, host bridge, LAN listener, or GPU passthrough.
- Dedicated `vmrunner` account with automatic desktop login for tests.
- Python 3.11.9, .NET SDK 8.0.425, Godot 4.5.2 .NET and matching templates,
  plus Git for Windows MinGit 2.56.0 for build metadata.
- Tools under `C:\Tools`; infrastructure and runs under `C:\LuckerParty`.

The account password, client key, injected SSH host key, unattended-install
media, disk, firmware variables, and TPM state stay in the private VM home.
Never commit, print, or upload that directory. Password SSH is disabled. The
host key is generated on box and injected before installation, so SSH can pin
it without trusting a network scan.

The host authentication agent was unavailable during setup. Signed Arch packages
were verified against the existing pacman keyring and extracted under
`host-tools/` in the VM home. `python tools/windows_vm.py host-tools` repeats
this without root or changes to the system package database. It requires Arch's
`pacman`, `pacman-key`, `curl`, and `bsdtar`; it is host infrastructure, not a game
build requirement. The package list includes `qemu-system-x86`, `qemu-img`,
`swtpm`, `cdrkit`, and `virt-firmware`.

## Preparation and recovery

Download Windows media from
[Microsoft's evaluation page](https://www.microsoft.com/en-us/evalcenter/download-windows-11-enterprise).
The initial VM uses the English US Windows 11 Enterprise **25H2** evaluation ISO,
build `26200.6584`, verified against Microsoft's published SHA-256:

```text
a61adeab895ef5a4db436e0a7011c92a2ff17bb0357f58b13bbc4062e535e7b9
```

This is a **90-day evaluation**, not a permanent Windows license. Microsoft
documents hourly shutdowns after expiration. Use appropriately licensed media
for a long-lived replacement; do not rely on snapshots to extend evaluation.

Place `python-3.11.9-amd64.exe`, `dotnet-sdk-8.0.425-win-x64.zip`,
`Godot_v4.5.2-stable_mono_win64.zip`, `templates.tpz`, and
`MinGit-2.56.0-64-bit.zip` in an assets directory.
Obtain them from the publishers and verify hashes/signatures before preparation.
The seed records asset hashes and checks them again inside Windows.

```text
python tools/windows_vm.py host-tools
python tools/windows_vm.py prepare --iso /absolute/path/windows.iso --sha256 <publisher-sha256> --assets /absolute/path/assets
python tools/windows_vm.py start --install
```

Preparation refuses to replace an existing configured VM or disk. The installer
wipes only the VM's dedicated virtual disk; do not attach host disks. Windows
installs unattended, logs in, installs the toolchain, creates the integration
task, and enables SSH. Watch `provision.log` and look for `PROVISION_PASS` in
`provision-status.txt`. Initial setup needs internet for the built-in OpenSSH
Server capability. After provisioning, shut down and restart without `--install`
to detach installation media.
If first-login setup reports `OOBEZDP`, its optional update step offers **Skip**;
use that option in the console, then inspect provisioning and network access.

Back up the **stopped** VM directory as a unit: disk, firmware variables, TPM
state, configuration and keys. Restore it as a unit too. Independent VMs need
different VM homes and unused ports (`prepare --ssh-port ... --vnc-port ...`).
Nothing autostarts at host boot.
Retiring this VM requires no changes on the other PC or system service removal:
shut down and archive its dedicated directory when no longer needed.

## Verification scope

Native headless scenarios validate movement, camera math, launcher guards and
control, and real ENet multiplayer behavior. Installed tests validate actual
Windows installation and updates with Dev IDs. They do not establish GPU
compatibility, rendering quality, input feel, or high-refresh performance. The
basic virtual display keeps automated checks independent of box's GPU.

## Verified September 28, 2026

The completed native run `run-1790642975300105006` passed both suites in desktop
session 3, with exit code 0 for each. It took approximately 5 minutes 32 seconds
(00:49:37–00:55:09 UTC on September 29). Evidence is saved in the originating
checkout under `artifacts/windows-vm/run-1790642975300105006/`: `status.json`,
`integration.log`, and separate stdout/stderr files for each suite.

- Game checks: build/core boundary, movement and 240 FPS camera scenarios,
  launcher control/guards, core input/name checks, and all 45 real ENet scenarios.
- Installed checks: native Windows Dev packaging and installation in a path with
  spaces, exported GUI executable's redirected control pipe, offline and bad
  downloads, successful HTTP update and launcher restart, Stable/Beta/older Stable
  transitions, exactly one game launch, and retained preferences/saved data.
- Build metadata: original commit `8b726f0e0e58d64a4753dfd5b138d32629fbbc88`
  and `dirty: true`, reflecting the checkout's uncommitted setup changes.
- Infrastructure: strict pinned SSH, Secure Boot enabled, TPM present/ready,
  interactive limited task with no timeout, and refusal to shut down active tests.
  Graceful shutdown and restart without either installation ISO passed.

Initial setup exposed the ISO's inherited read-only runner attribute, a missing
Git prerequisite, and native output omitted by PowerShell transcription. The
provisioner and runner now account for each. No changes were made on the other PC.
Rendering, controls, and high-refresh performance remain human hardware checks.

## Follow-up verification from the main checkout

Run `run-1790647171379573841` completed both native suites in session 4 with
exit code 0 (01:59:33–02:05:03 UTC September 29; approximately 5 minutes 30
seconds). It used commit `a87caed` plus the uncommitted VM integration and
Windows control-reader fix. The full Linux game/launcher suite also passed,
including all 45 real multiplayer scenarios. Evidence is in this checkout's
ignored `artifacts/windows-vm/run-1790647171379573841/`.

The completed result initially remained in `status.tmp`; its idle task,
transcript and both exit codes were checked before recovering the snapshot.
The writer/poller publication fixes described above address this failure.
The focused follow-up `run-1790647857868436708` passed installed-update checks
with exit code 0 and published/collected its terminal result normally, without
manual recovery (02:10:59–02:14:54 UTC; approximately 3 minutes 55 seconds).
Its evidence is under `artifacts/windows-vm/run-1790647857868436708/`.

A separate limited interactive task opened that run's installed Dev launcher
with `--no-update`, verified an enabled Play control through UI Automation, and
captured the actual 1240×720 window. It then closed only its own launcher and
removed its temporary task. `launcher.png`, `console.png` and `status.json` are
under ignored `artifacts/windows-vm/launcher-desktop-trial/`. QEMU PNG console
capture works without a separate image-conversion dependency.

The VM's 1280×800 desktop initially exposed a launcher positioning issue:
startup reduced window height without recentering the original position, leaving
the caption above the screen. Commit `ce3a54e` fixes this using the display's
scaling and working area. A later native trial verified the 1240×720 client at
(20,16), entirely within the 1280×752 working area. Final composition, both
sidebar modes and Settings selection captures are under ignored
`artifacts/windows-vm/launcher-final-desktop/`. See the
[launcher checklist](launcher-polish-checklist.md) for the full verification.
This does not establish physical GPU or input quality.
After collecting the follow-up run, graceful shutdown completed normally. The
VM is stopped and can be reused with the normal `start` command.

## Player launcher trial

Beta 26 was downloaded from the published release, verified against its
`SHA256SUMS.txt` on box and inside the guest, and installed separately at
`C:\LuckerParty\beta26-player\current\LuckerParty.Launcher.exe`. This uses
the production Windows package ID; integration suites still use Dev IDs.
Native Settings toggle/Back, both sidebar modes, Play readiness and window
placement passed. Evidence is under ignored
`artifacts/windows-vm/launcher-beta26-desktop/`. The temporary desktop task
closed its own launcher and was removed; the installation remains available
for future player-update tests. Graceful VM shutdown followed the trial.
