# Coordinated CNC State

- Round ID: `20260806-bug-polish-01`
- Phase: `RC4 complete locally; final task statuses recorded; final-head CI running`
- Common base branch: `agent/cnc38-early-viki-infantry-rush`
- Common base SHA: `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`
- Coordinator model: `gpt-5.6-sol` / `high` (trial mismatch explicitly accepted by user)
- Game slots: `2` for ordinary/full MAX simulations. The three-slot trial showed
  that short, tightly bounded custom fixtures can run three safely, but three
  benchmarked ordinary/full games reached about 5.5 GB RSS within 28 seconds and
  were stopped before OOM; do not use three for normal full-game acceptance.
- Large-build slots: `1`
- Lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260806-bug-polish-01/locks`
- Release candidate: `RC4` task-status head
  `419bee2531d4802bf922c3597b42c6eeb75ab250` (code candidate
  `a7d29d08d83deebb7867076a141675326553dc3f`)
- Release PR: [product #84](https://github.com/Realpra1/LibertyDawn/pull/84),
  open to `bleed` and intentionally unmerged

## Workers

| Worker | Task | Branch | Worktree | State | Process/result | PR | Review | Integrated status |
|---|---|---|---|---|---|---|---|---|
| 1 | CNC-39 Engineer correction | `agent/round-20260806-cnc39-rc2-repair` | `.worktrees/coordinated-cnc/20260806-bug-polish-01/integration/combined-worker-1-cnc39` | `complete` (`WORKER-1-CNC-39/STATE.md`) | `roles/combined-worker-1-review-response/process.json` complete 0 | [#83](https://github.com/Realpra1/LibertyDawn/pull/83) at `0e9efa901a` | required response and strict literal evidence passed | `included in RC4; Task Maker finalized complete` |
| 2 | CNC-39A Engineer/commando target coordination | `agent/round-20260806-cnc39a-rc2-repair` | `.worktrees/coordinated-cnc/20260806-bug-polish-01/integration/combined-worker-2-cnc39a` | `first iteration` (`WORKER-2-CNC-39A/STATE.md`) | `roles/combined-worker-2-rc2/process.json` complete 0 | [#80](https://github.com/Realpra1/LibertyDawn/pull/80) at `937ef02048` | integrated regressions passed; prior evidence gaps preserved | `included in RC4; Task Maker finalized first iteration` |
| 3 | CNC-43 MCV crush flavor | `agent/round-20260806-cnc43-rc2-repair` | `.worktrees/coordinated-cnc/20260806-bug-polish-01/integration/combined-worker-3-cnc43` | `complete` (`WORKER-3-CNC-43/STATE.md`) | `roles/combined-worker-3-rc2/process.json` complete 0 | [#78](https://github.com/Realpra1/LibertyDawn/pull/78) at `b229612791` | wall-intact recovery, natural conclusion, and broad gates passed | `included in RC4; Task Maker finalized complete` |
| 4 | CNC-43A Flame Tank balance | `agent/round-20260806-cnc43a-rc2-repair` | `.worktrees/coordinated-cnc/20260806-bug-polish-01/integration/combined-worker-4-cnc43a` | `complete` (`WORKER-4-CNC-43A/STATE.md`) | `roles/combined-worker-4-rc2/process.json` complete 0 | [#79](https://github.com/Realpra1/LibertyDawn/pull/79) at `ade3f9d325` | authorized balance matrix, matched control, and scope audit passed | `included in RC4; Task Maker finalized complete` |
| 5 | CNC-51 Transport-helicopter unload recovery and threat-safe landing | `agent/round-20260806-cnc51-rc3-final-repair` | `.worktrees/coordinated-cnc/20260806-bug-polish-01/integration/final-worker-5-cnc51` | `complete` (`WORKER-5-CNC-51/STATE.md`) | `roles/worker-5-rc3-final-response/process.json` complete 0 | [#81](https://github.com/Realpra1/LibertyDawn/pull/81) at `72dad573af` (product `cb6a05d5a3`) | terminal-recovery response passed 98/98 focused, 455/455 full, two full-engine regressions, and policy approval | `repair 4be958ee07 included in RC4; Task Maker finalized complete` |

## Release rounds

| RC | Head | Included heads | Repair heads | Build/checks | Integrated tests | Result |
|---|---|---|---|---|---|---|
| RC1 preview | `0057dd25868e` | CNC-39A `937ef02048`; CNC-43 `b229612791`; CNC-43A `ade3f9d325` | none | Debug/Release build, 445/445 unit, static/interface, Lua, MiniYAML/maps, diff check passed | pending full five-task candidate | draft [#82](https://github.com/Realpra1/LibertyDawn/pull/82); CNC-39/CNC-51 excluded |
| RC2 | `fd15540ffc98` (code `b456fd89fac8`) | all five reviewed heads | CNC-39/CNC-39A semantic reconciliation in merge commit | focused 15/15, full 454/454, `make check`, scripts, CNC content/maps, diff and scope audit passed with zero warnings/errors | starting; trial three simultaneous isolated MAX games | draft [#84](https://github.com/Realpra1/LibertyDawn/pull/84); successor because #82 was already merged |
| RC3 | `2343cf158bd3` (code `de855c42d39f`) | all five combined-testing receipts | CNC39 repair `bc3ab411f8`; strict review response passed | focused 15/15, full 454/454, `make check`, scripts, CNC content/maps, diff/scope audit passed with zero warnings/errors | all five handoffs complete; four no-repair, CNC39 repaired and retested | draft [#84](https://github.com/Realpra1/LibertyDawn/pull/84); final combined review and CI active |
| RC4 | `419bee2531d4` (code `a7d29d08d83d`) | existing RC3 candidate plus CNC51 final-review receipt `2e6fa14c56` and Task Maker receipt | CNC51 terminal-recovery repair `4be958ee07` | focused 98/98, full 455/455, `make check`, scripts, CNC content/maps, diff/scope audit passed | runs 53/54 and approved policy receipt verified; final task statuses recorded | product [#84](https://github.com/Realpra1/LibertyDawn/pull/84); final-head CI running, intentionally unmerged |

## Resume note

Record only routing, process identity, branch heads, phase, blockers, and concise
results here. Keep task specifications and detailed evidence in worker state and
reports.

After all five task heads complete review, integration, adversarial combined
testing, repair, and release review, use the tested cumulative release head as the
common base for the next coordinated five-task round. Do not stop after this
round unless the user pauses the pipeline or a real blocker prevents progress.
