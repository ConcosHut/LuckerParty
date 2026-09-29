# Project brief

## Game concept

Lucker Party is a multiplayer party game inspired by Warcraft III custom maps
such as Are You a Lucker? and Uther Party, and Counter-Strike: Source minigame
maps. A session selects a sequence of minigames. Players earn points according
to each game's rules, and cumulative points determine the session winner.

Minigames can emphasize luck, decisions under uncertainty, skill, or a mixture.
Random events and modifiers provide variation.

## Perspective and modes

Individual minigames can use first-person or overhead cameras and their own
controls. Player identity and session scores persist across games.

Desired session experiences:

- Party: a sequence of minigames with cumulative scoring.
- Practice: select a game and retry it quickly.
- Sandbox: experiment, debug games, or hang out with friends.

The intended direction includes playlists selected randomly, by pack or by tag.
Each minigame can use its own art style and characters to faithfully capture a
small, recognizable part of a familiar game. Party UI, names, scores and optional
player colors provide continuity; a universal player character is not required.
Game-wide and individual positive/negative modifiers apply only where supported
by that game's rules. Profiles and persistent statistics are longer-term features.

## Technical direction

The original prototype uses s&box and C#:
https://github.com/ConcosHut/LuckerParty.

Prototype 01 uses Godot 4.5.2 .NET with C#, targeting Windows and Linux x64.
The portable core remains a plain .NET 8 project. A future engine migration or
custom host remains possible; no ECS library is needed for this iteration.

Desired separation: keep rules, scoring, round flow, and random selection
independent of the engine. Rendering, input, physics, scenes, and network
transport belong in engine integration code. Actual portability depends on
language/runtime compatibility and the amount of engine-specific gameplay.

## First milestone

Cross-platform development and compilation are requirements. The initial setup
must work on this Linux development machine and the user's Windows PC, with a
short edit/build/playtest loop on Windows. Additional platform targets should
be agreed explicitly.

The first iteration implements a placeholder 3D arena, a first-person character,
colored solid boxes, walking, sprinting, jumping, mouse look, reset, and a menu.
Build/run commands and Windows/Linux export presets are included. Overhead
cameras remain a later addition.

The first multiplayer sandbox supports eight players with direct-IP ENet hosting,
server-owned movement, named capsules and a menu. See [multiplayer](multiplayer.md).
Multiplayer is a core design principle: gameplay rules must define network
ownership and late-join/disconnect behavior. Complete minigames, matchmaking,
relays and the full party framework follow later.

## Next milestone (planned)

The [multiplayer party MVP plan](party-mvp-plan.md) reviews the legacy lobby/UI
and proposes a real lobby, portable party/scoring flow and three small games:
rock-paper-scissors, lucky doors and a 2D dodge game. Build one complete party
with RPS first, then extend it to multiple game presentations. The shipped
sandbox remains the starting point; these features are not implemented yet.
