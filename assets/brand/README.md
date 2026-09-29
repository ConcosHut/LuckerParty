# Lucker Party logo sources

The user finalized the Clover Peak emblem and selected the image-generated
**Leafcut** lettering direction on September 29, 2026. The selected lettering
has been converted to editable vector paths for review and future layout work.

- `lucker-party-logo.svg`: full stacked logo, a 1254 × 1254 canvas.
- `lucker-party-emblem.svg`: finalized crown/clover, a tight 1010 × 659 canvas.
- `lucker-party-wordmark.svg`: Leafcut lettering alone, a tight 1157 × 497 canvas.
- `status.json`: selection state and hashes for the protected sources.

Every SVG has a transparent background and actual transparent counters/gaps.
All contours are closed. There are no fonts, raster images, filters, masks,
external dependencies or required generation tools. The wordmark has eleven
individually named letter paths in `word-lucker` and `word-party` groups.
The A's star counter uses four cubic edges with sharp, single-node tips.
Letters remain separate; there are no RTY ligatures.

## Protected emblem

`lucker-party-emblem.svg` is finalized. Preserve its geometry, colors, star
opening and negative space during subsequent type/layout work. The upper
clover leaf replaces the former center ball. Crown stems are sharp and the
navy band is aligned with them; adjacent pieces have consistent clearances.
The leaf clefts and star have single corner nodes, without raster tracing ledges.

The full logo reuses its exact four path definitions inside a uniformly scaled
and translated group. The standalone emblem's bytes and recorded SHA-256 have
not changed in the Leafcut pass. Image generation's redrawn version of the
emblem is not used as the vector source.

## Palette

Yellow `#FFD166`, coral `#FF6B76`, green `#278568`, navy `#20243B`.
Cream `#FFF8EC` is a recommended presentation background, not baked into the
SVGs. Use a light background for the navy lettering.

## Leafcut conversion

The lettering was traced from the selected concept image, with generated
texture removed. Smooth runs were simplified conservatively; small raster flats
at the A star, K waist, lower R junction and Y cleft were replaced with deliberate
corners. The lettering retains 99.07% silhouette intersection-over-union with
the selected raster at native resolution and uses 207 segments instead of 466.
The smooth-curve fitting tolerance was 0.4 source units; explicit tip cleanup
is measured separately by the final silhouette comparison.

This is custom outlined logo lettering, not an installed font or full alphabet.
The launcher embeds these SVGs and renders their paths natively through
`BrandLogo`: stacked on the normal home screen, horizontal in Settings and the
multi-instance layout. The protected geometry and colors remain unchanged;
no texture, outline or widened gaps are applied. Application icon resources
are a separate follow-up.

## Exploration history

Ignored `artifacts/brand-exploration/research-round-v7/` retains the raster
concepts, earlier wordmark/layout studies, vector comparisons and measurements.
`vector/build_leafcut.py` regenerates this vector draft with the ignored local
`.tools/brand-vector-env` environment; `vector/verify_leafcut.py` verifies closed
contours, font independence, the emblem and raster silhouette. The SVG files
above are the portable sources and do not require those tools for editing/use.
