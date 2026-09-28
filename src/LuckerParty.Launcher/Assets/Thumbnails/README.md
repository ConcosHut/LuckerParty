# Instance thumbnails

The sidebar thumbnails are original native Avalonia vector artwork in
`InstanceVisuals.cs`; this directory contains their design provenance rather
than raster files. No external image or font license is needed for the shapes.

The reference is the approved Party Room sidebar mockup and the bundled
`party-room-arena.png` illustration. Each capsule uses its pink, orange or purple
palette, a light source above and to the left, darker shading on the right,
a soft floor shadow, and a small tinted isometric tile. The thumbnail's tinted
rounded panel separates it from the ivory instance card.

The native control draws at a 68 × 90 unit reference size and scales to its
actual bounds, avoiding enlarged raster assets for small tiles and keeping
Windows DPI changes sharp. The result is an illustrated approximation of the
arena's 3D material, not a crop or photorealistic render of the generated hero.

Eye and close action icons are original vector paths in the same source file.
