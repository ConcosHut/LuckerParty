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

Themed playlists are a proposed addition, not a settled requirement.

## Technical direction

The original prototype uses s&box and C#:
https://github.com/ConcosHut/LuckerParty.

The team is considering a different engine to improve automated development.
Godot with C# is the current recommendation. ECS-oriented alternatives and a
custom host using libraries such as raylib or SDL remain under consideration.
No engine or language has been selected definitively.

Desired separation: keep rules, scoring, round flow, and random selection
independent of the engine. Rendering, input, physics, scenes, and network
transport belong in engine integration code. Actual portability depends on
language/runtime compatibility and the amount of engine-specific gameplay.

## First milestone

Create a very basic runnable template in the selected engine. Proposed scope:
a placeholder 3D room, a controllable character, first-person and overhead
camera options, a reset action, and documented build/run commands.

Networking, complete minigames, and the full party framework follow later.
Player count, distribution platforms, and joining/hosting infrastructure remain
open design questions.
