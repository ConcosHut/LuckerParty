# Lucker Party

Start with [the project brief](docs/project-brief.md) and
[the development harness plan](docs/development-harness.md).

## Current state

This workspace contains planning documents only. The original s&box project is
https://github.com/ConcosHut/LuckerParty. Engine and language selection remain
open; Godot with C# is the current proposal, not an implemented decision.

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

Build and run commands do not exist yet. Add exact, verified commands when the
template is created; do not present planned commands as working tooling.
