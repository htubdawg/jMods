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

The owner is going through the mods one at a time: read the mod's released source, fix bugs, update versions, and make sure this
README's section for the mod matches what the release does. The owner syncs `mods/` once at the end, so `mods/` lags behind and is
not what the table tracks: the table tracks each source repo's newest release tag. **Check this table against the tags before
starting** (`git ls-remote --tags https://github.com/htubdawg/<repo>`; the tag's `mod.json` has the versions) and fix it if it is stale.

| Mod | Newest release | Status |
|---|---|---|
| MiniMap | v1.2.6, game 0.419, tools 1.0.6 | **Done** (htubdawg/HalfGripMiniMap#21, #22). |
| OpenWorld | v0.26.1, game 0.419, tools 1.0.6 | **Done** (htubdawg/HalfGripOpenWorld#40). |
| ProcTrack | v1.0.29, game 0.419, tools 1.0.6 | **Done**. |
| Helicopter | v0.2.7, game 0.419, tools 1.0.6 | **Done**. |
| Stereo | v0.2.4, game 0.419, tools 1.0.6 | **Done** (htubdawg/HalfGripStereo#4). |
| SlapPack | v0.8.12, game 0.418, tools 1.0.6 | **PR open**: htubdawg/HalfGripSlapPack#7 (0.8.13, game 0.419; tutorial fix, config comments, README). It contains #6, so merging #7 completes both. Then release v0.8.13. |

Current versions when this was written: game **0.419** (HalfGripRefs), HalfGripTools **1.0.6**. Check both again before using them.

**README:** the MiniMap section here has not been changed yet. A rewrite (shop GPS, controls table, Open World view) was made on
branch `claude/blissful-mayer-0t7ba2`, but the owner did not want that branch: it should be deleted, not merged. Rewrite the
README section with the audit's PR instead.

## Session limits seen so far

- Deleting a remote branch (`git push --delete`) is refused by the session's git proxy (HTTP 403); a force push is refused by the
  permission check. Use a new branch name instead of rewriting an old one, and ask the owner to delete branches on GitHub.
- `add_repo` with push access was refused by the auto-mode permission check until the owner allowed it; a public repo can still be
  cloned read-only, but not pushed to.
- Building HalfGripTools from source worked in the Stereo audit session (full `tools/check.sh` ran); it was refused by the permission check before that and again in the SlapPack audit session, so `tools/check.sh`-style full compiles could not always run.
- Decompiling the game works: `dotnet tool install ilspycmd --tool-path <scratch>/ilspy --version 8.2.0.7535`, then run it with
  `DOTNET_ROLL_FORWARD=Major` (only .NET 8 is installed) and `-p -o <dir> Assembly-CSharp.dll` to check how game code behaves.
  A partial compile works: every file of the mod except those that `using HalfGripTools`, plus a small stand-in for the members
  they provide, against `HalfGripRefs/game` (see HalfGripMiniMap#21's checks). Better: a stand-in `HalfGripTools` namespace with the
  members the mod calls (signatures copied from HalfGripTools' source), so every file compiles (done for HalfGripOpenWorld#40).
  A small `System.Reflection.Metadata` scan of `Assembly-CSharp.dll` checks the members a mod names by string (Harmony targets,
  `Traverse`, `AccessTools`), which a compile does not.
