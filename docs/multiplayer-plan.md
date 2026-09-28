# First multiplayer sandbox

Status: implemented. Linux/Windows multi-process checks and a real graphical
Windows-to-Linux connection passed. Delivered through the Beta pipeline; see
[the multiplayer runbook](multiplayer.md) for use and measured limits.

Multiplayer is a core requirement: identity, spawning, movement, disconnects,
and future round state must have explicit network ownership. Start with the
existing first-person arena as a playable lobby, rather than a separate waiting
room and minigame transition system.

## Scope

- Main menu: saved display name, server address, configurable port, Host lobby,
  Join lobby, practice, quit. Esc returns to a session menu with rename/leave.
- Up to eight players, first-person local camera, colored remote capsules and
  overhead names. Join an IP/hostname; no hardcoded public service.
- Listen host on Windows/Linux; optionally run the same game as a headless
  dedicated server. Late join, disconnect, timeout, reconnect, host departure,
  occupied port, and malformed requests have explicit outcomes.
- ENet/UDP 27015 by default. LAN first. Document firewall and manual UDP router
  forwarding; do not change routers, enable UPnP, or deploy a public service.

## Authority and boundaries

The server assigns player IDs/spawn slots and accepts bounded inputs only from
that input stream's actual sender. It simulates the movement/collisions at 60 Hz
and sends snapshots at 30 Hz. Clients cannot submit authoritative transforms.
Local prediction/reconciliation keeps mouse look and movement responsive; remote
capsules interpolate snapshots. Players collide with the arena, not each other,
for this prototype, keeping prediction practical. The host is trusted.

Name rules and the bounded input codec belong to the plain C# core. ENet/RPCs,
Godot physics, camera smoothing, scene lifetime, and UI belong to integration.
Keep a single persistent RPC node path across menu/game/leave/rejoin. Reliable
roster/name messages are separate from unreliable movement/snapshots. Negotiate
an explicit protocol/arena revision; a package version is not a protocol.

No accounts, matchmaking, relays, host migration, lag compensation for weapons,
or persistent dedicated hosting in this iteration. Future minigame/scoring state
will follow server ownership rather than trusting client outcomes.

## Verification and delivery

Retain movement/camera and installed-update checks. Add bounded multi-process
host/client scenarios with machine-readable state: membership, validated names,
client movement on the server, collision, reset, late join, rename, disconnect,
rejoin, host exit, failed connection, and occupied port. Include sender/codec
validation and prediction error observations. Run on Linux and Windows; verify
Windows-to-Linux actual networking over the existing SSH playtest connection
when possible. Do not interrupt a running human playtest.

Publish the multiplayer prototype to Beta after checks. Report automated evidence
separately from input feel and internet/NAT playtesting. The plan is implemented
incrementally; record actual results in the multiplayer runbook.

References: [Godot 4.5 high-level multiplayer](https://docs.godotengine.org/en/4.5/tutorials/networking/high_level_multiplayer.html),
[ENet peer](https://docs.godotengine.org/en/4.5/classes/class_enetmultiplayerpeer.html).
