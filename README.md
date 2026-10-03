# Lucker Party

A multiplayer party prototype with direct-IP lobbies, readiness, player colors
and one complete rock-paper-scissors minigame: hidden choices, points, champions
and replay. The first-person sandbox remains available for practice and debugging.

## Install and play

Download the Windows Stable installer or Linux Stable AppImage from
[GitHub Releases](https://github.com/ConcosHut/LuckerParty/releases/latest).
The current Beta launcher checks for updates on opening. If the selected channel has a different
version, click Update to install it, then Play. Changing Stable/Beta only checks
that channel; it does not install or start a game. Choose Beta for
development updates; return to Stable at any time. Offline play, installation,
release promotion, and recovery are covered in [the release runbook](docs/releases.md).
Older Stable packages still include the legacy launcher; choose Beta for the
current Party Room interface and multiplayer party prototype.

For multiplayer play, choose Beta in the launcher, set your display name on the
game's Home screen, then choose Host Party or Join Party. Join accepts a host
IP/hostname or a copied `host:port` invite; UDP 27015 is the default. Every active player readies up, then
the host starts the party. See [the party guide](docs/party-prototype.md) for rules
and scoring.
See [the multiplayer runbook](docs/multiplayer.md) for LAN/internet hosting,
server commands, architecture, and verification.

For the original ZIP playtest build, extract **all** files from `artifacts/LuckerParty-prototype-01.1-windows-x64.zip`,
then double-click `LuckerParty.exe`. Keep its PCK and data directory next to it.
The packaged game includes its runtime; you do not need Godot or a .NET SDK to
play. This build targets normal Intel/AMD 64-bit Windows PCs.

| Control | Action |
| --- | --- |
| WASD / arrow keys | Move in Sandbox/Practice |
| Mouse | Look |
| Shift | Sprint |
| Space | Jump |
| R | Reset position and view |
| Escape | Open/close menu; shared multiplayer time keeps running |
| Tab | Show party standings while held |
| Alt-Tab | Switch windows without opening a game menu |
| F3 | Show frame timing and movement diagnostics |
| F4 | Compare smooth camera translation with the original stepping |
| F5 | Practice only: switch physics between 60 Hz and a diagnostic 10 Hz |

Alt-Tab leaves the current screen visible and the party running. In the 3D
sandbox it releases local controls and the mouse; switching back restores them
unless you explicitly opened the menu with Escape. The movement controls below
apply to Sandbox/Practice; the Party uses on-screen choices and server-owned timers.
Walk into boxes to check collision, and jump onto the low green step.

The camera now interpolates player position between physics ticks while mouse
look remains immediate. Physics runs at 60 Hz by default. For a high-refresh
monitor, hold A/D with the mouse still and press F4 to compare interpolation on
and off. F5 exaggerates stepping for debugging; return to 60 Hz for normal play.
See [the jitter investigation](docs/movement-jitter.md) for measured results.

## Develop on Windows or Linux

For routine Windows integration checks on `box`, use the dedicated
[Windows VM](docs/windows-vm.md): `python tools/windows_vm.py start`, then
`python tools/windows_vm.py check`, then `python tools/windows_vm.py stop`.
For requested physical-PC playtests, the SSH transfer and desktop launch
commands are in [docs/windows-playtesting.md](docs/windows-playtesting.md).

Pinned toolchain:

- [Godot **4.5.2 .NET**](https://github.com/godotengine/godot/releases/tag/4.5.2-stable),
  with matching **Mono/.NET export templates** when exporting.
- [.NET SDK **8.0.425**](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
  `global.json` permits newer patches in the same feature band.
- Python 3.11+ for the optional command wrapper below.

Install the SDK and unzip the Godot editor for your operating system. Import
`src/LuckerParty.Godot/project.godot` into Godot, then press **F5** to build and
run. Python is not needed for that editor workflow. A runtime-only .NET
installation cannot compile the project.

Install export templates using **Editor → Manage Export Templates → Install
from File**, selecting the release's `mono_export_templates.tpz` file.

Run these commands from the repository root, using `python` or `python3` as
appropriate. Set `GODOT_BIN` to the full path of the Godot .NET executable or
pass `--godot "full/path/to/godot"`. The wrapper also discovers this workspace's
local tools under `.tools/`. `--dotnet` / `DOTNET_BIN` can override the SDK host.

```text
python tools/dev.py build
python tools/dev.py run
python tools/dev.py check
python tools/dev.py check --suite launcher
python tools/dev.py check --suite core
python tools/dev.py check --suite game
python tools/dev.py check --suite party
python tools/windows_vm.py check --suite party
python tools/dev.py export --target windows
python tools/dev.py export --target linux
python tools/dev.py pack --target windows --profile player
python tools/check_distribution.py --target linux
```

On Windows, for example:

```powershell
$env:GODOT_BIN = "C:\Tools\Godot\Godot_v4.5.2-stable_mono_win64_console.exe"
python tools/dev.py check
python tools/dev.py export --target windows
```

Adjust that example to the actual location of the unzipped editor executable.
The exports package every output file into a ZIP under `artifacts/`.
Generated tools, build outputs, and artifacts are excluded from Git.

## Structure and verification

- `src/LuckerParty.Core`: plain C# party/RPS/scoring rules, movement settings, input codec and names.
- `src/LuckerParty.Godot`: scenes, input, character physics, rendering, and UI.
- `src/LuckerParty.Launcher`: standalone updater UI and guarded game process.
- `tools/dev.py`: shared Windows/Linux build, run, check, export, and pack commands.
- `tools/check_distribution.py`: real installed updater and failure checks.
- `tools/test_multiplayer.py`: real multi-process sandbox ENet and latency/loss checks.
- `tools/test_party.py`: real lobby/RPS/late-join/disconnect/replay checks and UI captures.
- `docs`: design brief, development harness plan, and playtest instructions.

`check` builds the project, checks the core dependency boundary, imports the
Godot project, and runs a bounded headless scenario against real scene physics.
It runs 15 movement/camera checks covering floor contact, speed, diagonal
normalization, sprint, box collision, jump/landing, disabled controls, immediate
mouse look, look limits, reset, interpolation reset, and the R key binding.
It also compares camera motion with and without interpolation at 240 render
frames per second against 60 physics ticks per second.
It also launches isolated host/client processes to check multiplayer ownership,
joining, movement, loss, capacity, disconnects, and recovery.
It exits nonzero on a failed assertion.

Use `check --suite launcher` for launcher UI/process changes (it also renders
screenshots under `artifacts/launcher-ui-checks/`), `--suite core` for portable
rules/input/name changes, or `--suite game` for game/core/network checks without
the launcher UI suite. Launcher/core checks do not require Godot. Plain `check`
retains the complete suite used by CI. See [the testing strategy](docs/testing-strategy.md)
for when native Windows or actual installer testing adds useful evidence.

For graphical evidence, the game accepts `-- --capture /absolute/path/image.png`
and saves a viewport screenshot, then exits. This requires a graphical display.

The first iteration's verification results and remaining Windows playtesting
are recorded in [docs/prototype-01.md](docs/prototype-01.md).

The standalone Avalonia/C# launcher uses Velopack for Windows installers and
Windows/Linux updates. `master` publishes Beta; `release` publishes Stable.
The old s&box master is preserved as `legacy-2`. See
[the release runbook](docs/releases.md) for packaging commands, installed update
checks, channel behavior, and recovery. [The implementation plan](docs/launcher-implementation-plan.md)
records the design and acceptance criteria.
