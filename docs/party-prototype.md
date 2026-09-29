# First multiplayer party: Rock-paper-scissors

The first party iteration implements a real lobby and one complete minigame.
Lucky Doors and Dodge Drop remain the next additions in
[the MVP plan](party-mvp-plan.md).

## Play

1. Enter a display name and choose **Host party**, or enter the host's address
   and port and choose **Join**. Default UDP port remains 27015.
2. Choose an unused player color. Clients may choose **Spectate** or **Join players**
   while in the lobby; the listen host remains an active player.
3. Every active player chooses **Ready up**, including the host. With at least
   two active players ready, the host can choose **Start party**.
4. Read the instructions, then choose Rock, Paper or Scissors once per throw.
   Choices stay hidden until everyone locks in or the eight-second timer ends.
5. See the reveals, awarded points and party champions. The host chooses
   **Play again** to return everyone to the same lobby without reconnecting.

Capacity is eight connected clients including host and spectators. Host can
configure one to five throws; default is three. Changes reset readiness.
The launcher multi-instance setting makes same-PC tests easy: host in one
window, join `127.0.0.1` in the others. Existing LAN/firewall/port-forwarding
requirements in [multiplayer.md](multiplayer.md) remain.

For each throw, your hand competes with every other active participant's hand.
Earn one win for each hand you beat or each opponent who missed their choice.
Equal hands draw. Most wins across throws determines placement. First/second/
third earn 10/6/3 points; other valid participants earn 1, and no participation
earns 0. Ties share an award and skip occupied places (1, 1, 3). Equal final
totals crown joint winners; all missing participants produce no champion.

Tab displays standings; Esc toggles the session menu. Name changes, resume and
leave are available there. Alt-Tab keeps the current party screen visible; it
never opens the menu. Opening the menu never pauses shared clocks.
Late joins spectate this party and can join players in the next lobby. A departed
player's results remain visible during play, but a new connection cannot inherit
them or receive its crown. Below two active players the current game cancels
without an award and returns to lobby. Host departure ends the connection;
reconnect recovery and host migration are not implemented.

**Multiplayer sandbox** and **Practice offline** retain the 3D test bed.
Existing command-line `--host`/`--server` launches continue to select sandbox;
add `--party` to a listen-host launch for Party. Dedicated `--server --party` is rejected; party administration
without a participating host is follow-up work.

## Implementation

- Portable core: `PartyCoordinator` contains readiness, roles, phases, hidden
  hands, outcomes, scoring, cancellation and replay. `PartySnapshot` is a
  recipient-specific projection, not a serialization of secret rule state.
- Godot transport: `PartyNetwork.cs` shares the stable `MultiplayerSession` RPC
  node; caller identity comes from ENet, and server-only RPCs publish state.
  Settings/Start/replay are host-only. Choice commands identify party/throw and
  sequence; invalid, late, duplicate, spectator and stale commands are rejected.
- Party members no longer require an avatar. Sandbox members retain their
  existing `FirstPersonPlayer`; movement snapshots and 240 FPS camera history
  remain separate. Wire protocol is now **2**, still using sandbox revision v1.
- Party heartbeats keep idle UI/spectators alive independently of movement input.
  State changes and one-second clock refreshes use reliable, bounded snapshots.
- UI: editable `UI/PartyShell.tscn`, shared `PartyTheme.tres`, bundled brand/fonts
  and `PartyView.cs`. Pointer presses survive roster refreshes; open menu fields retain focus and
  Tab/Esc shortcuts run before GUI navigation; choice callbacks
  retain the displayed party/throw identity. Connections/scores outlive screens.

## Focused verification

```text
python tools/dev.py check --suite core
python tools/dev.py check --suite party
python tools/dev.py check --suite party --graphical
python tools/dev.py check --suite game
```

The party suite builds/imports the game, checks portable rules and runs real
host/client party scenarios. It skips unrelated launcher and movement scenarios.
`--graphical` renders one host with headless clients, exercises actual viewport
pointer events and captures lobby/instructions/choices/podium/menu. A single
rendered host avoids saturating the Windows VM with multiple software renderers.
Default/all and game suites also run the party network scenarios.

Windows VM: `python tools/windows_vm.py start`, then `check --suite party`, then
`stop`. The focused check runs through the existing limited interactive task,
captures game views and tests the party over multiple native Windows processes.
It does not run launcher/installed-update qualification; CI still does so before
publication. See [Windows VM](windows-vm.md).

The trusted local driver can shorten clocks for headless checks and produce
isolated JSON probes/logs under `artifacts/party-checks/`; normal launches do not
read these files. Debug clock configuration cannot be submitted over ENet.
Checks cover ownership, colors, ready/spectator state, private hands, obsolete
commands, three throws, tied points/champions, timeouts, replay and disconnects.

Automated results do not establish fun, real WAN latency or hardware input feel.
Playtest those with friends before balancing or expanding the playlist.

## Implementation evidence — September 29, 2026

- Linux game suite passed the existing movement/camera and 45 sandbox network
  scenarios alongside the new party flow. The final focused party pass completed
  37 real-process scenarios, including viewport pointer clicks and Tab/Esc
  routing after a button has focus.
- Native Windows 11 VM run `run-1790697614725049584` passed 43 scenarios using
  normal gameplay clocks, one rendered host and headless clients. Reviewed the
  1280×720 menu, lobby, instructions, private choice and joint-podium captures.
  Tab/Esc were tested through the viewport input pipeline after a button had focus.
- The standalone component preview loads headlessly without errors. Core checks
  include eight-member capacity/unique colors, all RPS outcomes, secrecy,
  timeout boundaries, tied awards, replay and disconnect policies.

Two failures improved the harness: refreshing controls during a held mouse
press could lose the release; the UI now coalesces redraws until the press ends.
Tab standings now runs before GUI keyboard navigation can consume the key.
A graphical timeout assertion initially allowed exactly the sum of the normal
choice/reveal/results clocks; it now adds scheduling/replication allowance.
Keep these checks focused; the normal two-platform CI release gates still cover
launcher and installed-update behavior.

## Published build

[Beta 34 — 0.3.0-beta.34](https://github.com/ConcosHut/LuckerParty/releases/tag/v0.3.0-beta.34)
ships this milestone from commit `396b23c8f778b075ccf8c6a6af58227c4d841e19`.
[Release CI](https://github.com/ConcosHut/LuckerParty/actions/runs/36594543590)
passed the full game/launcher checks, real installed updates and player packaging
on Linux and Windows before publishing both platforms together. Beta 33 was
cancelled before publication to include the keyboard shortcut fix.

Choose Beta in the launcher and Update, then Play. Every participant must use
the new protocol-2 build. Human friend/WAN playtesting remains outstanding.
The dedicated Windows VM was shut down after verification.

The later [party UI concepts](design/party-ui-concepts/README.md) explore a
simpler Home → Join → Lobby flow and a compact in-party menu. They are mockups;
the shipped UI still uses the native PartyShell scene.
