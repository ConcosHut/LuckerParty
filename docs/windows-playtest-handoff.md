# Windows playtest chat handoff

Paste the text below into a new Codex chat running locally on Windows. This is
prepared context, not an already-created or connected Windows chat.

For direct Linux-to-Windows control, also use [the SSH setup add-on](windows-ssh-handoff.md).
SSH plus an interactive scheduled task is now the preferred first development
transport; a custom watch helper is a fallback.

---

Help establish the Windows side of our Lucker Party development/playtest loop.
The user wants development on their Linux machine (`box`) and frequent Windows
playtests, with eventual automatic download and visible game launch after a
requested build is ready. Start by retrieving and launching the existing build.

## Project context

- Linux source workspace: `/home/para/dev/lucker-party`.
- The Windows app already has an SSH connection named `box` for that workspace.
  Check whether the Windows shell can use that SSH alias; do not assume it can.
- Source is in local Git on Linux, branch `main`, with no Git remote configured.
- Game: Godot 4.5.2 .NET, C#, .NET 8, first-person single-player test arena.
  Movement, jumping, colored boxes, menu, and camera interpolation work. The
  user already tested the latest movement fix successfully on Windows.
- Keep game rules independent of Godot. The contemplated distribution launcher
  will also be independent of Godot, using C# and Velopack.
- Read `AGENTS.md`, `README.md`, and `docs/distribution-plan.md` on `box` for the
  development instructions and distribution proposal. No updater/helper is
  implemented yet. Stable/beta releases are planned, with a private development
  feed for quick iteration that does not require a Git push or CI run.

## First concrete task

1. Confirm this chat runs locally on Windows and can launch a visible application
   in the logged-in desktop session. If commands run in WSL or on `box`, explain
   that limitation and identify the native Windows execution path before launch.
2. Work in a new dedicated directory, preferably
   `F:\dev\lucker-party-playtest` if the drive is available; otherwise use a
   suitable user-owned directory. Preserve any existing files. Do not modify
   unrelated projects such as combat-arms or install a development toolchain.
3. Check access to `box` using the existing authenticated SSH connection.
   If usable, copy this complete ZIP from Linux with SCP:
   `/home/para/dev/lucker-party/artifacts/LuckerParty-prototype-01.1-windows-x64.zip`.
   Do not expose credentials or invent new connection credentials. If access is
   unavailable, report the exact missing connection information.
4. Verify the downloaded ZIP's SHA-256:
   `0f8ad22a550ff3e8bc3de968b8786282e899a791c68c468a9983dd8611765a19`.
5. Extract all files into a separate directory for this build. Run
   `LuckerParty.exe` with its build directory as the working directory. Preserve
   the PCK and bundled runtime/data files beside it; no Godot/.NET SDK is needed.
   Check for an already-running instance before starting another one.
6. Report the installed path, artifact identity, process/launch result, and any
   useful startup logs. Distinguish successful process launch from a human
   check of rendering and controls; leave the game running for the user.

Controls: WASD/arrows, mouse look, Shift sprint, Space jump, R reset, Escape menu.
F3 shows diagnostics, F4 toggles interpolation, F5 toggles diagnostic physics
rate. Leave interpolation enabled and physics at 60 Hz for normal play.

After that baseline, investigate the smallest repeatable download-and-launch
command and whether this app exposes chat coordination tools. The Linux chat
currently has read/list tools but no chat creation or message-sending tool.
Do not assume pairing a device gives an existing Linux chat a Windows shell.
Use `docs/windows-ssh-handoff.md` to establish direct SSH access and an interactive
desktop launch task. If that cannot meet the workflow, consider the small opt-in
desktop helper described in the distribution plan. Avoid interrupting the user's
current game.

The immediate outcome is a verified Windows retrieval/launch baseline and a
concrete next step for automating it. Player distribution still needs a
Velopack launcher and stable/beta hosting later.
