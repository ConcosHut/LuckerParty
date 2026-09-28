# Windows SSH and desktop launch setup prompt

Paste this after the Windows playtest handoff prompt, or use it alone in a chat
running locally on Windows. This supersedes the suggestion to build a custom
watch helper as the first development transport. Nothing here is installed yet.

---

Set up direct SSH access from my Linux development machine `box` to this Windows
PC, plus a scheduled task that launches Lucker Party in my logged-in desktop
session. Implement and verify the setup; do not stop at a plan. The Linux agent
should then be able to copy a build, run PowerShell commands, and trigger a
visible game launch without needing another chat for each playtest.

Context:

- Linux project: `/home/para/dev/lucker-party`, user `para`.
- This Windows app already connects to that machine as SSH host `box`. Check
  whether the Windows shell can use that alias before relying on it.
- Current Windows ZIP on Linux:
  `/home/para/dev/lucker-party/artifacts/LuckerParty-prototype-01.1-windows-x64.zip`.
- ZIP SHA-256:
  `0f8ad22a550ff3e8bc3de968b8786282e899a791c68c468a9983dd8611765a19`.
- Exported game is `LuckerParty.exe` with its PCK and bundled runtime/data files.
  It needs no Godot editor or .NET SDK on Windows.
- Prefer `F:\dev\lucker-party-playtest` for our dedicated working directory if
  available; otherwise choose a suitable user-owned directory. Preserve existing
  files and work exclusively on this setup.
- Velopack is planned for player distribution later; this task establishes the
  development transport and desktop launch, not a player updater.

Tasks:

1. Inspect native Windows version, current user identity, elevation, existing
   OpenSSH service/configuration, and the private network route to `box`. Identify
   the account and Windows address the Linux client should use. Use the logged-in
   desktop user for the game task; an elevated shell's identity may differ. Handle
   unsupported SSH account types explicitly rather than assuming any sign-in
   identity will work. Use the existing LAN/VPN connection; no router changes.

2. Install Windows OpenSSH Server if absent, start `sshd`, and configure automatic
   startup. Prefer invoking `powershell.exe -NoProfile -File ...` explicitly over
   changing the SSH server's default shell. Back up files before changing existing
   configuration. If elevation is unavailable, prepare a repeatable admin setup
   script and ask me to run that script or approve the Windows elevation prompt;
   continue the preparation that does not need elevation.

3. Configure public-key access from `box`. Through the existing authenticated
   Windows-to-box connection, create/reuse a dedicated Ed25519 client key on
   Linux at `/home/para/dev/lucker-party/.tools/ssh/windows-playtest_ed25519`.
   For unattended development use, create it without a passphrase, restrict its
   directory/key permissions, never overwrite an existing key, and keep the
   private key on Linux. Transfer only the `.pub` contents to Windows. `.tools/`
   is ignored by the project; never commit credentials. If access to `box` is
   unavailable, finish the Windows preparation and return the exact public-key
   and connection information needed from the Linux agent.

4. Authorize the public key in the location selected by the actual `sshd_config`.
   Handle Windows ACLs correctly, including the default administrator-account
   `C:\ProgramData\ssh\administrators_authorized_keys` behavior. Preserve existing
   keys. Restrict new firewall access to the Linux machine's actual private source
   address and appropriate network profile. Account for an auto-created broad
   allow rule on a new installation; do not leave it making the restriction
   ineffective. Preserve existing SSH users and firewall access on an existing
   installation; explain any conflict before a change that could lock them out.
   Validate configuration before restarting the service.

5. Verify the Windows SSH host-key fingerprint locally and against the key seen
   from `box` before recording it in a dedicated Linux known-hosts file under
   `.tools/ssh/`. Do not disable host-key checking. Test noninteractive key-based
   authentication from `box` with `BatchMode=yes` and a short connection timeout,
   then a harmless PowerShell command and file transfer. Record the exact working
   Linux command and identity/known-hosts paths without revealing private keys.

6. Create small repeatable Windows scripts in the dedicated playtest directory:
   one prepares an incoming build (verifies its supplied hash, extracts all files
   into a separate version directory, validates the executable/dependencies, then
   records which complete build should launch); another starts that build with
   the proper working directory and records the build identity, process ID, and
   errors. Never replace files used by a running game. If a playtest is running,
   defer the new launch and report its pending status instead of killing the game
   or starting another instance. Keep incoming-build paths within our directory.

7. Register an on-demand scheduled task named `LuckerParty-Playtest` with the
   logged-in desktop user's interactive logon token and limited privileges. It
   must run only when that user is logged in, require no stored account password,
   and have no automatic daily/logon schedule. The action runs the fixed launch
   script. Ensure task settings do not stop a game merely because a default
   execution-time limit expires. Respect an existing task of that name: inspect
   and preserve it before deciding whether it belongs to this setup.

8. Test the complete path from `box`: transfer the existing ZIP into the Windows
   incoming directory, verify/prepare it through PowerShell over SSH, and trigger
   `schtasks.exe /Run /TN LuckerParty-Playtest` over SSH. Confirm task/process/log
   results and report whether the game is visible on my desktop. A task-start
   response alone is not proof that the game launched. Leave the game running for
   me to playtest; if visibility cannot be verified with your available tools,
   explicitly separate the automated evidence from the human visibility check.

Deliver a short handoff I can paste back to the Linux agent: Windows host/IP,
SSH username/port, verified host-key fingerprint, Linux key/known-hosts paths,
Windows working directory and script paths, exact commands for transfer,
preparation, launch, and reading status/logs, what passed, and any remaining
blockers. Save setup/recovery instructions and describe how to remove only the
task, key entry, firewall rule, and service changes introduced by this setup.

Official references:
- https://learn.microsoft.com/en-us/windows-server/administration/openssh/openssh_install_firstuse
- https://learn.microsoft.com/en-us/windows-server/administration/openssh/openssh_keymanagement
- https://learn.microsoft.com/en-us/windows-server/administration/openssh/openssh-server-configuration
- https://learn.microsoft.com/en-us/powershell/module/scheduledtasks/new-scheduledtaskprincipal
- https://learn.microsoft.com/en-us/windows/win32/taskschd/schtasks
