# Lucker Party

Start with [the project brief](docs/project-brief.md) and
[the development harness plan](docs/development-harness.md). Build and play
instructions are in [README.md](README.md).

## Current state

Prototype 01 uses Godot 4.5.2 .NET and C# on .NET 8 (SDK 8.0.425). It implements
a single-player first-person test bed. The original s&box project is
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
- `python tools/dev.py check`: build, import, and run the bounded physics scenario.
- `python tools/dev.py run`: launch the graphical test bed.
- `python tools/dev.py export --target windows`: package the Windows x64 build.
- `python tools/dev.py export --target linux`: package the Linux x64 build.

Use `--godot` or `GODOT_BIN` for a non-PATH editor. Local tools under `.tools/`
are discovered automatically. Keep `.tools/`, `.godot/`, and `artifacts/` out
of Git. Successful export is not evidence of Windows runtime playtesting.
