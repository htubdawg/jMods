# jMods

The owner's Half Grip mod collection. `mods/` is **not source**: `.github/workflows/sync-mods.yml` deletes it and copies each mod's
latest published release back in every six hours. Anything edited under `mods/` is lost at the next sync. Only `README.md`,
`CLAUDE.md` and the workflow are edited here.

## Where each mod lives

| Folder in `mods/` | Source repo (all `htubdawg/`) |
|---|---|
| `OpenWorld` | `HalfGripOpenWorld` |
| `ProcTrack` | `ProcTrack` |
| `MiniMap` | `HalfGripMiniMap` |
| `Helicopter` | `HalfGripHelicopter` |
| `Stereo` | `HalfGripStereo` |
| `SlapPack` | `HalfGripSlapPack` |

Supporting repos: `htubdawg/HalfGripRefs` (private: game DLLs and scene dumps for compile checks; its README gives the current game
version) and `4l3x4nd3r0/HalfGripTools` (the mod loader, public; current version is `Plugin.Version` in `HalfGripTools/Plugin.cs`).

## Rules for fixing a mod (do these without being asked)

1. **Fix it in the mod's own repo, never in `mods/`.** Attach the repo to the session with `add_repo` (access `push`), clone it, and
   read its own CLAUDE.md first: each one has its own rules, checks and a Status section.
2. **Every change that will be released:** bump the mod's `version` in `mod.json`; set `gameVersion` to the current game and
   `tools` to the current HalfGripTools release (see above); keep the mod README's header line and install steps in step.
3. Run the repo's own checks (its CLAUDE.md lists them) and say plainly which ones ran and which could not.
4. Update the repo's CLAUDE.md Status, push to a branch and **open the PR yourself**. Do not stop at "want me to open a PR?".
   Before pushing more to a branch that already has a PR, check the PR is still open: the owner merges quickly, and commits
   pushed to a merged PR's branch never reach `main` (that happened with HalfGripMiniMap#21). Start a new branch from `main` instead.
5. Reaching jMods: the owner merges the PR and publishes a release; the sync then brings it here. Do not wait for that or edit
   `mods/` to make it show up sooner.

## The audit (keep current)

The owner is going through the mods one at a time: read the released source in `mods/<Mod>/`, fix bugs, update versions, and
make sure this README's section for the mod matches what the release does.

| Mod | Released (in `mods/`) | Status |
|---|---|---|
| MiniMap | 1.2.5, game 0.418, tools 1.0.2 | **Done, waiting on the owner**: fixes merged in [htubdawg/HalfGripMiniMap#21](https://github.com/htubdawg/HalfGripMiniMap/pull/21); game 0.419 and tools 1.0.6 in [htubdawg/HalfGripMiniMap#22](https://github.com/htubdawg/HalfGripMiniMap/pull/22). Next: owner merges #22 and publishes v1.2.6. |
| OpenWorld | 0.26.0, game 0.418, tools 1.0.6 | **Done, waiting on the owner**: game 0.419, version 0.26.1, a weather fix in [htubdawg/HalfGripOpenWorld#40](https://github.com/htubdawg/HalfGripOpenWorld/pull/40); README section updated (events, storms). Next: owner merges #40 and publishes v0.26.1. |
| ProcTrack | 1.0.28, game 0.418, no `tools` | Not started. Game version is behind; `tools` missing. |
| Helicopter | 0.2.6, game 0.418, tools 1.0.2 | Not started. Both versions are behind. |
| Stereo | 0.2.3, game 0.418, tools 1.0.2 | Not started. Both versions are behind. |
| SlapPack | 0.8.12, game 0.418, tools 1.0.6 | Not started. Game version is behind. |

Current versions when this was written: game **0.419** (HalfGripRefs), HalfGripTools **1.0.6**. Check both again before using them.

**README:** the MiniMap section here has not been changed yet. A rewrite (shop GPS, controls table, Open World view) was made on
branch `claude/blissful-mayer-0t7ba2`, but the owner did not want that branch: it should be deleted, not merged. Rewrite the
README section with the audit's PR instead.

## Session limits seen so far

- Deleting a remote branch (`git push --delete`) is refused by the session's git proxy (HTTP 403); a force push is refused by the
  permission check. Use a new branch name instead of rewriting an old one, and ask the owner to delete branches on GitHub.
- `add_repo` with push access was refused by the auto-mode permission check until the owner allowed it; a public repo can still be
  cloned read-only, but not pushed to.
- Building HalfGripTools from source was refused by the permission check, so `tools/check.sh`-style full compiles could not run.
  A partial compile works: every file of the mod except those that `using HalfGripTools`, plus a small stand-in for the members
  they provide, against `HalfGripRefs/game` (see HalfGripMiniMap#21's checks). Better: a stand-in `HalfGripTools` namespace with the
  members the mod calls (signatures copied from HalfGripTools' source), so every file compiles (done for HalfGripOpenWorld#40).
  A small `System.Reflection.Metadata` scan of `Assembly-CSharp.dll` checks the members a mod names by string (Harmony targets,
  `Traverse`, `AccessTools`), which a compile does not.
