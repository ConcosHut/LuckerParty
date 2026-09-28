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

The standalone C# Avalonia launcher packages the full game with Velopack 1.2.158.
Follow [docs/releases.md](docs/releases.md) for channels, publishing, and recovery.
`master` publishes Beta, `release` publishes Stable; `legacy-2` preserves s&box.
Use `python tools/dev.py pack --target windows` for isolated Dev packages and
`--profile player` for production IDs. Real installed checks run with
`python tools/check_distribution.py --target linux` or `--target windows` on the
matching OS. Build commands own a workspace lock; export platforms sequentially.
Keep update SDK startup auto-apply disabled, hold the launcher session guard until
the game exits, and keep preferences/logs outside replaced install files.
GitHub Actions verifies both platforms and publishes them together in a draft
before exposing it. Never overwrite a published version; advance version.json.
