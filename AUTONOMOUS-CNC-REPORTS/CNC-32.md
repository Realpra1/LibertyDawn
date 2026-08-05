# CNC-32: Remove Unsupported Mods from Normal Workflows

- Status: implementation and local validation complete; publication pending
- Cycles used: 15 of 30
- Branch: `agent/cnc32-remove-unsupported`
- Base: `origin/agent/cnc31-red-tiberium-bomb-trucks` at `5b341c4433`
- Draft pull request: pending

## Result

LibertyDawn's normal solution, build, validation, launcher, install, packaging, and itch entrypoints now target Tiberian Dawn (`cnc`) only. The shared engine, Common mod assembly/data, CNC assembly/data, content manager, server, utility, launchers, platform project, and unit tests remain in the solution where required. The standalone Dune 2000 assembly is no longer built by the normal solution or loaded by the hidden interface-check manifest.

Normal YAML and Lua validation is scoped to CNC plus its shared Common/root scripts. Windows, Linux, and macOS package entrypoints now emit one CNC product. Windows installation registers and creates shortcuts only for CNC; Linux metadata now describes only LibertyDawn Tiberian Dawn; itch exposes only `TiberianDawn.exe`. Development launch profiles and launch wrappers select CNC, and the Windows/Unix wrappers reject explicit RA, D2K, and TS requests.

Unsupported source/content trees remain untouched for history and comparison. Generic packaging helpers retain explicit multi-mod parameters for upstream/SDK compatibility, but every LibertyDawn normal caller passes only CNC and disables the D2K assembly.

## Upgrade safety

A fresh CNC package was clean, but the first upgrade review found that old multi-mod files could survive when installing into an existing destination. CNC-only installation now removes exact legacy RA/D2K/TS data directories, the old D2K assembly, Linux launchers/metadata/icons, and Windows launchers/icons/registrations/shortcuts. The paths are narrowly scoped to known unsupported artifacts; custom files and unrelated installed data are not removed.

Packaging publish is serialized with `-m:1` because multiple solution projects otherwise raced while copying the same self-contained runtime files and emitted transient copy warnings. The serialized build completed without warnings.

## Evidence cycles

1. Inventoried solution, build, validation, launch, CI, install, and all platform package entrypoints.
2. Removed D2K from the normal solution/aggregate and narrowed build/test/package defaults; strict Debug build and both interface checks passed with only CNC/Common mod assemblies emitted.
3. CNC-only YAML validation passed every shipped CNC map; local Lua compilation was unavailable because `luac.exe` is not installed, so that gate is deferred to GitHub CI rather than counted locally.
4. First real packaging-helper run emitted only CNC/Common/modcontent, but exposed parallel self-contained publish copy warnings.
5. Unsupported-launch adversarial test exposed a Windows batch string-match bug and accidentally started one game; the exact process was stopped and the guard was corrected.
6. Windows and Unix launch/utility guards rejected RA/D2K/TS with exit code 2; 334 unit tests passed.
7. Serialized package creation completed warning-free and a payload audit found no unsupported assembly, data, or launcher.
8. A five-bot source-tree Empire Earth match with ordinary two-Skynet/three-Brutalis stacks reached natural game over around tick 25,757 without a current fatal/Lua error.
9. The first packaged-runtime harness dropped `Launch.Map`; headless startup rejected the invalid harness as designed. This is not product evidence.
10. Corrected five-bot packaged runtime remained active through a bounded eight-minute late-game stress run; it was stopped at the bound and is not a clean completion pass.
11. First clean adversarial pass: source-tree two-bot Desert Rats reached natural game over.
12. Second clean adversarial pass: the actual reduced CNC package ran a distinct seeded two-bot Desert Rats match to natural game over.
13. Third clean adversarial pass: source-tree Island Duel exercised a separate map and movement domains to natural game over.
14. Upgrade fixture began with stale RA/D2K/TS data, D2K assembly, Linux launchers, shortcuts, MIME, icons, and appdata; CNC install removed all stale artifacts and preserved every required CNC artifact.
15. Final regression ran the upgraded CNC-only payload with ordinary bots to natural game over, followed by strict build, interface, unit, YAML, solution, shell, JSON/XML, NSIS-structure, artifact, and diff audits.

## Final local gates

- Strict Debug solution build: zero warnings and zero errors.
- Normal Release build: zero warnings and zero errors; output contains `OpenRA.Mods.Cnc.dll` and `OpenRA.Mods.Common.dll`, not `OpenRA.Mods.D2k.dll`.
- Unit tests: 334/334 passed.
- Explicit-interface and conditional-interface validators passed against CNC.
- Exhaustive `utility.cmd cnc --check-yaml` passed every CNC ruleset, sequence set, and shipped map.
- PowerShell launch JSON/MSBuild XML parsing, Bash syntax for launch/utility/package scripts, NSIS block balance, CNC-only normal-entrypoint scan, and `git diff --check` passed.
- Actual fresh and upgrade package-helper payloads contained CNC/Common/modcontent only and launched complete ordinary CNC matches.
- Three distinct adversarial engine passes plus the final upgraded-payload regression reached natural game over.

## Evidence retention and boundaries

The recurring invalid user `TibTest.oramap` cache warning predates this task and is recorded in `DEFERRED_WORK.md`; timestamped current runs had no new fatal, Lua, or unhandled error. At the user's request during final validation, approximately 6.5 GiB of generated `AUTONOMOUS-CNC-LOGS` and OpenRA runtime logs were moved to the Windows Recycle Bin to address critical disk pressure. They remain recoverable until the user empties the bin, but are no longer present at their original ignored paths. Durable results are preserved in this report.

Full Windows installer compilation and native Linux/macOS package assembly require platform packaging toolchains unavailable on this Windows host. The common real packaging path, generated payload, Linux shortcut/appdata functions, platform script syntax/static calls, and a real packaged game were validated locally; GitHub Linux/Windows checks remain the publication gate.
