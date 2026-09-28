# Lucker Party — prototype 01.1

A small first-person test bed: a gray grid floor, four colored solid boxes,
walking, sprinting, jumping, mouse look, reset, and an Escape menu. This iteration
is local single-player; no networking or minigames are implemented.

## Play the Windows build

Extract **all** files from `artifacts/LuckerParty-prototype-01.1-windows-x64.zip`,
then double-click `LuckerParty.exe`. Keep its PCK and data directory next to it.
The packaged game includes its runtime; you do not need Godot or a .NET SDK to
play. This build targets normal Intel/AMD 64-bit Windows PCs.

| Control | Action |
| --- | --- |
| WASD / arrow keys | Move |
| Mouse | Look |
| Shift | Sprint |
| Space | Jump |
| R | Reset position and view |
| Escape | Open/close menu and release/capture mouse |
| F3 | Show frame timing and movement diagnostics |
| F4 | Compare smooth camera translation with the original stepping |
| F5 | Switch physics between 60 Hz and a diagnostic 10 Hz |

Switching away from the game opens the menu. Use Resume when you return.
Walk into boxes to check collision, and jump onto the low green step.

The camera now interpolates player position between physics ticks while mouse
look remains immediate. Physics runs at 60 Hz by default. For a high-refresh
monitor, hold A/D with the mouse still and press F4 to compare interpolation on
and off. F5 exaggerates stepping for debugging; return to 60 Hz for normal play.
See [the jitter investigation](docs/movement-jitter.md) for measured results.

## Develop on Windows or Linux

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
python tools/dev.py export --target windows
python tools/dev.py export --target linux
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

- `src/LuckerParty.Core`: plain C# movement settings, independent of Godot.
- `src/LuckerParty.Godot`: scenes, input, character physics, rendering, and UI.
- `tools/dev.py`: shared Windows/Linux build, run, check, and export commands.
- `docs`: design brief, development harness plan, and playtest instructions.

`check` builds the project, checks the core dependency boundary, imports the
Godot project, and runs a bounded headless scenario against real scene physics.
It runs 15 movement/camera checks covering floor contact, speed, diagonal
normalization, sprint, box collision, jump/landing, disabled controls, immediate
mouse look, look limits, reset, interpolation reset, and the R key binding.
It also compares camera motion with and without interpolation at 240 render
frames per second against 60 physics ticks per second.
It exits nonzero on a failed assertion.

For graphical evidence, the game accepts `-- --capture /absolute/path/image.png`
and saves a viewport screenshot, then exits. This requires a graphical display.

The first iteration's verification results and remaining Windows playtesting
are recorded in [docs/prototype-01.md](docs/prototype-01.md).
