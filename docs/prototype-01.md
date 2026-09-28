# Prototype 01

The initial Windows build was reported working by the user, with strafing jitter
at 240 FPS. Prototype 01.1 adds camera interpolation and diagnostics. Its current
checks and investigation are in [movement-jitter.md](movement-jitter.md).
The verification below records the initial iteration.

## Implemented

- Godot 4.5.2 .NET, C#, .NET 8, SDK 8.0.425.
- First-person capsule controller: six-meter/second walking, nine-meter/second
  sprinting, jumping, gravity, and solid collisions.
- Mouse look with an 85-degree vertical limit and window-size-independent
  sensitivity; reset restores position, velocity, and view.
- A 32-meter square arena with perimeter walls, a two-meter floor grid, red,
  blue, and yellow boxes, and a low green step.
- Controls displayed on screen, crosshair, FPS/ground-contact indicator, and
  an Escape menu with resume, reset, and quit. Losing focus opens the menu.
- Portable C# movement settings; engine-dependent movement/collision belongs
  to the Godot integration.
- Shared build/run/check/export wrapper and complete Windows/Linux ZIP exports.

## Verification

- Debug build: passed with zero warnings and errors.
- Headless scenario: all 13 checks passed against the actual controller and
  scene colliders (floor contact/height, walking speed, diagonal speed, sprint,
  box collision, jump, landing, disabled controls, look rotation/limit, reset,
  and R-key reset).
- Graphical Linux launch: scene rendered and viewport screenshot inspected.
  Screenshot capture is visual evidence, not a performance benchmark.
- Windows x64 cross-export from Linux: completed without exporter errors.
  Executable identified as Windows x64 PE; ZIP integrity verified and packaged
  application/core assemblies and bundled .NET runtime checked.
- Linux x64 release export: completed; the packaged executable passed all 13
  checks using its bundled runtime, without the development SDK environment.

The Windows executable has not been run on an actual Windows PC in this session.
Keyboard/mouse feel, Alt-Tab behavior, window resizing, and GPU compatibility
still need Windows playtesting. See [PLAYTEST.txt](PLAYTEST.txt).

## Output locations

Generated artifacts are ignored by Git and can be recreated with `tools/dev.py`:

- `artifacts/LuckerParty-prototype-01-windows-x64.zip`
- `artifacts/LuckerParty-prototype-01-linux-x64.zip`
- `artifacts/prototype-01.png`

The command wrapper now exports version 01.1 ZIPs. Original version 01 ZIPs are
retained locally for reference.

Keep all extracted executable, resource-pack, and runtime/data files together.
