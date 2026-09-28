# Repository migration — September 28, 2026

The public repository remains `ConcosHut/LuckerParty`. Migration used GitHub
branch renames, preserving both independent histories without force-pushing.
ParaLizard had administrator access. There were no open pull requests or
repository rulesets when the migration began.

- Original s&box master: `e0f6f6b8c654bb9f6fbe607569f47038ffc9ac27`.
- Preserved as `legacy-2` and annotated tag `sbox-master-2026-09-28`.
- Godot history was pushed to temporary `godot-next`, verified, then renamed to
  `master`; initial implementation commit: `af25d54d7a52b88be594484c7b75fbe065494840`.
- New repository default: `master`. Local `main` was renamed and tracks it.
- `release` will start from the accepted Godot revision and publish Stable.
- Other branches preserved: `legacy` (`cc877492d519cb3a972778f06357bdf5b974c864`),
  `fixed-camera` (`b9dbe07e1f09cb35516c7105b5f56108d69c94d6`),
  `terry-races` (`5647e33dd9370d866df7d8e18276ffe73b37c0dc`).

Collaborators should make a fresh clone for Godot. Existing s&box work can
continue from `origin/legacy-2`; do not merge the unrelated histories by pulling
new master into an old master checkout. Tools, artifacts, machine credentials,
and SSH keys remain ignored. See [releases.md](releases.md) for promotion.
