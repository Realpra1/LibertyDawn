# CNC-66: Windows Validator Wrapper Repair

- Status: complete
- Cycles used: 4 of 30
- Branch: `agent/cnc66-validator`
- Pull request: https://github.com/Realpra1/LibertyDawn/pull/51

## Result

`utility.cmd cnc --check-yaml` now forwards scripted arguments to `OpenRA.Utility.exe` instead of silently ignoring them and looping forever in the interactive prompt. Zero-argument and one-mod-argument interactive use remains intact, command quoting is preserved through `%*`, and the utility process exit code is returned to automation.

The repair is a compatible backport of official OpenRA commit [`07ec2d03fbfb`](https://github.com/OpenRA/OpenRA/commit/07ec2d03fbfb), whose stated purpose was to make `utility.cmd` programmatically usable. LibertyDawn improves the upstream implementation by propagating the actual child exit code instead of unconditionally returning success.

Windows CI now invokes the documented wrapper directly, so argument-forwarding regressions cannot remain hidden behind `make.ps1` calling the executable by another path.

## Diagnosis

The YAML validator was not slow or frozen. Before the fix, the batch file never inspected `%*`; it invoked the utility with no arguments, entered `set /P`, and repeatedly printed the empty mod prompt when run without interactive input. This explained the previous silent five- and twenty-minute timeouts.

The direct executable already provided bounded stage diagnostics by printing the mod, default sequence set, and each map title. On the same checkout it validated every CNC ruleset/map—including Empire Lars—in 18.132 seconds. No validator pass or map dominated enough to justify engine/lint changes.

## Cycles

1. Reproduced the batch prompt loop under a five-second bound, compared current official OpenRA history, implemented forwarding, and proved full wrapped validation completed in 17.765 seconds with output byte-identical to the 18.132-second direct baseline.
2. Adversarial failure/quoting test: copied a map to a path containing a space and inserted an invalid actor. Direct and wrapped validation both emitted the same lint error and exited 1.
3. Adversarial compatibility test: zero-argument mod selection and one-argument CNC command-selection modes both remained interactive and exited cleanly when fed `--exit`; one-argument mode still displayed `--check-yaml` help.
4. Adversarial integrated regression: a seeded, fully enabled headless MAX Empire Earth match with two Skynets and three Brutalis reached natural game over at world tick 27,880. All bots loaded and acted, the replay and benchmark data were flushed, and no new fatal, Lua, or unhandled exception log appeared.

## Validation

- Release build: passed with zero warnings/errors.
- Strict Debug build and both trait-interface checks: passed with zero warnings/errors.
- Unit tests: 286/286 passed.
- Direct and wrapped full CNC YAML output SHA-256: `B500E5210A018F7BF75CC368EA3B73F7B353E4B9E480C3E083019EE7FB014703`.
- GitHub CI before the documentation-only final push: Linux and Windows passed; Windows included the new direct wrapper gate.
- Local Lua syntax command could not run because `luac.exe` is not installed; authoritative GitHub Lua checks passed.
- Ignored raw evidence: `AUTONOMOUS-CNC-LOGS/CNC-66/`, including the complete match replay and benchmark files.

## Scope

No game, AI, balance, rules, maps, validator passes, or supported-mod selection changed. The unrelated malformed user map `TibTest.oramap` still logs a nonfatal map-cache warning during startup; the five configured bots and selected Empire Earth map loaded normally.
