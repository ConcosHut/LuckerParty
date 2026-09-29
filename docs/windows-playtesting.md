# Windows playtesting from box

Use the [dedicated Windows VM](windows-vm.md) for routine native integration,
installer/update and launcher desktop tests. This physical-PC connection is for
requested hardware rendering, controls and high-refresh playtests.

The development connection is configured. Use SSH/SCP from the Linux workspace
to prepare a Windows build, then trigger the on-demand interactive desktop task.
A custom watch helper and additional chats are not required for this loop.
This is development infrastructure; the player-facing Velopack launcher and
stable/beta distribution are available; see [releases](releases.md).

## Configured connection

- Windows SSH destination: `Para@192.168.0.244`, TCP port 22.
- Allowed Linux source address: `192.168.0.215`, Windows Private firewall profile.
- Verified Windows ED25519 host fingerprint:
  `SHA256:JXHGOJAp2hB8qgKEOi62hAp8tTQbFij0WylOPJY7Xpw`.
- Linux client key: `.tools/ssh/windows-playtest_ed25519`.
- Linux known-hosts file: `.tools/ssh/windows_playtest_known_hosts`.
- Linux transfer/launch wrapper: `.tools/ssh/playtest-windows.sh`.
- Windows working directory: `F:\dev\lucker-party-playtest`.
- Windows scripts: `Prepare-PlaytestBuild.ps1`, `Launch-Playtest.ps1`,
  `Prepare-And-Launch.ps1`, and `Get-PlaytestStatus.ps1` in that directory.
- Windows setup/removal instructions: `F:\dev\lucker-party-playtest\SETUP.md`.

The key and wrapper are machine-local files ignored by Git. Do not print or
commit private keys. Another checkout does not inherit this connection merely
by reading these instructions. Address or network-profile changes may require
updating the setup; preserve host-key verification when doing so.

## Build and send a requested playtest

Run the appropriate checks and export from the repository root on `box`:

```bash
python tools/dev.py check
python tools/dev.py export --target windows
```

Then use the wrapper with the archive, its SHA-256, and a unique build ID:

```bash
playtest_archive="/home/para/dev/lucker-party/artifacts/LuckerParty-prototype-01.1-windows-x64.zip"
playtest_sha256=$(sha256sum -- "$playtest_archive" | cut -d ' ' -f 1)
playtest_build_id="playtest-$(date -u +%Y%m%d-%H%M%S)"
/home/para/dev/lucker-party/.tools/ssh/playtest-windows.sh \
  "$playtest_archive" "$playtest_sha256" "$playtest_build_id"
```

Use the actual export's archive name if the prototype version changes. Do not
reuse an older archive after a failed export. A new build ID identifies a
complete extraction directory; the SHA-256 identifies the exact archive.

The wrapper checks the local archive hash, copies the ZIP, asks Windows to
verify/extract it, invokes `LuckerParty-Playtest`, and returns status/log output.
The game uses its build directory as its working directory. All exported files,
including the PCK and runtime/data files, must remain together.

The task runs as desktop user Para with interactive logon, limited privileges,
no stored password, no triggers, and no execution time limit. Windows must be
awake, reachable, and the user logged in; playing requires the desktop available.
If a game is already running, the launch script reports `PENDING` instead of
starting another instance. Close the game and explicitly trigger the task for
the pending build; do not assume it automatically launches after the old game
exits. Do not kill a user's active playtest or replace its application files.

## Read status or trigger launch

Read status without changing the running game:

```bash
ssh -i .tools/ssh/windows-playtest_ed25519 \
  -o IdentitiesOnly=yes -o BatchMode=yes -o ConnectTimeout=5 \
  -o StrictHostKeyChecking=yes \
  -o UserKnownHostsFile=.tools/ssh/windows_playtest_known_hosts \
  Para@192.168.0.244 \
  'powershell.exe -NoProfile -ExecutionPolicy Bypass -File F:\dev\lucker-party-playtest\Get-PlaytestStatus.ps1'
```

Use the same SSH options with remote command
`schtasks.exe /Run /TN LuckerParty-Playtest` to request launch of the prepared
build. A successful task-start response is not sufficient: inspect launch
status, process build path, session ID, and logs. Record rendering/controls
observations separately from automated process checks.

For player-launcher update checks, run the updater through an interactive
scheduled task too. Starting it directly inside an SSH command can let SSH
session cleanup terminate the updater before replacement/restart completes.
Inspect the replacement process path/version and pending-intent state after the
task; an updater start message alone is not evidence of a completed update.

## Verification recorded September 28, 2026

The Windows setup agent reported end-to-end transfer, hash verification,
preparation, task launch, and rendering for `prototype-01.1-ssh-verified`.
Archive hash:
`0f8ad22a550ff3e8bc3de968b8786282e899a791c68c468a9983dd8611765a19`.

The Linux development chat independently verified the saved host fingerprint
and connected with strict host-key checking and noninteractive key authentication.
`Get-PlaytestStatus.ps1` returned success and reported that build running as
PID 16252 in desktop session 1, responding, from its versioned build directory.
It confirmed the task configuration above; historical logs included a `PENDING`
result while a previous game was running. This verification did not re-launch
or interrupt the current game. Controls for this launch still need human
playtesting; the user previously confirmed the movement interpolation fix.
