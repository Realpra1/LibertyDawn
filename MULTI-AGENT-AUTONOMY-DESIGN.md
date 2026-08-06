# Multi-Agent Autonomous CNC Coordinator: Design Review

This document evaluates the proposed coordinator architecture and records the
questions that should be answered before implementing the new skill. The
existing `autonomous-cnc-coding` skill must remain unchanged.

## Initial verdict

The central idea is sound: isolate context by responsibility, give workers
durable task-local specifications, separate Git worktrees, and use a lightweight
coordinator. This should reduce context pollution and make parallel development
more reliable.

Several parts need revision before implementation:

1. Five native workers cannot run under the current four-agent limit. The parent
   occupies one slot, leaving three native subagents. Additional independent
   `codex exec` sessions are possible, but need a file-based job queue, process
   supervision, unique worktrees, and structured result files.
2. Renaming a skill as a work file does not override higher-priority instructions.
   Independent sessions may read their own narrow instructions, but applicable
   system, developer, and repository instructions remain binding.
3. Every agent must read applicable `AGENTS.md`. The repository currently directs
   autonomous CNC development to the original skill. `AGENTS.md` will probably
   need a narrow coordinated-mode rule while leaving the original skill intact.
4. Detailed specs should not go in the global state file. Use separate immutable
   numbered spec files. Keep global state to task IDs, ownership, base SHA,
   branch, phase, and result.
5. Select up to five compatible tasks, not exactly five. Tasks may overlap,
   depend on each other, or modify the same central AI code.
6. Specs made in advance can go stale. Each needs a base commit and a freshness
   check before work starts.
7. The claimed 32 cycles are per-task accounting, not necessarily 32 game runs.
   Five workers can execute up to 100 isolated cycles, and integrated testing can
   add many more scenario executions.
8. Five workers plus MAX games on four vCPUs can reduce simulation speed and
   evidence quality. Use global locks: initially one large build and two MAX
   games at a time, increasing to three games only if benchmarking supports it.
9. Five simultaneous Sol-high reviewers are probably wasteful. One independent
   review per PR is useful. Routine Git integration should be deterministic;
   reserve a strong model for meaningful conflicts.
10. Agent review is not equivalent to CI. Compilation, unit/static checks,
    runtime evidence, and performance measurements remain separate gates.
11. The integration repair model is unresolved. Fixing only the integration
    branch makes individual task PRs stale; fixing task branches requires a safe
    way to ingest new commits repeatedly.
12. Do not discard incomplete task ideas. Store them as drafts, ask for missing
    information, and promote them to `ready` only after the standard is met.
13. A worker should submit evidence and a proposed status, not certify itself as
    complete. Completion should require acceptance evidence, review, and green
    required checks.
14. A skill cannot change the model of the session already running it. Launch the
    coordinator with an exact model ID and reasoning level; avoid an ambiguous
    phrase such as "ChatGPT 5.5 equivalent" in automation.

## Recommended starting architecture

- One Terra-medium coordinator.
- One single-writer Task Maker.
- One persistent Terra-medium Task Reader per batch.
- A fresh Sol-xhigh speccing session for every selected task.
- Up to five external worker sessions, while respecting the three-subagent native
  limit and the separate local CPU resource limits.
- Explicit build and game locks.
- One independent worktree and branch per worker.
- One immutable numbered spec plus a separate append-only journal/result receipt
  per worker.
- One fresh review session per PR, queued as capacity permits.
- Deterministic integration where possible; a Sol-high conflict resolver only
  when reasoning is necessary.
- Task-specific integration tests plus shared regression scenarios.
- Only the Task Maker writes the task sheet.
- Only the coordinator writes global state.

## Questions requiring answers

The decisions marked **critical** should be settled before scaffolding the new
skill. The sections can be answered in separate passes.

### Scope and authority

1. **Critical:** What should the new skill be named?
2. Should it live beside the original at `.agents/skills/<new-name>/` and be
   committed to this repository?
3. **Critical:** May `AGENTS.md` be updated to recognize coordinated mode while
   leaving the original autonomous skill untouched?
4. Is this skill CNC-specific, or should its orchestration layer eventually
   support other Liberty Dawn projects?
5. Should it continue through batches until explicitly paused, like the current
   autonomous skill?
6. **Critical:** Is five a fixed batch size or a maximum of five compatible tasks?
7. If only two compatible tasks exist, should the round begin immediately or wait?
8. **Critical:** May the coordinator launch independent `codex exec` processes
   outside the native subagent system?
9. What is the maximum number of simultaneous paid model sessions?
10. **Critical:** Is there a credit, dollar, or wall-clock budget per batch?
11. Should budget pressure stop the round, reduce concurrency, or downgrade models?
12. **Critical:** Must the exact models be Terra 5.6 medium, Sol 5.6 high, and Sol
    5.6 xhigh? What is the fallback if one is unavailable?

### Task intake and task-sheet ownership

13. **Critical:** May incomplete ideas be stored as drafts instead of rejected?
14. Should drafts live in the main task sheet or a separate task inbox?
15. Who may promote a draft to ready: only the user, or the Task Maker after all
    required information exists?
16. Should missing questions be asked immediately or collected into a batch?
17. Can the user explicitly override the standard and force an incomplete task
    into the queue?
18. Must every task have a numerical success measure, or can some use qualitative
    player-observable outcomes?
19. Should predicted behavior change include a numerical estimate when possible?
20. Must every task state a performance expectation, such as no measurable
    simulation slowdown?
21. Should a bug against a completed task receive a new regression task ID linked
    to the original?
22. Should a bug against an active task amend its spec or enter the queue as a new
    task?
23. Who decides whether tasks are closely related: Task Reader, Speccer, or
    Coordinator?
24. May the user reprioritize while specs are being prepared?
25. Is the Task Maker the only task-sheet writer even for PR numbers and statuses?

### Task Reader

26. **Critical:** Should one Task Reader load the sheet once and provide task
    packets sequentially, or should each selection use a fresh reader?
27. May it read state, reports, PR metadata, and history to detect dependencies?
28. Must it inspect likely file overlap before approving a parallel batch?
29. Must it skip tasks whose prerequisites are absent from the base checkpoint?
30. Should it prefer subsystem diversity over strict numerical task order?
31. May a task packet include linked bugs, relevant reports, and dependency
    summaries, or only the literal task text?
32. Should the coordinator know task titles and IDs for status reporting, or only
    anonymous worker numbers?

### Specification

33. Should every spec use a completely fresh Sol-xhigh session?
34. May the Speccer inspect code, history, tests, maps, configs, logs, and reports?
35. Must the Speccer be read-only with respect to product code?
36. Should the spec name likely files and symbols, or focus on behavior and let
    the worker choose implementation?
37. **Critical:** Should every spec include all of the following?

    - Desired player-visible behavior
    - Forbidden and regression behavior
    - Likely wrong implementations and challenges
    - Competing AI systems
    - Literal acceptance scenario
    - Adversarial scenarios
    - Required instrumentation
    - Performance risks
    - Base commit
    - Expected file ownership and overlap
    - Stop and first-iteration criteria

38. Should a spec become immutable when work begins?
39. If evidence invalidates it, should the original Speccer revise it or should a
    fresh Speccer produce version 2?
40. Who approves a spec before a worker receives it?
41. Should oversized tasks be split during specification?
42. If split, do subtasks consume multiple batch positions?
43. **Critical:** Should specs be tracked repository files, ignored local files,
    or files on a dedicated coordination branch?
44. Should completed specs remain in history or be summarized into task reports?

### Workers and worktrees

45. Should workers create PRs, or stop after committing for a publisher agent?
46. Must every task branch use the exact same base SHA?
47. May the planner exclude a task that depends on another task in the same batch?
48. Are declared files strict ownership boundaries or overlap warnings?
49. What happens when two tasks unexpectedly need the same file?
50. Should workers be prohibited from editing task/global state and submit only
    structured receipts?
51. May workers add deferred-work notes?
52. **Critical:** What counts as one cycle: one game, one concurrent scenario
    group, or one code revision followed by its test set?
53. Does a compilation failure consume a cycle?
54. Does an invalid test setup consume a cycle?
55. Is 20 a hard cap if cycle 20 reveals a small and obvious fix?
56. Is there also a wall-clock limit per task?
57. When blocked by another task, should a worker sleep, return a blocker receipt,
    or take a fallback task?
58. Should workers propose `complete` or `first iteration`, with the coordinator
    deciding final status?

### Resource scheduling

59. **Critical:** Start with two concurrent MAX games and benchmark three?
60. May one worker reserve both game slots for a matched differential test?
61. Should large builds use a global exclusive lock?
62. Should a game be killed automatically when simulation ticks stop advancing?
63. Should quick focused tests receive priority over long full matches?
64. Should idle model sessions stop and later restart from durable state?
65. How long may an idle external session remain alive?

### Review gates

66. **Critical:** Is there one reviewer per PR or five reviews of every PR?
67. Does the reviewer receive only spec and diff, or also evidence and surrounding
    code?
68. Must reviewers remain read-only and return findings to the worker?
69. Which severities block integration: critical/high only, or medium too?
70. May an author reject a finding with written evidence?
71. Does an unresolved disagreement go to a second reviewer?
72. Must a CPU-performance concern have profiling evidence before blocking?
73. Is there individual review before integration and combined-diff review later?
74. Must required GitHub checks be green before integration?

### Integration and repair

75. **Critical:** Does merger mean locally combining task branches, not merging
    their GitHub PRs?
76. Does the integration PR target `bleed`?
77. Do individual task PRs remain open for inspection?
78. May the merger exclude a task marked `first iteration` or failed review?
79. Should nontrivial conflicts return to the responsible worker?
80. **Critical:** Do integrated-test fixes go onto original task branches or new
    repair branches based on the integration commit?
81. Who owns a failure caused by the interaction of two tasks?
82. Does every integration round get a new branch, or update one persistent branch?
83. Is there one visible integration PR or one PR per integration round?
84. After four failed rounds, publish the safe subset or mark the whole batch as
    first iteration?
85. **Critical:** Is a task complete before combined testing or only after the
    integration branch passes?
86. Does the user remain the only person who merges the final PR into `bleed`?

### Combined testing

87. Does every worker run three task-specific scenarios, or may the coordinator
    assign cross-feature regression scenarios?
88. Must all three integration cycles be adversarial, or can one be a normal full
    match?
89. Can one well-designed integrated game satisfy evidence for multiple tasks?
90. Should a passing worker terminate if another worker's later fix could affect it?
91. Must every passing scenario rerun after each integration repair?
92. Must the last integration round include a real full match to natural conclusion?
93. Should old pre-Codex/control bots be included automatically for AI behavior?
94. What is the maximum total wall-clock time for a five-task batch?

## Suggested first response set

Answer questions 1, 3, 6, 8-13, 26, 37, 43, 52, 59, 66, 75, 80, and 85 first.
Those choices determine whether the system can be built without later structural
rework.
