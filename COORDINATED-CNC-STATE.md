# Coordinated CNC State

- Round ID: `20260806-bug-polish-01`
- Phase: `RC2 assembled; combined adversarial worker testing starting`
- Common base branch: `agent/cnc38-early-viki-infantry-rush`
- Common base SHA: `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`
- Coordinator model: `gpt-5.6-sol` / `high` (trial mismatch explicitly accepted by user)
- Game slots: `2` for ordinary/full MAX simulations. The three-slot trial showed
  that short, tightly bounded custom fixtures can run three safely, but three
  benchmarked ordinary/full games reached about 5.5 GB RSS within 28 seconds and
  were stopped before OOM; do not use three for normal full-game acceptance.
- Large-build slots: `1`
- Lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/locks`
- Release candidate: `RC2` at `fd15540ffc98c70f085688fe0b38a4a6341fc6ed`
  (code candidate `b456fd89fac88d71dfadd65c47cfb7b409d44122`)
- Release PR: [draft #84](https://github.com/Realpra1/LibertyDawn/pull/84)

## Workers

| Worker | Task | Branch | Worktree | State | Process/result | PR | Review | Integrated status |
|---|---|---|---|---|---|---|---|---|
| 1 | CNC-39 Engineer correction | `agent/round-20260806-cnc39-rc2-repair` | `.worktrees/coordinated-cnc/20260806-bug-polish-01/integration/combined-worker-1-cnc39` | `Complete - testing` (`WORKER-1-CNC-39/STATE.md`) | `roles/combined-worker-1-rc2/process.json` running | [#83](https://github.com/Realpra1/LibertyDawn/pull/83) at `0e9efa901a` | `ready with one fix`; exact surplus-Engineer Stop response completed with ActorID regressions and green CI (`REVIEW-1.md`) | `RC2 combined testing active` |
| 2 | CNC-39A Engineer/commando target coordination | `agent/round-20260806-cnc39a-rc2-repair` | `.worktrees/coordinated-cnc/20260806-bug-polish-01/integration/combined-worker-2-cnc39a` | `First iteration - testing; combined pass/no repair` (`WORKER-2-CNC-39A/STATE.md`) | `roles/combined-worker-2-rc2/process.json` complete 0 | [#80](https://github.com/Realpra1/LibertyDawn/pull/80) at `937ef02048` | `blocked`; one required save/load response completed with exact-head CI and reload evidence (`REVIEW-2.md`) | `RC2 passed: 4 games, focused 15/15, full 454/454; prior evidence gaps remain; documentation head 4c140dc37a` |
| 3 | CNC-43 MCV crush flavor | `agent/round-20260806-cnc43-rc2-repair` | `.worktrees/coordinated-cnc/20260806-bug-polish-01/integration/combined-worker-3-cnc43` | `combined pass/no repair` (`WORKER-3-CNC-43/STATE.md`) | `roles/combined-worker-3-rc2/process.json` complete 0 | [#78](https://github.com/Realpra1/LibertyDawn/pull/78) at `b229612791` | `ready with one fix`; one permitted evidence response complete (`REVIEW-3.md`) | `RC2 passed: 6 games, wall-intact recovery and natural conclusion; documentation head 10931c9f20` |
| 4 | CNC-43A Flame Tank balance | `agent/round-20260806-cnc43a-rc2-repair` | `.worktrees/coordinated-cnc/20260806-bug-polish-01/integration/combined-worker-4-cnc43a` | `combined pass/no repair` (`WORKER-4-CNC-43A/STATE.md`) | `roles/combined-worker-4-rc2/process.json` complete 0 | [#79](https://github.com/Realpra1/LibertyDawn/pull/79) at `ade3f9d325` | `ready with one fix`; one permitted evidence response complete (`REVIEW-4.md`) | `RC2 passed: adversarial fixture matrix plus matched natural control; documentation head 8947aa71f7` |
| 5 | CNC-51 Transport-helicopter unload recovery and threat-safe landing | `agent/round-20260806-cnc51-rc2-repair` | `.worktrees/coordinated-cnc/20260806-bug-polish-01/integration/combined-worker-5-cnc51` | `combined pass/no repair` (`WORKER-5-CNC-51/STATE.md`) | `roles/combined-worker-5-rc2/process.json` complete 0 | [#81](https://github.com/Realpra1/LibertyDawn/pull/81) at `72dad573af` (product `cb6a05d5a3`) | `ready with one fix`; moving-aircraft closing envelope response complete, exact-head CI passed | `RC2 passed: 6 games, focused 97/97, full 454/454; documentation head b007a26c2b` |

## Release rounds

| RC | Head | Included heads | Repair heads | Build/checks | Integrated tests | Result |
|---|---|---|---|---|---|---|
| RC1 preview | `0057dd25868e` | CNC-39A `937ef02048`; CNC-43 `b229612791`; CNC-43A `ade3f9d325` | none | Debug/Release build, 445/445 unit, static/interface, Lua, MiniYAML/maps, diff check passed | pending full five-task candidate | draft [#82](https://github.com/Realpra1/LibertyDawn/pull/82); CNC-39/CNC-51 excluded |
| RC2 | `fd15540ffc98` (code `b456fd89fac8`) | all five reviewed heads | CNC-39/CNC-39A semantic reconciliation in merge commit | focused 15/15, full 454/454, `make check`, scripts, CNC content/maps, diff and scope audit passed with zero warnings/errors | starting; trial three simultaneous isolated MAX games | draft [#84](https://github.com/Realpra1/LibertyDawn/pull/84); successor because #82 was already merged |

## Resume note

Record only routing, process identity, branch heads, phase, blockers, and concise
results here. Keep task specifications and detailed evidence in worker state and
reports.

After all five task heads complete review, integration, adversarial combined
testing, repair, and release review, use the tested cumulative release head as the
common base for the next coordinated five-task round. Do not stop after this
round unless the user pauses the pipeline or a real blocker prevents progress.
