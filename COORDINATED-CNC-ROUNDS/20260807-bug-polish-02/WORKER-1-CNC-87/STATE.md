# Worker State: CNC-87

Reread this file after context compaction, before every code-change cycle, after
test results arrive, and before publication. This is the complete assigned work
contract. Do not read the full task sheet, coordinator state, or another worker's
spec. Read applicable `AGENTS.md`. Inspect another worker's named PR commits only
when the dependency section directs it.

## Assignment

- Worker: `worker-1`
- Task: `CNC-87 — Repair current Codex CLI launching, nested-role configuration, and large-build resource enforcement for coordinated external roles.`
- Change category: `coordinated-development orchestration/tooling correctness and resource-concurrency enforcement (non-gameplay)`
- Balance authority: `Frozen. No game balance, rules, AI strategy/policy, economy, timing, unit, structure, map, or gameplay changes are authorized.`
- Status: `Publication pending`
- Common base branch/SHA: `agent/cnc-20260806-bug-polish-01-release` / `419bee2531d4802bf922c3597b42c6eeb75ab250`
- Task branch: `agent/round-20260807-cnc87-role-launch-lock`
- Intended PR base: `agent/cnc-20260806-bug-polish-01-release`
- Cycle budget: `20` isolated code-change cycles
- Cycles used: `4`
- Game/build lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/locks`
- Game capacity: `2`
- Large-build capacity: `1`
- Task report: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/workers/worker-1-cnc87/COORDINATED-CNC-ROUNDS/20260807-bug-polish-02/WORKER-1-CNC-87/REPORT.md`
- Match-analysis directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/analysis/worker-1-cnc87`
- Liberty Dawn design reference: `.agents/references/LIBERTY-DAWN-DESIGN.md`
- Full-engine game tests completed: `4`
- Terra cycle code reviews: `none yet; required after cycles 5/10/15/20 that occur`
- Sol-xhigh policy escalation: `unused (requires at least 10 game tests; one maximum)`
- PR: `#86 — https://github.com/Realpra1/LibertyDawn/pull/86 (draft; checks running)`

## Integrated repair assignment

- Phase: `isolated implementation`
- Current release branch/head: `not assigned`
- Integration notes: `not assigned`
- Repair branch: `not assigned`
- Repair PR base: `not assigned`
- Integrated cycles used this RC: `0/3`
- Integrated cycles used total: `0/12`

Before relaunching this worker for combined testing or repair, the integrator must
replace these fields with the exact release head, note path, branch, and counters.
During that phase, the repair branch replaces the original task branch as the
writable branch; the task scope and behavioral contract do not change.

## Why and predicted change

Coordinated rounds depend on fresh external Codex sessions after native agent
slots are exhausted. A rejected `codex exec` command can therefore prevent a
five-worker round from starting. A worker-created Commenter or Policy Reviewer
with an improvised model/reasoning selection can silently spend far more than the
role design permits and violates the intended context/cost split. Large builds
are also a global host resource: a worker holding the slot-aware
`large-build-1.lock` and an Integrator independently holding
`large-build.lock` do not contend, so two nominally capacity-one suites can use
CPU and RAM at once and invalidate both resource status and timing evidence.

The common base already contains commit `96ca6049b5`, which changed the obsolete
`codex exec -a never` spelling to the currently accepted
`-c approval_policy="never"`. On the installed `codex-cli 0.146.0`, the current
constructed configuration exits zero through the CLI help/parser path, while
`codex exec -a never --help` exits 2 with `unexpected argument '-a'`. Do not
manufacture another spelling change merely to produce a diff: preserve the
working supported mechanism and add the missing executable regression that will
catch future drift.

After this task, an operator should observe all of the following:

- Every constructed external-role command is accepted by the installed CLI
  without starting or billing an agent during the fast smoke check, while its
  approval, sandbox, model, reasoning, output, and supervision contract remains
  pinned.
- Worker-spawned ordinary Commenters and Policy Reviewers have one enforced
  launch path and run exactly `gpt-5.6-terra` at `medium`; callers cannot
  reconstruct or override those values. Spec Policy Reviewer and escalated
  Policy Reviewer exceptions remain `gpt-5.6-sol` at `high` and `xhigh`.
- Worker, Reviewer, and Integrator large build/test entry paths resolve to one
  slot-aware protocol, one namespace, and capacity one. A second path visibly
  queues until the complete first process tree is finished; a failed or stale
  holder does not strand the slot; game reservations remain independent.
- A fast local regression reports actionable evidence if CLI parsing changes,
  role pinning is bypassed, process trees overlap, mixed lock names appear, or a
  holder lifecycle is mishandled.

## Authoritative behavior

Preserve and implement this literal task contract:

> Repair current Codex CLI launching, nested-role configuration, and large-build
> resource enforcement for coordinated external roles. Replace the obsolete
> approval-policy argument with the supported current CLI mechanism for approval
> policy `never`, retaining the launcher's pinned sandbox, model, reasoning,
> output, and supervision behavior. Make worker-spawned Commenter and Policy
> Reviewer role/model/reasoning selection reliable and enforced rather than
> relying on a worker to reconstruct a command: ordinary Commenters and Policy
> Reviewers must use exactly `gpt-5.6-terra` at medium reasoning, while documented
> higher-level policy-role exceptions remain intact. Define and enforce one
> canonical slot-aware lock protocol and namespace for every worker, reviewer,
> and integrator large build: capacity one must serialize all entry paths, while
> independent game slots remain available. Add fast local smoke/regression
> validation that checks the constructed `codex exec` invocation against the
> installed CLI's accepted options without consuming an agent run, verifies
> nested-role configuration enforcement, and launches two independent
> large-build reservation probes through the actual worker and integrator entry
> paths. That regression must prove the second probe cannot enter until the first
> releases, then prove stale or failed holders release safely; it must fail
> clearly if process trees overlap, both `large-build.lock` and
> `large-build-1.lock` exist, or either path can acquire while the other is held.
> The expected result is that external launches begin successfully under the
> current CLI, fresh isolated analysis roles use the coordinator-prescribed
> configuration, and simultaneous large builds queue behind the same
> capacity-one reservation. Do not merely document the setting, weaken
> sandbox/path-envelope validation, prevent workers from running fresh isolated
> analysis roles, weaken approval, sandbox, model, reasoning, output, or
> monitoring settings merely to make the command parse, or weaken resource
> isolation to avoid serialization.

Observable requirements and responsibility boundaries:

1. `launch_role.py` (or one cohesive launcher module it owns) remains the source
   of truth for role-to-model/reasoning, sandbox, session directory, strict
   analysis envelope, output, and supervision policy. A caller selects a role;
   it does not supply or reconstruct protected role settings.
2. The no-agent CLI validation must exercise the installed `codex exec` parser
   with the constructed options, not merely search help text or assert a Python
   list. It must preserve `--ephemeral`, `--json`, `-C`, sandbox, approval
   `never`, exact model, exact reasoning, `-o`, and the prompt/output contract.
3. Strict Commenter and Policy Reviewer JSON path envelopes, staged input rules,
   output filenames, worktree/design-document validation, and the analysis-role
   `workspace-write` sandbox remain intact. Non-analysis roles retain their
   documented `danger-full-access` sandbox and supervision behavior.
4. Ordinary `commenter` and `policy-reviewer` resolve to Terra 5.6 medium with no
   caller override or alternative native/reconstructed path. The documented
   `policy-speccer` Sol 5.6 high and `policy-escalation` Sol 5.6 xhigh mappings
   remain distinct and tested. Other role mappings are not in scope to retune.
5. There is one repo-owned large-build reservation policy. Worker, cycle/final
   Reviewer, and Integrator guidance and executable entry paths delegate to that
   policy without reconstructing a filename, capacity, alias, or direct `flock`
   command. It covers `make`, `make all`, `make test`, `make check`, equivalent
   `dotnet`/`msbuild` full builds or test suites, packaging builds, and other
   comparably expensive shared-engine checks. Cheap file inspection and focused
   pure-Python smoke checks do not need the slot.
6. Capacity one means no overlap from entry until the entire foreground command
   process tree is done. The implementation must not report release while a
   spawned descendant continues build/test work. Nonzero exit, signal,
   cancellation, stale metadata, and an abandoned holder must have explicit,
   safe behavior and must not turn an old lock-file body into a permanent lock.
7. Lock identity is a kernel-held lock on one canonical path/namespace. Never
   unlink or replace a lock inode while it may be held, because another process
   could then lock a new inode under the same pathname. Fresh regression runs
   create only the canonical file; a mixed legacy/canonical namespace is a clear
   failure with both paths named, not a silent pass or unsafe cleanup.
8. The `game` resource remains capacity two and independently acquirable while a
   `large-build` holder is active. Do not serialize games behind the build slot or
   change game capacity as a shortcut.
9. Diagnostics must distinguish requested/queued, rejected/invalid, acquired,
   owner/entry role, child/process-tree lifecycle, released, timed out, and final
   command outcome. Bounded durable metadata may aid diagnosis, but live held
   status must follow the kernel lock rather than stale JSON alone.

## Forbidden behavior and failure signals

- Any emitted `codex exec -a never`, absent/changed approval policy, interactive
  approval fallback, weaker sandbox, unpinned model/reasoning, lost `--json` or
  `-o`, changed session directory, missing supervisor metadata, or real agent
  event in the no-agent CLI smoke is a failure.
- A unit test that only compares an expected command list, mocks the CLI parser,
  or greps `codex exec --help` without submitting the constructed options to the
  installed parser does not meet the CLI regression.
- Documentation alone is not enforcement. A remaining ordinary native
  Commenter/Policy Reviewer path on which a worker can pass `max`, select another
  model, or omit the no-history/strict-envelope boundary is a failure.
- Collapsing all policy roles onto Terra medium is a failure: `policy-speccer`
  and `policy-escalation` are intentional higher-tier exceptions. Conversely,
  allowing ordinary `policy-reviewer` or `commenter` to inherit or override the
  worker's Sol model/reasoning is a failure.
- Weakening `validate_analysis_job`, permitting relative or symlink-escaped
  inputs, extra inline job fields, a different design document, or output outside
  the role directory is a release blocker.
- Direct `flock` or a second wrapper/alias that can choose a different large-build
  filename or capacity is a failure even if the documentation recommends the
  canonical route.
- Both `large-build.lock` and `large-build-1.lock` in a clean probe directory,
  acquisition by either entry path while the other is live, an enter timestamp
  before the prior process tree's exit, or a surviving descendant when release is
  announced is a failure.
- Treating file existence or stale owner JSON as a held lock, deleting/replacing
  a possibly held inode, swallowing a child's nonzero exit, leaking a descendant,
  leaving a killed holder permanently blocking, or reporting success after a
  timeout is a failure.
- Routing `game` through the capacity-one large-build lock, reducing the two game
  slots, sharing game support/log/save/port directories, or serializing
  independent game probes behind a large build is a failure.
- Repeated fixed sleeps as proof of serialization are too flaky. Tests require
  handshake markers and monotonic enter/exit timestamps, bounded timeouts, exact
  PIDs/process groups, and an asserted final ordering.
- Do not add gameplay, AI, map, rules, balance, unrelated build-system, or broad
  coordinator refactors. Do not add noisy global process scans or broad
  process-name kills.

## Relevant current implementation and control behavior

- `.agents/skills/coordinate-cnc-development/scripts/launch_role.py` owns a
  `ROLES` map. At the base SHA it maps `commenter` and `policy-reviewer` to
  `gpt-5.6-terra`/`medium`, `policy-speccer` to `gpt-5.6-sol`/`high`, and
  `policy-escalation` to `gpt-5.6-sol`/`xhigh`.
- Its current command is `codex exec --ephemeral --json -C <session-dir> -s
  <sandbox> -c approval_policy="never" -m <model> -c
  model_reasoning_effort="<effort>" -o <last-message> <prompt>`. Analysis roles
  run in the exclusive output directory under `workspace-write`; other roles use
  the worktree under `danger-full-access`.
- `validate_analysis_job` already enforces strict path-only JSON: Commenter gets
  staged artifact paths and `NARRATIVE.md`; policy roles get exactly the design,
  staged task-context, staged narrative, and `POLICY-REVIEW.md` paths. Preserve
  these checks.
- Background launch recursively invokes the same launcher with `--supervised`,
  uses `start_new_session=True`, and records `supervisor.json`. The supervised
  process writes `events.jsonl`, `process.json`, final status/exit, and
  `last-message.md`. `--print-command` exposes command metadata without launching
  Codex, but there is no automated installed-parser regression.
- `.agents/skills/coordinate-cnc-development/SKILL.md` currently prefers native
  agents and tells the caller to validate an envelope with `--print-command`
  before a native no-history spawn. That native spawn still requires a worker to
  reconstruct model and reasoning values, which is the observed bypass. Worker,
  Integrator, and state-template prose name fresh Terra roles but do not all
  enforce the executable route.
- `.agents/skills/coordinate-cnc-development/scripts/with_resource_slots.py`
  creates indexed paths `<resource>-<index>.lock`, so capacity-one `large-build`
  uses `large-build-1.lock`. It uses nonblocking `fcntl.flock`, waits up to a
  timeout, writes owner JSON while held, runs the direct child with
  `subprocess.run`, and releases in `finally`.
- Normal failed-child behavior is already useful: a probe whose child exited 7
  returned 7, and a following capacity-one probe reacquired immediately. Preserve
  exit-code propagation and safe release.
- Current direct-child lifetime is insufficient for detached descendants. A
  control probe launched a detached 1.5-second grandchild, the wrapper returned,
  and a second holder entered while that descendant was still alive. Process-tree
  ownership/lifetime must be made explicit and tested.
- The worker state template documents a concrete slot-aware `game` command but no
  equally unambiguous large-build command. The Integrator skill says to use global
  slots without naming an executable entry path. This permits ad hoc direct
  `flock`.
- A spec-time control probe held `large-build-1.lock` through
  `with_resource_slots.py`; direct `flock` on `large-build.lock` entered in 14.3
  ms, whereas a second slot-aware probe timed out with exit 75. The directory then
  contained both filenames. This exactly demonstrates that each mechanism works
  alone but capacity one is not global across namespaces.
- No test at the base SHA targets `launch_role.py` or
  `with_resource_slots.py`. The repository's Python regression convention is
  `unittest` under `tests/`, exemplified by `tests/test_launch_ai_parallel.py`.
- Open-PR inspection found no active CNC-87 task PR. PR #84 supplies the recorded
  common base and is completed prior-round work, not a dependency to modify.

## Likely wrong approaches and challenges

- Reverting or rewriting the already-correct approval-policy spelling instead of
  protecting it with an installed-parser test would risk regression without
  addressing the remaining task.
- Import-time assertions on `ROLES`, command snapshots, or fake-only CLI tests can
  pass while a future installed CLI rejects the command. Keep unit structure
  checks, but include a no-agent real-parser check and clear version/argv/stderr
  failure output.
- Passing `--help` in a way that short-circuits before parsing the constructed
  options can create false confidence. Prove in the regression that injecting the
  legacy `-a` (or another known-invalid option) makes the validator fail.
- A caller-selectable `--model`, `--effort`, `--resource`, capacity, or lock path
  simply relocates the reconstruction bug. Protected role and capacity-one
  policy belong to the owning launcher/resource abstraction.
- Removing native analysis roles only in one prose file leaves other Worker,
  Integrator, match-review, spec, or review instructions as bypasses. Inventory
  and update every relevant role instruction/template while keeping native agents
  available for roles not covered by this exact nested analysis requirement.
- A second convenience wrapper can drift from the first. Worker, Reviewer, and
  Integrator may have distinct user-facing entry labels for diagnostic ownership,
  but all must delegate to one canonical acquisition implementation and identity.
- File-name normalization alone is not enough if an old direct-`flock` command
  remains executable. Conversely, deleting `large-build.lock` automatically can
  split an active inode and create the very overlap being fixed.
- The current wrapper owns only the direct child lifetime. Process groups,
  descendants, signals, cancellation, and wrapper death need deliberate semantics
  so a release means expensive work is truly over. Do not solve this with a broad
  machine-wide process scan.
- Tests based only on elapsed sleep can be nondeterministic under load. Use
  ready/enter/exit handshakes, monotonic times, unique temporary directories, and
  bounded polling; report the complete event sequence on failure.
- A stale lock file is not itself a stale held lock under `flock`. Status and
  cleanup must distinguish an unlocked file with old JSON from a live kernel
  lock. Never infer occupancy from pathname existence alone.
- Running two real builds merely to test locking is slow and noisy. The fast
  regression should use short independent process probes through the exact public
  Worker and Integrator entry paths; separately run representative real CNC build
  checks under the same canonical route.
- `launch_role.py` currently mixes envelope validation, command construction,
  background supervision, metadata, and execution in one approximately
  250-line module. Split focused pure helpers only where it makes the protected
  policy and subprocess boundaries easier to test; avoid a broad framework
  rewrite. The approximately 100-line resource helper should retain one cohesive
  lock/lifecycle responsibility.

## Competing systems and ownership

There are no game actors, production queues, cash consumers, targets, orders, or
AI managers affected by this non-gameplay task. The competing systems are host
processes and orchestration entry paths:

- **Role policy owner:** `launch_role.py` owns role identifiers, exact
  model/reasoning, sandbox/session directory, CLI options, prompt/output, strict
  analysis-envelope validation, and foreground/background supervision. Workflow
  skills and worker states are consumers and must not duplicate protected values.
- **Nested launch contenders:** Worker match Commenters, ordinary Policy
  Reviewers, spec Policy Reviewers, escalated Policy Reviewers, Integrator match
  analysis, and coordinator-launched external sessions share launcher behavior.
  Only ordinary Commenter/Policy Reviewer pinning is changed; higher policy tiers
  and unrelated role mappings are regression surfaces.
- **Resource policy owner:** one shared resource-slot module owns canonical names,
  capacity, acquisition/wait, live-owner state, timeout, child/process-tree
  lifecycle, exit propagation, and release. Role-specific entry commands delegate
  to it.
- **Large-build contenders:** every isolated Worker, repair Worker, cycle/final
  Reviewer that executes an expensive suite, and Integrator candidate/release
  build can run `make all/test/check`, equivalent full `dotnet`/`msbuild`, or
  packaging work in separate worktrees while sharing host CPU/RAM and the global
  lock directory.
- **Legacy competitor:** direct `flock <lock-dir>/large-build.lock` bypasses the
  indexed slot namespace and must disappear from all documented/executable paths;
  mixed namespace must remain an explicit diagnostic failure.
- **Independent resource:** full-engine games use `game-1.lock`/`game-2.lock`,
  capacity two, isolated support directories and ports. They must continue while
  a large build waits or runs.
- **OS/runtime boundaries:** `fcntl.flock` is per inode/open file description;
  lock-file text can be stale; unlink/recreate can split identity; subprocess
  groups and inherited descriptors affect descendant lifetime; signals and
  abrupt exits must not create false release or permanent exclusion.
- **Codex CLI/user configuration:** the repository invocation pins the task's
  protected settings and may load user configuration for unrelated settings.
  The smoke must identify executable/version and parser failure without changing
  user configuration, authenticating a run, or generating an agent session.

## Cross-worker dependencies

- No active CNC-87 task PR or prerequisite exists. PR #84 is the supplied common
  base, already represented by SHA `419bee2531d4802bf922c3597b42c6eeb75ab250`.
- This task has material operational overlap with every concurrent Worker,
  Reviewer, and Integrator because they share the launcher contract and the
  absolute lock directory. During isolated implementation, do not edit another
  worker's state/spec or infer its task details.
- A process launched from an unchanged worktree can still use the legacy direct
  lock path while the new task branch uses the canonical path. Therefore CNC-87
  evidence proves only participants that use the changed entry paths. The
  coordinator/integrator must not claim round-wide capacity-one enforcement until
  the relevant role instructions/scripts from this PR are present in the tested
  candidate and all active large builds use them.
- Before publication, inspect the common PR/base and any newly opened PR whose
  scoped diff touches `.agents/skills/coordinate-cnc-development/`,
  `.agents/skills/integrate-cnc-release/`, `.agents/skills/review-cnc-pr/`, or the
  worker-state template. Monitor commits only; do not read another worker spec.
  If such a PR appears, rebase/resolve ownership deliberately and rerun the full
  launcher and two-entry contention regression.
- Do not push directly to `bleed` and do not merge PR #84 or any task PR.

If this section names another task PR, inspect that PR's commits while working and
before publication. Do not read its worker spec.

## Spec-time policy consultation

- Proposed-policy narrative: `not applicable — CNC-87 changes orchestration, CLI construction, and host-resource exclusion; it changes no gameplay or AI policy.`
- Sol-high policy review: `not applicable — a Liberty Dawn playtester policy verdict cannot inform CLI parsing or process locking.`
- Verdict and confidence: `not applicable (high confidence that policy consultation is irrelevant)`
- Recommendations adopted as testable hypotheses: `none; repository/runtime evidence supplies the relevant hypotheses.`
- Recommendations rejected or deferred, with reason: `The consultation itself was skipped under the role rule permitting a skip only when policy is genuinely irrelevant. Full-engine games remain required as orchestration regressions, but their strategic outcome is not a policy acceptance metric.`

## Acceptance and tests

### Literal black-box acceptance

From the task worktree and a new task-local temporary/output directory, an
operator performs one scripted smoke/regression with no real Codex agent launch:

1. Stage minimal valid strict Commenter and Policy Reviewer envelopes and invoke
   the repository launcher validation path for `commenter`, `policy-reviewer`,
   `policy-speccer`, and `policy-escalation`.
2. Submit each constructed `codex exec` option set to the installed CLI parser in
   no-agent mode. Capture the Codex executable/version, exit, stderr, and sanitized
   argv. All commands parse; ordinary roles show exactly Terra/medium and the two
   higher-tier policy exceptions show exactly Sol/high and Sol/xhigh. Sandbox,
   approval `never`, ephemeral/JSON/output, session directory, prompt, strict
   paths, and supervision-related metadata remain present.
3. Through the documented executable **Worker** large-build entry path, start a
   probe that announces ready/acquired, holds until a controlled release, and
   keeps a child process alive. While it is held, start a second probe through the
   documented executable **Integrator** entry path. The second announces queued
   but cannot announce acquired/enter. Release the complete first process tree;
   only then may the Integrator enter and complete. Repeat with entry order
   reversed.
4. Exercise a child that exits nonzero, a killed/abandoned holder, a canonical
   file containing stale owner text but no kernel lock, and an attempted detached
   descendant. Exit codes are preserved, no expensive descendant overlaps a new
   holder, and the next entry path safely acquires within a bounded timeout.
5. Assert the clean directory contains one canonical large-build lock identity
   and never both `large-build.lock` and `large-build-1.lock`. A negative mixed-
   namespace fixture must fail with both paths and remediation guidance named.
   While a large-build holder is active, a `game` reservation must acquire one of
   its two independent slots.

The final observable outcome is an ordered event record proving maximum
large-build concurrency of one across the actual Worker and Integrator paths,
zero process-tree overlap, safe recovery, independent game acquisition, and exact
role/CLI configuration, with no billed/created Codex agent session.

After the fast regression passes, run one real full-engine ordinary-CNC-AI test
and launch its Commenter through the newly enforced ordinary analysis path. This
is the end-to-end proof that games and real external analysis still work; verify
the Commenter's `process.json` records `gpt-5.6-terra`/`medium`, its output is
`NARRATIVE.md`, and it cannot write the repository worktree.

### Focused checks and instrumentation

Add repository-local `unittest` coverage using unique temporary directories and
short deterministic process probes. The exact file split is the worker's design
choice; keep launcher-policy tests separate from lock/process-lifecycle tests
when that improves diagnosis. Each test must state and record its hypothesis:

| Test | Failure hypothesis and perturbation | Exact failure signal | Required pass evidence |
|---|---|---|---|
| Installed CLI parser smoke | A CLI upgrade rejects an option even though the Python command snapshot looks right. Run the constructed argv through the installed parser without a prompt run; inject legacy `-a` as the negative control. | Nonzero real-parser exit for the production command, any agent event/session, or negative control unexpectedly passing. | Production options exit zero; negative control exits nonzero; report includes Codex path/version, sanitized argv, and actionable stderr. |
| Command contract | Fixing parsing dropped approval, sandbox, model, reasoning, output, ephemeral/JSON, CWD, prompt, or supervision fields. Check foreground and background metadata with a fake executable where needed. | Missing/duplicated/changed option, output outside expected directory, missing process/supervisor record, or swallowed fake exit. | Every protected option and metadata field is exact; fake exit propagates; no real agent is launched. |
| Strict envelopes | Refactoring model enforcement weakens isolation. Try extra keys, relative paths, symlink escape, wrong design doc, missing staged file, wrong output filename, and output outside role dir. | Any invalid envelope reaches CLI execution. | Each is rejected before execution with its field/path named; valid staged copies pass. |
| Role pinning | A worker can override or bypass ordinary analysis configuration, or higher exceptions are collapsed. Exercise every policy-role ID and attempted overrides/direct path. | Ordinary role is not Terra/medium, override is accepted, a workflow still instructs native reconstruction, or higher role differs from Sol high/xhigh. | One enforced path; exact four mappings; no protected caller override; all relevant role instructions point to it. |
| Same-path capacity | Slot logic itself permits two holders. Start two canonical capacity-one probes with handshakes. | Second enter precedes first tree exit or maximum live holder count exceeds one. | Ordered markers prove queue then handoff and capacity one. |
| Worker/Integrator cross-path | Role entry paths silently choose different files/protocols. Run both orders. | Either order overlaps, lock identities differ, or either route bypasses canonical diagnostics. | Both orders serialize on the same canonical identity and report their distinct owner labels. |
| Failure/stale recovery | Nonzero exit, signal, stale JSON, or wrapper death strands the slot or produces false release. | Exit swallowed, timeout after holder is gone, unsafe unlink/replacement, new holder enters while old work lives, or status trusts stale JSON. | Exact nonzero/signal outcome is recorded; complete tree is resolved; next path acquires; live status follows kernel lock. |
| Namespace guard | Legacy direct `flock` coexists with canonical slot. Seed or attempt both pathnames. | Regression passes, silently deletes a possibly held inode, or creates both files. | Clean run has one identity; mixed fixture fails clearly before expensive work and names both paths. |
| Game independence | A unified helper accidentally serializes all resources. Hold large-build and acquire game slots. | Game waits on large-build or game capacity changes from two. | Game enters while build is held; two games can still reserve distinct slots; third waits. |

Useful bounded diagnostics in test artifacts under
`/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/analysis/worker-1-cnc87/`:

- launcher role, exact protected model/reasoning/sandbox, Codex executable and
  version, parser exit and stderr, job/output paths, supervisor PID, child PID,
  process-group/session ID, started/completed timestamps, and final exit;
- resource, canonical lock path/slot, capacity, caller role/entry path,
  request/queue/acquire/child-start/child-exit/release monotonic timestamps,
  holder PID/process group, child exit/signal, timeout, and next-acquire result;
- test-local event JSON/JSONL with a maximum-live-holder calculation and explicit
  detection of legacy/canonical filenames and surviving descendants.

Never log credentials, environment dumps, complete user configuration, or
per-poll spam. Remove temporary debug prints and retain only concise actionable
state transitions. Handled errors must name missing Codex, unsupported option,
invalid role/envelope, inaccessible lock directory, mixed namespace, timeout,
child failure, and unsafe live process-tree conditions. Do not translate these
into success.

The fast Python suite should normally finish within 15 seconds on an idle host,
use bounded waits, and remain near-zero CPU while queued. The production helper
must not scan the machine or allocate/poll without bound; lock setup overhead
should be negligible compared with a build. Record elapsed/CPU evidence or a
credible bounded argument in the report.

### Ordinary and differential games

This task does not change AI strategy, gameplay, or persisted state, so games are
regression evidence for resource independence and the real Commenter route, not a
measure of strategic improvement. Nevertheless, the first behavioral test after
the first implementation change must include a full-engine ordinary-AI CNC game,
normally headless MAX, rather than waiting for unit-only confidence.

Test 1 uses `mods/cnc/maps/Empire-Earth.oramap`, seed `87001`, the normal example
lobby with ordinary Skynet and Brutalis bots, headless MAX, and at least tick
10000. Hold a canonical large-build probe, queue the opposite role's large-build
probe, and concurrently acquire a normal `game` slot for the match. Failure
hypothesis: unifying locks accidentally blocks games or the role changes prevent
normal analysis. Failure signals: game cannot acquire while build is held, wrong
map/options/bots, no MAX/tick evidence, fatal/desync, shared support directory,
large-build overlap, Commenter launch config not Terra/medium, or absent factual
`NARRATIVE.md`. Pass evidence: independent game entry, intended map/seed/bots,
MAX activation, advancing ticks and clean stop, serialized build probes, and a
real isolated Commenter artifact with exact recorded configuration.

Increase difficulty immediately after the smoke: reverse acquisition order,
change seed/timing/duration, inject failure/stale state, vary process-tree shape,
and eventually run a natural-conclusion match. Do not repeat the same happy path.
After every materially judged match or paired batch, stage only authorized logs,
manifest, summary, and metrics and launch a fresh Commenter through the enforced
launcher. Policy Reviewer feedback is not required because no AI policy is being
changed; the Policy Reviewer configuration is instead covered by the strict
no-agent regression.

Save/load is not relevant: the changed orchestration has no serialized game
state, and a reload cannot strengthen resource-lock or launcher evidence. Routing
and transport topology are also not relevant. One later Archipelago game may be
used as a geometry/duration variation, but do not claim island behavior as CNC-87
acceptance.

### Old-behavior control and required improvement

Use base SHA `419bee2531d4802bf922c3597b42c6eeb75ab250` in an isolated control
worktree and the changed task head with matched Python/Codex executable, lock-dir
filesystem, probe commands, hold durations, CPU load, and timeouts.

- **Split-lock control:** reproduce the packet's actual old pair: worker
  slot-aware `large-build-1.lock` versus Integrator direct
  `large-build.lock`. The control is expected to overlap and create both names;
  record enter/exit times and maximum live count. On the changed head, invoke the
  actual documented Worker and Integrator routes in both orders. Required
  improvement is decisive: control maximum concurrency 2 versus changed maximum
  1; changed second-enter must be at or after complete first-tree exit, with one
  canonical identity.
- **Process-tree control:** reproduce the base wrapper returning while a detached
  descendant still lives and a second holder enters. The changed result must have
  zero overlap: it either keeps ownership until the descendant finishes or safely
  terminates/waits for its assigned tree under a documented policy.
- **Nested-role control:** record that base `ROLES` values themselves are already
  correct but the workflow still permits a native/reconstructed path. The changed
  head must expose one enforceable Worker path with no model/reasoning override;
  static call-site inventory plus executable attempts to override must show the
  bypass removed.
- **CLI control:** the base already uses the supported approval config and should
  parse. The changed head must remain at parity on every protected option while
  adding a regression that fails on the legacy option. A made-up behavioral win
  over the base is neither required nor credible for this already-landed part.
- **Game regression:** keep map, seed, bots, factions, starts, lobby options,
  content, and exit condition matched when comparing base and changed runtime.
  Require no material degradation in startup, MAX tick progress, fatal/desync
  status, or wall/CPU time attributable to the lock/launcher changes. Match win is
  not a CNC-87 metric.

Repeated overlap, only marginal reduction, timing ambiguity, or a pass based only
on feature-fired logs is a defect. Investigate until event ordering and final
process outcomes are decisive.

### Adversarial cases

After the latest relevant fix, require at least these three distinct clean
adversarial scenarios. Pair each with a full-engine ordinary-AI game using an
independent game slot so the general worker contract is met and cross-resource
coupling is challenged; the short lock probes, not game outcome, carry the
CNC-87 causal evidence.

1. **Worker holds, Integrator queues; ordinary analysis path.** Empire Earth,
   seed `87001`, headless MAX to at least tick 10000. The Worker probe holds a
   living child; the Integrator starts second. Failure hypothesis: paths use
   different namespaces or game/Commenter work is blocked. Force both paths and
   the game to request concurrently. Failure signal: Integrator enter before the
   Worker tree exits, game blocked, two lock names, bad map/bot/tick evidence, or
   Commenter not Terra/medium. Pass: strict ordering, game progress, and factual
   Commenter output.
2. **Integrator holds, Worker queues; failure and stale recovery.** Change seed
   to `87002` and use a longer/different timing window or Archipelago with valid
   ordinary bots. Make the Integrator child exit nonzero, then test a stale
   canonical metadata file and immediate Worker recovery. Failure hypothesis:
   release works only in one order or status confuses stale text with a live
   lock. Failure signal: swallowed exit, stranded slot, unsafe file replacement,
   game blocked, or reordered markers. Pass: exact failure recorded, no overlap,
   next acquisition succeeds, game advances, only canonical identity exists.
3. **Process-tree/cancellation and mixed-namespace attack.** Seed `87003`; run an
   ordinary Empire Earth game to natural conclusion at headless MAX. Attempt a
   detached descendant and terminate/cancel the direct holder under the supported
   supervision path; separately seed both legacy and canonical lock names as a
   negative fixture. Failure hypothesis: wrapper release ignores descendants or
   mixed names evade the smoke. Failure signal: new holder overlaps surviving
   work, leaked process, permanent lock, negative fixture passes, or natural game
   lacks outcome/MAX evidence. Pass: assigned tree fully resolved, next holder
   enters only afterward, mixed fixture fails clearly without unsafe cleanup, and
   natural game completes independently.

Additional focused adversaries before publication:

- invalid CLI option/config and missing Codex executable produce actionable
  failure without agent execution;
- ordinary role override attempts are rejected, while both documented higher
  policy tiers remain exact;
- two independent game slots enter during a large build and a third game waits,
  proving namespace separation and capacity preservation;
- timeout and permission errors return nonzero and do not claim acquisition;
- foreground and background fake-role executions preserve output and monitoring
  behavior across child success, child failure, and supervisor completion.

If a fix follows any adversarial failure, restart the three-clean-scenario count
for affected scenarios and rerun literal acceptance.

### Final regression

On the final task head, from clean task-local output/lock directories:

1. Run the full fast local launcher/role/lock suite against the installed Codex
   CLI and record version, exact protected settings, negative-control failure,
   both cross-path acquisition orders, maximum concurrency, process-tree result,
   stale/failed recovery, namespace inventory, and game-slot independence.
2. Run representative real CNC `make test` and `make check`/required scoped checks
   through the canonical large-build entry path. Do not build or test unsupported
   mods except shared engine compilation required by CNC.
3. Run the literal Worker-first/Integrator-second acceptance once more with the
   strongest compatible process-tree and failure timing stress after all fixes.
4. Run one fresh ordinary-AI headless MAX game to a natural conclusion with
   isolated support/log/replay/benchmark paths while using the global `game`
   wrapper. Prove intended map, seed, bots, actors/options, MAX activation,
   advancing ticks, clean logs, and final outcome. Launch and verify its factual
   Commenter through the enforced Terra-medium path.
5. Confirm `git diff` contains only scoped orchestration skills/scripts/tests and
   this state/report, no raw logs/build output, no temporary diagnostics, no
   gameplay/balance changes, and no legacy direct large-build path. Run required
   GitHub checks and record their final status.

The final literal pass is not a request/reservation log: it is the observed
ordered entry/exit outcome, no overlapping process tree, successful recovery and
reacquisition, independent completed game, accepted installed-CLI invocation,
and correctly configured real Commenter artifact.

## Implementation rules

### Concise implementation and publication plan

1. Record base/control results first: current installed CLI positive/legacy
   negative parser outcomes, role/path inventory, split-name overlap, direct-tree
   escape, and normal failed-child release.
2. Add focused failing regressions for installed CLI parsing, exact role policy,
   strict envelopes, cross-entry serialization, lifecycle recovery, mixed
   namespace, and game independence. Keep no-agent tests fast and deterministic.
3. Centralize/enforce ordinary nested-role invocation in the owning launcher and
   update every Worker/Integrator/spec/review consumer so it selects a role but
   cannot reconstruct protected model/reasoning. Preserve higher-tier mappings,
   strict path validation, sandbox, output, and supervision.
4. Define one canonical large-build policy/identity and make Worker, Reviewer,
   and Integrator executable entry paths delegate to it. Address full assigned
   process-tree lifetime, signals/failures, stale metadata, status, and mixed-name
   detection without unsafe inode deletion. Preserve the independent generic game
   slots.
5. Immediately after the first implementation change and baseline focused gates,
   run full-engine test 1 with ordinary AI and the real Commenter path. Use its
   evidence to decide the next harder contention/lifecycle test.
6. Complete matched old-control comparisons, three clean adversarial scenarios,
   representative guarded CNC build/checks, the final literal regression, task
   report, scoped commits, PR, cycle reviews due, final review, and GitHub checks.

- Do not ask implementation or preference questions. Investigate code, history,
  controls, configs, tests, and evidence; choose the strongest safe option and
  record material assumptions. Stop only this task for a real authority,
  credential, missing-file, unsafe-path, or irreducible blocker.
- Keep responsibilities separate and dependencies explicit. Prefer short,
  cohesive classes and functions; split oversized responsibilities when that
  improves cohesion, testability, or hot-path clarity without unrelated churn.
  Preserve unrelated behavior and user changes.
- Put tunable policy in the owning rules/config/save/map layer and algorithmic
  invariants in code. Do not duplicate policy across AI personalities or hide a
  rules/config concern in test-only code.
- Treat balance as frozen unless `Balance authority` above expressly permits the
  specific surface. Never change cost, HP, damage, armor, speed, timing, power,
  prerequisites, probabilities, resource values, or comparable tuning to make a
  behavior test pass. Unauthorized balance changes invalidate the result because
  they can fake improvement. Record a needed balance change as deferred work.
- For an expressly authorized balance-only task, test its bounded local effect
  first: affected-unit survival, useful damage, exchange value, adaptive rating,
  and selection frequency as relevant. Treat whole-match outcome/composition as
  secondary regression evidence unless the task explicitly makes it primary.
- Add proportionate unit/interface/static tests. Add useful bounded debug logging
  and handled warnings/errors at the owning boundary: make failures actionable,
  never silently swallow exceptions or substitute success, avoid per-tick spam,
  and remove obsolete/noisy temporary instrumentation before publication.
- Keep deterministic simulation hot paths bounded. Avoid repeated full-map/unit
  scans, uncontrolled allocations, nondeterministic iteration/order, unbounded
  retry queues, or logging that materially reduces MAX throughput. Measure or
  explain performance-sensitive changes with current evidence.
- Inventory and test ordinary modules that compete for the same units, queues,
  cash, reservations, targets, repair, or retargeting. For this non-game task,
  the inventory above establishes that there are no such modules; instead force
  every host-process/resource contender listed under `Competing systems`.
- Record worthwhile out-of-scope fixes, refactors, and optimizations under
  `Deferred work` in the task report/handoff; never expand scope silently or make
  concurrent workers edit a shared deferred-work file.
- Keep raw logs/replays/saves/profiles outside Git or under ignored
  `AUTONOMOUS-CNC-LOGS/`. Record concise paths, seeds, and conclusions here or in
  the task report.
- Never push directly to `bleed`, merge a GitHub PR, or edit the task sheet or
  coordinator state. Update this state and task report on the recorded task branch
  or, during integrated repair, the recorded repair branch.

## Evidence-driven loop

One cycle begins when a product-code/config change is made. A cycle may build,
run focused checks, and execute up to two materially useful games needed to judge
that change. Merely reading logs or correcting an invalid harness without a
product change does not begin another cycle; record it honestly.

Treat full-engine simulations with ordinary AI as cheap primary feedback. The
first behavioral test after the first implementation change must be a full-engine
ordinary-AI game, normally headless MAX, with every relevant normal module enabled
from test 1. A focused custom map, pre-spawned actors, short distance, or obvious
cheese setup may make the event immediate, but it must not replace the real engine
or ordinary AI with a passive/custom bot or isolated manager fixture. Run focused
unit/static checks as useful baseline gates before or alongside it; do not delay
game evidence while accumulating unit-only confidence. Keep available game slots
working while other agents code or analyze because simulation is cheaper than
missing human feedback.

When a required situation is rare, construct it deliberately in a full-engine
custom map while keeping ordinary AIs and every relevant normal module enabled.
For this task the rare states are host-process states, so construct those with
process probes while a normal unmodified CNC game runs independently; do not add
test-only game actors or AI policy.

For every change to AI strategy, priorities, economy, production, targeting,
recovery, or tactics, compare against old behavior repeatedly throughout the loop.
This task makes none of those changes. Use the matched orchestration controls
defined above and require game runtime parity rather than a strategic win.

Treat all tests as attempts to break the implementation. Compilation, lint, and
static analysis are baseline gates; every unit, integration, save/load, replay,
or game test must exercise a regression risk, boundary, invalidation, contention,
failure/recovery path, or assumption under pressure. Before running it, record:

- Failure hypothesis: what plausible defect this test could expose.
- Perturbation: what is made harder or different from the last passing test.
- Failure signal: the exact log/state/operator-visible outcome that proves breakage.
- Pass evidence: the final observable result needed to falsify the hypothesis.

The existing broad regression suite counts as an adversarial gate against breaking
unrelated behavior, but it does not replace targeted falsification of this task.

One initial full-engine smoke may establish that the harness and simplest path
work. As soon as it passes, change at least one meaningful dimension—entry order,
hold timing, failure/signal, stale state, process-tree shape, resource namespace,
game duration/map, or role—and make every later test harder or materially
different. Never spend cycles on near-identical happy-path confirmations.

For each cycle:

1. Reread this state, current diff, and previous evidence.
2. Implement or revise the smallest evidence-driven change.
3. Run focused unit/static checks and fix relevant errors or warnings without
   treating them as a substitute for the game.
4. From cycle 1, run the simplest not-yet-proven full-engine ordinary-AI
   adversarial scenario that can falsify the current implementation while proving
   the requested outcome if it survives.
5. Diagnose results against desired and forbidden behavior. Add bounded
   instrumentation when evidence cannot distinguish launch request/rejection,
   protected configuration, reservation request/owner, queue/acquire/release,
   process-tree state, command exit, or final operator-visible outcome.
6. Remove or reduce obsolete/noisy diagnostics after they answer the question.
7. Update the cycle journal before making another code change.

## Interim code-review loop

After product-change cycles 5, 10, 15, and 20 that occur, and before the next
product change or publication, launch a fresh Terra 5.6 medium
`cycle-reviewer`. Give it a job declaring `cycle` mode and only this state path,
the recorded base SHA, current branch/head and cumulative scoped diff, relevant
evidence through that cycle, and a task-local output path such as
`/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/analysis/worker-1-cnc87/cycle-review-05/CYCLE-REVIEW.md`.

Use the coordinator launcher/enforced role configuration introduced or confirmed
by this task rather than reconstructing model/reasoning. The reviewer writes only
its review artifact and returns at most one `advisory_concern`. Read it, verify its
evidence, and record whether it is adopted or rejected and why. An adopted product
change begins the next ordinary cycle; the review grants no extra cycles. At
cycle 20, either reject the concern with evidence or hand off `First iteration -
testing` if resolving it would require cycle 21. A clear review does not replace
adversarial games, Commenter review, CI, or the final Sol-high task-PR review and
one-response gate.

## Match narrative and policy-feedback loop

After every materially judged full-engine match or paired control batch:

1. Increment `Full-engine game tests completed` for each game, including an
   invalid setup that still ran far enough to expose evidence; label invalid runs.
2. Copy (do not symlink) only the authorized current/control logs, manifests,
   summaries, and metrics into the role output directory's `inputs/` subtree. In
   that directory, write a strict JSON Commenter job containing only their absolute
   `artifacts` paths, optional `design_reference`, and the absolute `output` path
   ending in `NARRATIVE.md`.
3. Launch a no-history fresh `commenter` through the task's enforced external
   launcher path. It must record exactly `gpt-5.6-terra` and `medium`; do not
   reconstruct those values or use a native bypass. Do not stage source code,
   this worker state, the task sheet, implementation notes, or inline job-file
   commentary.
4. Read its factual `NARRATIVE.md`. Verify cited artifacts/ticks and use it to
   confirm the game ran normally and independently of large-build locking.
   Correct the input/evidence rather than editing the narrative into a preferred
   story.
5. Do not launch routine Policy Reviewers for CNC-87 game results because this is
   not AI-policy work. The exact ordinary Policy Reviewer and higher-tier
   configurations are proven by the no-agent regression. If an unrelated AI
   policy issue appears, record it as deferred rather than expanding scope.

Detailed narratives stay under the ignored analysis directory. Preserve their
paths plus concise factual conclusions in the cycle journal and task report.

Use the full engine and real bot types. On Linux use the explicit headless MAX
path when graphics/input are irrelevant. Prove the current run loaded the intended
map, bots, actors, options, activated headless MAX, advanced ticks, flushed logs,
replay/benchmark evidence where configured, and produced the final outcome. A
passive fixture or manager-only simulation is not sole proof.

Use ordinary full matches for emergent AI behavior. This task does not alter AI,
but its required games still use ordinary AIs and every normal module. After
normal acceptance first passes, require the three distinct clean adversarial
scenarios above after the latest relevant fix. If a fix follows a failure,
restart affected clean evidence and rerun the literal acceptance.

Run at least one real full match at headless MAX to a natural conclusion. Do not
waste concurrency on near-copy spawn swaps unless position bias affects an
observed orchestration timing result.

Wrap shared game resources with:

```text
python3 .agents/skills/coordinate-cnc-development/scripts/with_resource_slots.py \
  --lock-dir /root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/locks --resource game --capacity 2 --slots 1 -- COMMAND...
```

Reserve two game slots when using a two-game `launch-ai-parallel.py` batch. Poll
background games within 60 seconds, normally cap them at 30 minutes, isolate every
support directory, settings, log, replay, save, benchmark prefix, map artifact,
port, and display, and judge each run separately. Use concurrent slots for
materially different scenarios. Return to serial tests if contention corrupts
timing or evidence. A required full match may exceed 30 minutes while it continues
making useful progress; stop it when evidence is sufficient or progress stalls.

For expensive setup, optionally save shortly before a critical game event, but
save/load is not CNC-87 acceptance and a reloaded run is never sole evidence.

After 20 unsuccessful code-change cycles, publish the safest useful result as
`First iteration - testing`. Do not pad cycle counts after evidence is sufficient.

When the phase is integrated testing, the isolated 20-cycle cap no longer blocks
the assigned release validation. Use at most three code-change cycles for the
current RC and at most twelve across four RCs, updating both integrated counters.
Test the exact recorded release head before changing code; put any change only on
the recorded repair branch and rerun the materially affected original acceptance,
adversarial, and combined scenarios.

## Completion and publication

Propose `Complete - testing` only after literal acceptance, all required clean
adversarial cases, final regression, task checks, report, PR, and required GitHub
checks pass. Otherwise propose `First iteration - testing` with exact failures and
risks. The reviewer and integrated release determine final status.

The task report must cover behavior, design choices, assumptions, cycle count,
tests, seeds/artifact paths, diagnostics removed or retained, performance and
determinism, old-control configuration and comparative results, PR/checks,
deferred work, and remaining risks. Include exact Codex version/parser evidence,
role mappings, all large-build event orderings, maximum overlap counts, lock-name
inventories, stale/failure/process-tree results, and the real Commenter receipt.

Push the task branch and open one individual PR. Do not merge it. Wait for every
required GitHub check; diagnose and fix relevant failures within the isolated
cycle budget and rerun them. If required checks cannot become green, propose
`First iteration - testing` rather than completion.

When review returns a correction, perform at most one review-response code/test
cycle, applying the highest-impact safe finding you agree with or recording
evidence for rejection. This cycle counts within the 20 isolated cycles; never
silently exceed the budget.

## Cycle journal

| Cycle | Commit/change | Failure hypothesis and perturbation | Checks/games | Narrative/policy/cycle-code review | Failure/pass evidence | Decision/next harder test |
|---|---|---|---|---|---|---|
| 1 | Enforced installed-parser validation and external analysis-role policy; added protected Worker/Reviewer/Integrator large-build entry mode, canonical namespace guard, guardian/subreaper lifecycle, diagnostics, guidance, and regressions. | A correct-looking argv can fail the real CLI; native analysis reconstruction can bypass pinning; Worker/Integrator names can overlap; detached/abandoned trees can release early; game capacity can be coupled accidentally. Perturbed both entry orders, stale/nonzero/mixed names, detached child, SIGKILL of wrapper, and two occupied game slots. | `python3 -W error::ResourceWarning -m unittest -v tests.test_launch_role tests.test_resource_slots`: initial expected failures, then 10/10 pass in 2.12 s. First real `make -j2` compiled successfully but exposed persistent MSBuild descendants. First game harness attempt with `--no-xvfb` failed platform initialization at tick 0 and is not counted. Corrected Empire Earth seed 87001 run passed to tick 10000 under game capacity 2. | Real enforced Commenter: `analysis/worker-1-cnc87/cycle-01-game/commenter/NARRATIVE.md`; receipt records Terra 5.6 medium, exit 0. No policy review (non-policy task). | CLI 0.146.0 production parser exit 0, legacy `-a` exit 2, zero agent events. Old split control max concurrency 2 and both names; old detached control released in 0.0712 s while descendant lived. Changed Worker tree exited at monotonic 237101.793, Integrator entered 237101.804; game independently acquired while Worker held. Game passed in 40.662 s at 245.904 valid ticks/s with intended bots/MAX/tick and clean exit. Real build logged child exit but correctly withheld release while three persistent MSBuild nodes remained. | Treat persistent build-server lifetime as an actionable failure: add bounded graceful descendant completion followed by targeted termination/reaping, then repeat real build before harder cancellation/stale scenarios. |
| 2 | Added bounded descendant grace and targeted termination/reaping for persistent assigned build servers, with exact PID diagnostics and regression. | A successful real build can strand capacity one indefinitely because reusable MSBuild nodes outlive the foreground process. Perturb with a 60-second detached child and a real incremental `make -j2`. | Focused suite 11/11 pass in 3.61 s. Guarded real incremental `make -j2` passed with 0 warnings/errors. | Cycle review not due. Existing cycle-1 Commenter evidence remains applicable because no game behavior changed. | Foreground build exited 0; helper observed assigned PIDs 736650/736651/736655, allowed the grace period, terminated/reaped all three, recorded no SIGKILL, returned 0, and only then released. | Proceed to negative CLI/envelope/background/cancellation hardening, reverse-order failure/stale scenario, then process-tree/mixed-namespace natural game. |
| 3 | Made launcher/supervisor records expose exact protected runtime fields and resource acquisition diagnostics name only the actually held slot. | Candidate-path diagnostics can conceal which game slot is live, and argv-only launcher records make sandbox/session/output supervision harder to audit. Perturb with two simultaneous games, foreground failure/background success, then final Worker-first adopted-child contention. | Affected suite 16/16 pass in 6.01 s. Final-head broad suite previously passed 20/20 in 4.91 s; final guarded `make test` and `make check` pass, each with zero build warnings/errors. Final Empire Earth seed 87001 passed to tick 10000. | Fresh final Commenter: `analysis/worker-1-cnc87/final-acceptance/commenter/NARRATIVE.md`; receipt records Terra 5.6 medium, workspace-write, exact session/output directories, exit 0. No policy review (non-policy task). | Scenario 2: Integrator failure exit 7/tree exit 237500.219, Worker acquire 237500.233; stale owner text ignored by kernel state; seed 87002 passed tick 15000. Scenario 3: mixed names rejected without deletion; cancellation exit 143, detached PID 751035 terminated, release 237768.708, Integrator entry 237768.771; seed 87003 reached natural game over tick 20000. Final literal: Worker child-tree resolution 238291.196, release 238291.198, Integrator acquire 238291.223; game held exact `game-1.lock`, tick 10000, exit 0. Final `make test` reaped PIDs 767032/767034/767035; `make check` reaped 767800/767801/767805 before release. | Implementation/evidence complete. Publish scoped commit/PR, run final independent review and required checks; make no further product change unless that gate identifies a compatible blocker. |
| 4 | Adopted final Sol-high review fix in the contention test: raw descriptor event draining plus teardown-tracked exact probe cleanup on every path. Test-only review-response cycle; production behavior unchanged. | `select()` on `TextIOWrapper` can miss an already user-buffered `queued` line and an assertion can bypass marker release/process wait/pipe closure. Perturb with five isolated full resource-suite repetitions and warnings-as-errors, then the full portfolio. | Five resource-suite repetitions passed: 3.665, 3.615, 3.549, 3.663, 3.721 s. Full 20-test portfolio passed in 5.651 s with `ResourceWarning` as error; `git diff --check` passed. | Final Sol-high review at `analysis/worker-1-cnc87/final-review/FINAL-REVIEW.md` returned ready with one fix. Finding adopted and verified. No cycle-5 Terra review due. | Raw `os.read` drains every complete buffered JSON line before another `select`; every spawned test process is tracked and teardown performs exact terminate, bounded communicate/wait, escalation only for that PID, and pipe closure. No leak/warning/flaky event loss in five repetitions. | Push review-response head, ask the same final reviewer to verify the correction, and require fresh Linux/Windows checks. No new game is needed because this cycle changes test harness only, not product/game/runtime policy. |

## Handoff receipt

- Proposed status:
- Final branch/head:
- PR and checks:
- Cycles used:
- Acceptance evidence:
- Adversarial evidence:
- Old-behavior control and comparative result:
- Match narratives and routine policy-review conclusions:
- Terra cycle code reviews and dispositions:
- Sol-xhigh policy escalation (unused, or test count/path/conclusion):
- Final regression:
- Error/warning and diagnostic-cleanup result:
- Performance/determinism result:
- Deferred work:
- Known failures/risks:
- Relevant artifact paths:
