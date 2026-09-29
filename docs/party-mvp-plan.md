# Multiplayer party MVP — implementation plan

Status: proposed, September 29, 2026. The shipped game remains the multiplayer
sandbox; this document plans the next gameplay milestone.

## Goal and product direction

Two to eight friends can host/join, ready up, play three short minigames, see
scores accumulate, crown a winner, and return to the same lobby for another
party. Keep direct-IP ENet, Windows/Linux builds and the existing launcher.
Capacity stays eight connected clients, including the listen host and spectators.

The longer-term vision is a party harness containing small, faithful slices of
familiar games: Source minigame maps, Warcraft custom maps, MapleStory-style 2D
games and other genres. Individual games own their camera, controls, characters
and art direction. The shell provides identity, instructions, playlist progress,
scores and results. Player color belongs in the shell and games that choose to
support it; a universal character or forced cosmetic overlay is unnecessary.
The three original test games below validate this structure before investing
in larger recreations.

Later parties can select random games, packs or tags. Modifiers may affect a
whole game or one player, including positive/negative rolls, where supported.
Profiles and persistent statistics follow a reliable party loop; this MVP needs
session identity and results, not an account service.

## Legacy findings

Reviewed `origin/legacy-2` at `e0f6f6b8c654bb9f6fbe607569f47038ffc9ac27`;
the remote branch matched the local reference.

| Source | Keep or reinterpret |
| --- | --- |
| `Docs/game-design.md` | Lobby → round consisting of several minigames; client/player/spectator distinction; host settings and start countdown. |
| `code/UI/Lobby.razor`, `ClientList.razor` | Active/spectator lists, own Ready/Unready action, host-only Start and explicit role changes. |
| `code/LobbyManager.cs`, `Client.cs` | Caller ownership validation for readiness and role changes. Preserve the policy, not s&box component membership. |
| `code/NetworkManager.cs` | Server-owned connection/membership lifetime. Current Godot networking already covers much of this. |
| `code/GameManager.cs` | Lobby/round lifecycle and manager cleanup. Implement finer phases in portable rules. |
| `code/RoundConfiguration.cs`, `RoundManager.cs` | A minigame-count field and Intro/Running/Outro skeleton; no implemented playlist/scoring loop to port. |
| Razor/SCSS screens | Interaction reference. Rebuild with finalized branding instead of the old styling/remote clover texture. |

Legacy calls the entire playlist a **Round**. Use **party** in player-facing UI,
**minigame** for an entry and **throw/attempt** within a game such as RPS.

## UI approach

Use native Godot UI with C# behavior. Scene files are text, inspectable in Git
and editable visually. `Control` nodes supply widgets; nested containers supply
layout; shared `Theme` resources supply fonts, spacing and style variations.
`StyleBoxFlat` supports rounded corners, borders and shadows. `CanvasLayer`
provides the screen-space shell over different game worlds.

Sources: [Godot 4.5 themes](https://docs.godotengine.org/en/4.5/tutorials/ui/gui_skinning.html),
[containers](https://docs.godotengine.org/en/4.5/tutorials/ui/gui_containers.html),
[StyleBoxFlat](https://docs.godotengine.org/en/4.5/classes/class_styleboxflat.html),
[theme previews](https://docs.godotengine.org/en/4.5/tutorials/ui/gui_using_theme_editor.html),
[keyboard navigation](https://docs.godotengine.org/en/4.5/tutorials/ui/gui_navigation.html).

Recommended resources/scenes under the Godot project:

- `UI/PartyTheme.tres`: existing navy, ivory, sand, coral, green and lilac,
  licensed bundled fonts and finalized brand assets.
- Reusable action button, player row, game card, instructions, score row and
  modal scenes, with consistent hover/pressed/disabled/focus states.
- Main menu, lobby, instructions, per-game results and final podium screens.
- Persistent overlay: game number/title/timer, Tab standings and Esc menu.
- `UI/ComponentPreview.tscn`: preview all shared components/states directly,
  without connecting multiple clients for every visual adjustment.

Lobby layout: roster on the left; playlist and host settings on the right;
Ready/Start along the bottom. Show the join address with a Copy action and
explain unmet Start requirements. Instructions describe objective, controls and
scoring. Results show outcome, earned points and total; final results crown
all tied winners. Host Play again returns everyone to the existing lobby.

Share art/colors with Avalonia, but keep the game theme native to Godot. Native
UI is sufficient for the proposed screens. Do not introduce Chromium/Electron
or a Razor compatibility layer. Reconsider only when a concrete required
component demonstrates a limitation; small custom Godot controls/shaders are
available when the design needs them.

## Proposed flow and defaults

`Lobby → Start countdown → Instructions → Playing → Minigame results`
repeats for the playlist, then `Final results → Lobby`.

- Default playlist contains each of three games once in shuffled order.
  Develop with fixed order first, then expose a host inclusion list/shuffle.
  Require at least one game; defer repeats, packs, tags and voting.
- Start requires a listen host, at least two active players and all active
  players ready, including the host. Spectators do not block Start.
- Host playlist edits reset readiness; joins enter unready. Roster/configuration
  changes during start countdown cancel it.
- Three-second start countdown, about five-second instructions, bounded game
  timers and about four-second results. Final results wait for host Play again.
  Keep timing configurable and instructions accessible during gameplay.
- Placement awards: 10/6/3 points for first/second/third; 1 for other valid
  participants; 0 for no valid participation. Ties share the award and skip
  occupied ranks (1, 1, 3). Every game's maximum award is the same; raw scores
  determine placement rather than contributing directly to party totals.
- Equal final totals produce joint winners. A sudden-death game can come later.

These are implementation defaults, not final balancing decisions.

## Sample games

| Game | Rules/presentation | Purpose |
| --- | --- | --- |
| Rock-paper-scissors | Three simultaneous throws. Lock one hidden hand per throw; reveal and compare against every other submitted hand. Count pairwise wins for placement; equal hands draw and a missing hand forfeits comparisons. A 2D card/table UI. | Private state, submissions/deadlines, reveals and multiplayer ties without a tournament bracket. |
| Lucky doors | Three attempts selecting one of three doors. Server secretly shuffles rewards of 0/1/2 behind them each attempt, accepts one locked choice per player, then reveals for everyone. Players may share a door; total rewards determine placement. | Server-owned randomness, independent choices and shared reveals. |
| Dodge Drop | Simple 2D three-lane arena; left/right changes lane. Warn before falling blocks; every hazard wave leaves a safe lane. Survive up to 20 seconds. Server determines elimination; survival time determines placement and survivors tie. Colored shapes/names suffice. | Authoritative timed gameplay, spectating and changing presentation from the 3D sandbox. |

Missing RPS choices can only award pairwise wins to players who actually
submitted a valid hand. All missing players cannot earn shared winner awards.
Define timeout/disconnect behavior with each rule implementation. Keep hidden
choices, unrevealed rewards and future random seeds private until appropriate.

## Architecture

### Portable C# rules

Add a small party model: identities/roles/readiness, configuration, playlist,
phase, party/game instance IDs, deadlines, results and total scores.
`PartyCoordinator` validates commands and advances phases; each minigame has
its own rule implementation. A small explicit catalog supplies game IDs,
instructions and player limits. Avoid dynamic plugins/universal engine wrappers.

Rules receive explicit simulation time and server-owned randomness. Keep Godot
objects, widgets, physics and ENet peers out of the core. Full server rule state,
public snapshots and recipient-specific private state are distinct: never
serialize the entire rule object to clients. Scenario seeds reproduce rules,
but gameplay snapshots must not disclose future outcomes.

### Godot integration

Current `SessionPlayer.Avatar` is required, and `SpawnPeer` always creates a
`FirstPersonPlayer` in `TestBed`. Separate connection/party membership from
avatar lifetime. Keep proven 3D movement/prediction in its adapter; choice games
need no avatar, while Dodge Drop owns a separate 2D view. Preserve the sandbox
as a selectable practice/development mode.

Keep RPC nodes at a stable path across game scene changes. Add party coordination
as a separate responsibility; do not turn `TestBed` into the whole game. A scene
host mounts/unmounts the current game's view. UI observes accepted state and
sends intent; it does not award points or advance authoritative phases.
On transitions remove game nodes, timers, subscriptions, cameras, input routes
and pending commands; restore mouse state. Membership/scores remain alive.

Public Party MVP uses a listen host. Preserve current dedicated sandbox hosting;
dedicated party administration is follow-up scope. Headless scenario hosts still
drive party verification through the trusted automation harness.

### Network policy

Server owns membership, readiness, colors, roles, phase/deadlines, playlist,
random outcomes, eliminations and scores. Clients request their own settings,
readiness, role or input. Verify host-only Start/settings on the server, beyond
hiding UI controls. Colors use a bounded palette with unique server fallbacks.

Assign a session player ID separately from reusable peer IDs/spawn slots.
Commands carry party/game instance IDs and input sequences; validate actual
sender, membership, phase, bounds and rate. Reject obsolete game input and apply
each game's scores once. Advance negotiated protocol/revision for new wire data.

Reliable state updates carry roster, transitions and results. Real-time game
snapshots are separately sequenced; clients interpolate presentation while the
server ticks at 60 Hz. Preserve independent 3D camera history. Joining receives
complete public state plus permitted private state; don't depend on replaying
past events. Bound packet size/rate and keep party traffic separate from the
existing movement decoder.

Late joins spectate until the next party. They receive phase/deadline, public
game state and standings but cannot submit active game input. Disconnects mark
players departed, preserve earned scores and stop waiting for their input. New
connections cannot inherit points; departed players cannot receive the crown.
Below two active players, cancel the current game without an award, explain why,
and return to lobby. Host departure ends the party as today; defer migration
and reconnect identity recovery.

Esc/menu/focus loss stops local input, never shared simulation/deadlines. Locked
choices remain locked; missing input follows the game's displayed timeout rule.

## Implementation sequence

1. **Real lobby/UI foundation.** Extract reusable UI scenes/theme/preview;
   decouple members from mandatory avatars; add roles, colors, readiness and
   host controls over ENet. Deliverable: two clients agree on lobby state,
   validation and joining, while sandbox movement remains available.
2. **Complete RPS-only party.** Implement coordinator, private choices, deadlines,
   results, totals, joint winners and replay. Deliverable: finish a party and
   return to the same lobby. This is the first playable milestone; get here
   before expanding the framework or catalog.
3. **Lucky doors/playlist.** Add the second rule/view, enabled-game selection
   and shuffle, shared scoring, full snapshots and mid-party spectating.
   Deliverable: totals and membership survive switching games; secrets stay hidden.
4. **Dodge Drop.** Add the 2D view, authoritative lanes/hazards and elimination.
   Deliverable: three-game playlist mixes choice, chance and skill and agrees
   on a winner despite changing presentation.
5. **MVP finish.** Polish instructions/results/standings; add direct practice and
   isolated debug starts for each game; verify disconnect/cleanup/replay; publish
   a Beta for friends to test.

Optional after this loop: Slow Motion for Dodge Drop, explicitly supported by
the game and applied to its simulation clock. Don't change global network physics
or party/UI deadlines. Defer full modifier architecture, individual modifier
gambling, faithful game recreations, content packs, persistent stats, matchmaking
and custom engine work.

## Verification and acceptance

- Fast core scenarios cover RPS/reveal secrecy, seeded door rewards, timeouts,
  placement ties, exactly-once scores, transitions and 2D hazard/elimination rules.
- Multi-process scenarios cover shared lobby, host authorization, forged/stale
  input rejection, complete playlist, late spectators, disconnects in relevant
  phases and a second party on the same connections without leftover state.
- Capture actual menu/lobby/instructions, lock/reveal, 2D arena, standings,
  per-game results and tied podium at representative sizes. Use the component
  preview for UI-only edits.
- Focused core checks for rules, game checks for network/scene changes; don't
  repeat launcher/installed suites locally for unchanged distribution. Existing
  both-platform release gates remain. A targeted Windows VM desktop check
  validates game UI/input integration. Hardware feel and fun need human playtests.

Acceptance: 2–8 players finish all three games, agree on every score and winner,
return to lobby and play again without reconnecting. Late/missing/disconnected
players cannot stall or control the party. Logs record party/game/phase,
reproduction data and the reason for every score change.

Next action: implement milestone 1, followed immediately by the RPS-only party.
Review a populated lobby and first complete party before expanding the catalog.
No gameplay code or player release changes are made by this plan.
