# Multiplayer sandbox

Multiplayer is a core principle: the host owns player membership, movement and
collision outcomes. Future minigames, random outcomes and scoring must define
server ownership, late-join state and disconnect behavior too.

## Play

Choose **Beta** in the launcher for the multiplayer prototype. In the game:

1. Enter a display name (up to 24 characters).
2. Choose **Host lobby**, or enter the host's IP/hostname and choose **Join lobby**.
3. Use the same port on both machines; the default is **UDP 27015**.

Hosting opens the existing arena immediately, as a playable sandbox lobby.
Other players are colored capsules with names overhead. Up to eight players can
join, including a listen host. WASD, mouse look, sprint, jump and reset work.
Players collide with the world; they pass through each other in this iteration.
Names may duplicate and are display names, not authenticated accounts.

Esc opens the session menu. Change your name there, resume, leave, or close your
hosted lobby. Your menu/focus loss stops your inputs without pausing everybody.
Closing the host ends that lobby and returns clients to the main menu. There is
no host migration. A failed connection returns to an editable, usable menu.
**Practice offline** retains the existing movement/diagnostic test bed. F5's
slow-physics comparison is available only there; multiplayer runs at 60 Hz.

Name, last server address and port are stored in Godot's `user://profile.cfg`
under its Lucker Party user data directory, outside the replaced installation.
These preferences are shared by game builds/channels. Automated checks use
explicit isolated control files and do not overwrite a human profile.

## LAN and internet

On the same machine, use `127.0.0.1`. On a LAN, use an address shown by the host's
HUD, such as `192.168.0.215`. If the host has several interfaces, use the address
reachable from the client's network. Hosting must be allowed through its local
firewall. Windows may ask to allow the game; select the intended network profile.
A host firewall rule needs the selected **UDP** port, not a TCP rule.

For friends outside your LAN, the host needs a reachable public address and a
router rule forwarding that UDP port to the hosting machine's LAN address.
Clients join the public address. Network arrangements without a reachable public
address need another approach, such as a relay/VPN, outside this prototype.
No automatic router configuration, public server, accounts, matchmaking or relay
is included. Tests do not prove internet/NAT connectivity.

See [Godot 4.5 hosting considerations](https://docs.godotengine.org/en/4.5/tutorials/networking/high_level_multiplayer.html#hosting-considerations)
and [ENet's UDP transport](https://docs.godotengine.org/en/4.5/classes/class_enetmultiplayerpeer.html).

## Headless server and direct launch

The same exported game can host without a desktop/GPU. Keep every exported file
together. Run the game executable, rather than the player launcher, for a server:

```text
LuckerParty.x86_64 --headless -- --server --port 27015
LuckerParty.exe --headless -- --server --port 27015
```

`--server` has no fake host player and accepts eight actual clients. Run only one
server per port. A failed command-line host exits nonzero. A direct graphical
host/client can also start with `-- --host --name Alice --port 27015` or
`-- --join 192.168.1.100 --name Bob --port 27015`. Quote names containing spaces.

Direct server launches bypass the updater's session guard. Stop that server
before replacing its export/installation; use separate complete versioned
exports for servers and player installations. Persistent server service setup is
not included.

## Architecture

- `GameRoot`: main menu, saved profile, scene lifetime and practice entry.
- `MultiplayerSession`: persistent `/root/Main/Network` RPC node, ENet transport,
  membership, handshake, inputs and snapshots.
- `FirstPersonPlayer`: host physics, local prediction/reconciliation, independent
  camera presentation history and remote capsule interpolation.
- Plain C# core: bounded input wire format and display-name rules, without Godot.

Protocol 1 and arena revision `sandbox-v1` are negotiated separately from package
versions. A new peer registers once; the server validates its name and assigns a
spawn slot. Reliable roster/name updates use channel 0, input batches use channel
1 and snapshots channel 2. Clients cannot select the peer whose inputs they
control; the server uses the actual RPC sender. Only the server publishes roster
or snapshots. Client-supplied transforms are not accepted.

Physics runs at 60 Hz; snapshots at 30 Hz. Up to 32 recent unacknowledged inputs
are redundantly sent to recover ordinary UDP loss. The server keeps a bounded
queue and consumes one input per physics tick. More packets cannot produce more
simulation time. Input size, sequences, finite values, angles, flags, membership
and packet rate are checked. Name changes are bounded/rate-limited.

The local camera interpolates its own presentation samples, independently of
reconciliation. Corrections decay over time; teleports clear history. Repeatedly
resetting Godot's body interpolation on each snapshot would reintroduce stepping
on a 240 Hz display. Remote capsules smoothly follow authoritative snapshots.
Prediction is a prototype and still needs human tests under real WAN conditions;
there is no combat lag compensation, encryption/account identity, or anti-cheat
service. The host is trusted. Keep physics/rule changes compatible across peers,
or advance the negotiated revision.

## Reproduce checks

```text
python tools/dev.py check
python tools/test_multiplayer.py --executable /absolute/path/to/LuckerParty.x86_64
python tools/test_multiplayer.py --executable C:\path\to\LuckerParty.exe
```

The full wrapper runs movement/camera, launcher guard, input/name rules and real
multi-process networking checks. The harness selects a free local UDP port and
starts independent game processes, using atomic control/state files under
`artifacts/multiplayer-checks/`. It checks listen/dedicated hosting, late joins,
rename, authoritative movement/jump/collision/reset, 240 FPS camera stepping,
80 ms simulated RTT with jitter/5% loss, sender input pressure, leave/rejoin,
protocol rejection, full-server rejection, freed-slot joining, host departure,
failed join and recovery. It exits nonzero with state/log evidence on failure.
It closes only the child processes it started.

`--probe-file`, `--control-file`, `--network-camera-check`, `--exit-after`,
`--menu-capture` and `--arena-capture` are scenario/debug entry points, not normal
player UI. Viewport capture needs a graphical display. Machine-local Windows
verification uses SSH and isolated exports, without replacing active playtests.

## Verification recorded September 28, 2026

Linux and the actual Windows PC passed 44 multi-process ENet scenarios, including
loss/jitter, server-time input flooding, killed-client cleanup, capacity and
recovery. The final local check also covers a rate-limited name request restoring
the server's accepted name in the client UI/preferences. Gameplay's 15 assertions,
240 FPS camera scenario, core codec/name rules and launcher guards also passed.
CI runs these checks on both platforms before each Beta publication.

Network camera samples on Windows at a 240 FPS target: raw 70/119 stationary
moving frames; interpolated 0/119. Under simulated 80 ms RTT with 5% loss, the
interpolated camera had 0/164 stationary moving frames. Linux comparisons also
passed. These are bounded scenarios, not a complete WAN/input-feel evaluation.

The actual Windows graphical client joined a Linux host over the machines'
existing Tailscale connection, with a third Linux client present. The Linux host
observed Windows movement, the client saw all three members and named capsules,
and the viewport capture showed 240 FPS / 60 physics Hz. Names keep a readable
screen size at distance. The main menu was also rendered/captured on Windows.
All temporary desktop verification tasks and scenario processes were removed;
the existing player and ZIP playtest tasks remain.

Direct traffic to the box's LAN IP was blocked by its active UFW policy; the
kernel log confirmed dropped packets from the Windows PC to the test UDP port.
No firewall/router changes were made. To host on the box's LAN address at the
normal port, an administrator can inspect UFW and deliberately allow the client:

```text
sudo ufw allow from 192.168.0.244 to any port 27015 proto udp
```

That example permits only the configured Windows PC. Other friends/networks
need an appropriate host rule. The existing Tailscale addresses also work for
machines already on that network. Internet routing, Windows inbound firewall
configuration on other PCs, real-world input feel and wider platform coverage
still need playtests.
