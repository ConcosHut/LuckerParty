# Development harness plan

## Purpose and source

Make development changes easy to build, exercise, inspect, and reproduce while
keeping the project understandable to the human team.

Inspired by OpenAI's February 11, 2026 article:
[Harness engineering](https://openai.com/index/harness-engineering/).

This plan adapts those ideas for Lucker Party. Prototype 01 implements the pinned
toolchain, shared command wrapper, core dependency boundary, bounded movement
scenario, screenshot capture, and Windows/Linux packaging. CI, installed-update checks and multi-process multiplayer checks are implemented.
Seeded minigames and round/scoring events remain future work. See
[prototype verification](prototype-01.md) and [commands](../README.md).

## Small initial foundation

Implemented in the initial engine template:

1. Pin the engine and language toolchain versions.
2. Document and verify commands to build, run, and perform a bounded smoke check.
3. Make failures return a nonzero exit status with an actionable message.
4. Support restarting the initial scene without restarting the whole workflow.
5. Keep AGENTS.md short and link to design and architecture details.

Use local verification first. Add the same checks to CI when the project has a
repository and CI setup. A smoke check must exercise startup, not merely compile.

## Windows iteration and cross-platform builds

Initial required development platforms are Linux and Windows. Godot/C# is
selected; use the same pinned Godot .NET edition and compatible .NET SDK on both.
Use matching .NET export templates for packaging.

Provide a documented Windows loop: sync the same source revision, build, and
run the project directly in Godot. Provide repeatable Windows and Linux exports
for playtesters who do not install development tools; package all output files
together, including the C# runtime/data dependencies, rather than only the EXE.

Use cross-platform tooling for shared build logic. Shell wrappers may provide
convenience but must not make Bash a Windows prerequisite. Keep paths portable,
asset filename casing consistent, and native dependencies available for each
target architecture.

When CI is available, run core/build checks on Linux and Windows and produce
playtest artifacts. Verify Linux-to-Windows export against the selected engine
version early; provide a native Windows build path as well. Record actual Windows
runtime results separately from successful compilation/export. Routine native
Windows checks run in the dedicated [QEMU/KVM VM](windows-vm.md), including real
installation/update tests and desktop launcher inspection. Its private state is
shared across checkouts; the helper serializes runs and preserves active tests.
Graphical hardware playtesting uses the configured SSH connection and interactive
desktop task on the user's PC; see [Windows playtesting](windows-playtesting.md). The Linux chat
can transfer builds, trigger launch, and inspect Windows status/logs. Process
launch evidence does not replace a human check of rendering and controls.

Documentation:
- [Godot C# setup and desktop support](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html)
- [Exporting projects](https://docs.godotengine.org/en/stable/tutorials/export/exporting_projects.html)
- [Windows exports](https://docs.godotengine.org/en/stable/tutorials/export/exporting_for_windows.html)

## Enforce the engine boundary

The template has a plain C# core and a Godot integration project. The integration
depends on the core; the core cannot depend on Godot or engine integration code.
The command wrapper checks the core's project/package references before running
the game or packaging builds.

Pass simple commands and events across the boundary. Keep engine objects out
of shared player records, scoring rules, and session state.

Keep physics and camera behavior in the integration initially. Do not promise
identical physics across engines or adopt an ECS library without a concrete
gameplay need.

## Reproduce gameplay bugs

As the first minigame is implemented, add a scenario entry point that can choose
the game, configuration, modifiers, and random seed directly. Record these
values with failures, along with build identity and relevant player actions.

Give core rules an explicit source of time and randomness. A seed alone does
not reproduce engine physics, asynchronous networking, or changes in input
timing. Begin with repeatable core-rule scenarios; expand recording only when
a bug demonstrates the need.

For example, a scoring scenario could eliminate player A, time out the round,
and verify that survivor B receives the configured award exactly once. Another
could verify that a player leaving during a round cannot stall completion.

## Observe the running game

Expose a developer overlay and structured event logs as the relevant state is
implemented: current game, session phase, seed, timer, scores, and authority.
Include minigame start/end events and the reason each score changes.

Build a small input/scenario driver so automated checks can exercise camera
switching, reset, and later round transitions. Capture screenshots for visual
inspection where useful. Headless rule checks do not validate rendering,
controls, or camera behavior; those require running the graphical game too.

Practice and sandbox should provide shared shortcuts for selecting a game,
forcing a modifier, inspecting state, and resetting. Reuse the gameplay rules
used by party sessions.

## Expand alongside features

- First template: build, bounded startup check, camera/reset scenario, run docs.
- First minigame: core-rule scenarios, seed selection, score/event inspection.
- Multiplayer: launch isolated host/client instances on configurable ports;
  check shared state, score authority, transitions, and disconnect handling.
- More minigames: exercise cleanup of cameras, objects, timers, and subscriptions
  between games; share proven utilities instead of copying implementations.

Tests should cover meaningful outcomes and failure cases. Keep the harness
small; add tools when they remove a recurring source of uncertainty or effort.

Use the [testing strategy](testing-strategy.md) to select focused local checks.
The default full suite remains the release gate; it is not the edit loop for
every layout adjustment. Native Windows desktop checks and installed-update
checks establish different properties and should be triggered by relevant
changes rather than repeated automatically alongside the same passing CI checks.

## Human feedback

Automation can verify rules and catch regressions. The team should playtest
whether games are fun, controls feel good, instructions are clear, and unlucky
outcomes are entertaining. Record useful feedback as concrete changes or
acceptance criteria. Keep reported automated evidence separate from subjective
playtest conclusions.
