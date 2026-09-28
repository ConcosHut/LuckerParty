# Lucker Party

Start with [the project brief](docs/project-brief.md) and
[the development harness plan](docs/development-harness.md). Build and play
instructions are in [README.md](README.md).

## Current state

Prototype 01.1 uses Godot 4.5.2 .NET and C# on .NET 8 (SDK 8.0.425). It implements
a single-player first-person test bed with interpolated camera translation and
F3/F4/F5 timing diagnostics. The original s&box project is
https://github.com/ConcosHut/LuckerParty. Networking and minigames come later.

## Working principles

- Keep code readable by the human team. Use clear names and small modules.
- Support development and builds on Linux and Windows, including quick Windows
  playtesting. Avoid making Bash or Linux-specific paths a build requirement.
- Keep game rules separate from engine objects, rendering, physics, and transport.
- Build abstractions around demonstrated needs; avoid a universal engine wrapper.
- Add runnable verification alongside substantive gameplay features.
- Report what was verified and which behavior still needs human playtesting.
- Preserve meaningful design decisions in repository documents.
- Update these instructions and linked documents when the actual setup changes.

## Commands

From the repository root, with the pinned SDK and Godot .NET editor available:

- `python tools/dev.py build`: compile and verify the core dependency boundary.
- `python tools/dev.py check`: build, import, and run the movement and 240 FPS
  camera interpolation scenarios.
- `python tools/dev.py run`: launch the graphical test bed.
- `python tools/dev.py export --target windows`: package the Windows x64 build.
- `python tools/dev.py export --target linux`: package the Linux x64 build.

Use `--godot` or `GODOT_BIN` for a non-PATH editor. Local tools under `.tools/`
are discovered automatically. Keep `.tools/`, `.godot/`, and `artifacts/` out
of Git. Successful export is not evidence of Windows runtime playtesting.

Windows SSH playtesting is configured from this `box` workspace. Follow
[docs/windows-playtesting.md](docs/windows-playtesting.md) to send a requested
build, launch it through the interactive Windows task, and read status/logs.
Keep machine-local keys ignored and preserve active playtests; a `PENDING`
result requires a later task trigger after the game closes.

The next planned feature is the standalone Velopack launcher. Follow
[docs/launcher-implementation-plan.md](docs/launcher-implementation-plan.md).
The user chose public `ConcosHut/LuckerParty`, preserving s&box on `legacy-2`,
with future Godot `master`/Beta and `release`/Stable branches. The remote migration
has not happened; this local checkout is still on `main` without a Git remote.
