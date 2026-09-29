# Lucker Party

Start with [the project brief](docs/project-brief.md) and
[the development harness plan](docs/development-harness.md). Build and play
instructions are in [README.md](README.md).

## Current state

The first multiplayer sandbox uses Godot 4.5.2 .NET and C# on .NET 8 (SDK 8.0.425). It implements
a first-person sandbox with a main menu, eight-player direct-IP ENet lobbies,
server-owned movement, prediction and names. Practice retains F3/F4/F5 diagnostics. The original s&box project is
https://github.com/ConcosHut/LuckerParty. Minigames come later.

## Working principles

- Keep code readable by the human team. Use clear names and small modules.
- Support development and builds on Linux and Windows, including quick Windows
  playtesting. Avoid making Bash or Linux-specific paths a build requirement.
- Keep game rules separate from engine objects, rendering, physics, and transport.
- Build abstractions around demonstrated needs; avoid a universal engine wrapper.
- Multiplayer is a core principle: new gameplay must define server authority,
  late-join state, and disconnect behavior. Clients submit intent, not scores
  or random outcomes. Do not pause shared physics when a player opens a menu.
- Add runnable verification alongside substantive gameplay features.
- Report what was verified and which behavior still needs human playtesting.
- Preserve meaningful design decisions in repository documents.
- Update these instructions and linked documents when the actual setup changes.

## Commands

From the repository root, with the pinned SDK and Godot .NET editor available:

- `python tools/dev.py build`: compile and verify the core dependency boundary.
- `python tools/dev.py check`: build, import, and run movement, 240 FPS camera,
  core input/name, launcher guard, and multi-process networking scenarios.
- `python tools/dev.py check --suite launcher`: focused launcher control/UI checks,
  without a game build or Godot import; use for launcher-only iteration.
- `python tools/dev.py check --suite core`: portable input/name/rule checks only.
- `python tools/dev.py check --suite game`: game/core/network checks without the launcher UI suite.
- `python tools/dev.py run`: launch the graphical test bed.
- `python tools/dev.py export --target windows`: package the Windows x64 build.
- `python tools/dev.py export --target linux`: package the Linux x64 build.

Use `--godot` or `GODOT_BIN` for a non-PATH editor. Local tools under `.tools/`
are discovered automatically. Keep `.tools/`, `.godot/`, and `artifacts/` out
of Git. Successful export is not evidence of Windows runtime playtesting.

Choose verification by changed behavior; follow [docs/testing-strategy.md](docs/testing-strategy.md).
For UI-only edits, run the launcher suite and inspect final renders. Use a native
VM desktop check when chrome, DPI, fonts or Windows integration changes. Do not
repeat full multiplayer or installed-update suites locally for unchanged game
or updater code; CI already runs both before publication. Repeat passed checks
only after relevant edits, failures or new uncertainty. Batch visual adjustments
before final review; a tool/docs-only change does not need a player release.

Use the dedicated Windows VM on `box` for routine native Windows game,
launcher, and installed-update checks; follow [docs/windows-vm.md](docs/windows-vm.md).
Run `python tools/windows_vm.py start`, then `check`, then `stop` when idle.
It shares a private VM home across checkouts and serializes runs. Keep its
credentials, disk, firmware and TPM state outside Git; never reinstall it for a
normal test. Desktop work must run through an interactive task, not the SSH session.
Use `screenshot` for console captures. Hardware rendering/input and high-refresh
playtesting still use the physical PC when requested.

Windows SSH playtesting is configured from this `box` workspace. Follow
[docs/windows-playtesting.md](docs/windows-playtesting.md) to send a requested
build, launch it through the interactive Windows task, and read status/logs.
Keep machine-local keys ignored and preserve active playtests; a `PENDING`
result requires a later task trigger after the game closes.

The standalone C# Avalonia launcher packages the full game with Velopack 1.2.158.
Follow [docs/releases.md](docs/releases.md) for channels, publishing, and recovery.
`master` publishes Beta, `release` publishes Stable; `legacy-2` preserves s&box.
Use `python tools/dev.py pack --target windows` for isolated Dev packages and
`--profile player` for production IDs. Real installed checks run with
`python tools/check_distribution.py --target linux` or `--target windows` on the
matching OS. Build commands own a workspace lock; export platforms sequentially.
Keep update SDK startup auto-apply disabled, hold the launcher session guard until
all game instances exit, and keep preferences/logs outside replaced install files.
The Party Room launcher prepares updates on startup without launching a game.
Settings is toggleable navigation with a persistent selected state; Back and
Escape return to play and clear selection. Startup windows fit and recenter in
the display's working area using logical dimensions and pixel positions.
Its settings opt-in reveals a sidebar with targeted Show/Close and permits additional Play launches of the installed
build while games run; updates and channel changes still require every child to exit.
GitHub Actions verifies both platforms and publishes them together in a draft
before exposing it. Never overwrite a published version; advance version.json.
Launcher window state is coordinated in LauncherApp.cs; layout/chrome/sidebar
live in separate partial-class files and control themes in Assets/PartyRoomControls.axaml.
The finalized Clover Peak/Leafcut SVG sources live in assets/brand/ and render
natively through BrandLogo.cs. Preserve their geometry and colors. The approved
four-world hero is Assets/party-room-arena.png; keep regional art fixes intact.
The home screen uses a sand action strip with a large bottom-right Play button
and a restrained coral shadow; narrow layouts reflow the actions.
Parallel edits may share a checkout when file ownership is disjoint; run builds
only after integration. See docs/launcher-polish-checklist.md for the UI pass.

Multiplayer architecture, command-line servers, connection requirements, and
verification are in [docs/multiplayer.md](docs/multiplayer.md). Network physics
stays at 60 Hz. Preserve the independent camera presentation history during
reconciliation; resetting it on every snapshot causes high-refresh stepping.
