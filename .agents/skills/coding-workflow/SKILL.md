---
name: coding-workflow
description: Plan, implement, test, integrate, and publish high-quality code changes in software repositories. Use for substantial coding work that creates, changes, fixes, refactors, or integrates application, library, game, infrastructure, or test code. Apply adaptive planning, clarification, modular design, configuration ownership, optional parallel sub-agents, debugging support, testing, deferred-work tracking, and feature-branch pull-request discipline.
---

# Coding Workflow

Use a deliberate engineering workflow while keeping ceremony proportional to the task.

## 1. Establish Scope

1. Read repository instructions and inspect relevant code, tests, configuration, Git status, and recent history before editing.
2. Before diagnosing behavior or proposing changes, inspect available relevant debug, warning, error, crash, test, and service logs. Confirm their timestamps and whether they cover the reported run; state when no relevant current logs exist.
3. Preserve unrelated user changes. Never overwrite or silently include them.
4. Distinguish the requested outcome from optional cleanup.
5. For nontrivial work, present and maintain a concrete development plan before coding. Include investigation, implementation, tests, integration, and publication when applicable. Keep trivial edits lightweight.

## 2. Resolve Material Ambiguity

Ask only questions whose answers materially affect behavior, architecture, compatibility, data, safety, or scope. First investigate answers available from the repository.

When a user decision is necessary:

- Present mutually exclusive options as **A**, **B**, **C**, and so on.
- Put the recommended option first and explain its tradeoff briefly.
- State what will be assumed if the question is useful but non-blocking.
- Do not block on preferences that can be safely adjusted later.

## 3. Choose an Execution Strategy

Work locally when the task is small, tightly coupled, or concentrated in the same files.

Use parallel sub-agents when there are at least two independent, bounded workstreams that can produce useful results concurrently. Do not create agents merely to satisfy process. Respect the available concurrency limit.

For parallel implementation:

1. Define clear ownership, interfaces, dependencies, and validation for every workstream.
2. Isolate mutating work in separate feature branches and preferably separate Git worktrees. Do not let agents edit overlapping files in a shared worktree.
3. Assign one workstream per sub-agent.
4. Give each sub-agent the relevant repository context and the engineering requirements in this skill.
5. Explicitly tell every sub-agent: **Do not spawn additional sub-agents.**
6. Keep integration, conflict resolution, combined testing, and publication under the primary agent's control.

Use this assignment footer:

> Follow the repository instructions and the coding-workflow engineering rules: keep changes scoped and modular, preserve unrelated work, add proportionate tests and diagnostics, and report deferred improvements. Work only on your assigned branch/worktree and commit your result. Do not publish externally. Do not spawn additional sub-agents.

## 4. Implement Maintainable Code

- Separate concerns and give each module, class, object, and function one coherent responsibility.
- Split code when size or coupling harms understanding, testing, or reuse; do not fragment cohesive code solely to reduce line count.
- Keep classes below 400 lines when practical and never above 500 lines; split oversized responsibilities into short modular owners.
- Prefer established repository patterns and small, reviewable changes.
- Make dependencies and ownership explicit. Avoid hidden global state and duplicated policy.
- Handle expected failures deliberately and preserve useful context in warnings and errors.
- Avoid unrelated refactors unless required for a safe implementation.

## 5. Put Configuration in the Right Layer

Keep policy, tuning values, content data, and environment-specific settings out of implementation code when they must vary independently.

- Follow the project's existing configuration and serialization conventions.
- Put game-content tuning in rules, configuration, save, or map files only when that layer owns the value.
- Keep true algorithmic invariants close to the code that enforces them.
- Provide validation and safe defaults for new configuration.
- Preserve compatibility with existing saves, maps, schemas, and deployments unless the user approves a migration.

## 6. Add Diagnostics and Tests

- Add proportionate unit tests for deterministic logic, regressions, boundary cases, and failure paths.
- Add integration or end-to-end validation when behavior crosses module boundaries.
- Make complex or runtime-dependent behavior observable with useful debug logs.
- Log decisions and identifiers needed to diagnose behavior, without flooding normal logs or exposing secrets.
- Emit actionable warnings and errors and handle recoverable failures gracefully.
- State where generated logs can be found and how the user can share or inspect them.
- Run the narrowest relevant checks first, then the broader suite warranted by risk.

## 7. Record Deferred Work

Do not silently expand scope when discovering unrelated bugs, optimizations, or refactors.

1. Fix an incidental issue immediately only when it is necessary for correctness or safe completion of the requested task.
2. Otherwise add a concise actionable entry to the repository's existing work log.
3. If no work log exists and the finding is concrete and valuable, create or update `DEFERRED_WORK.md` at the repository root. Keep it separate from the implementation commit unless the user wants it included.
4. Record the location, impact, evidence, and suggested next action.
5. Tell the user whenever the work file was created or updated. Escalate urgent security, corruption, or production risks immediately.

## 8. Integrate Parallel Results

1. Review every agent diff and commit before integration.
2. Reject or revise changes that exceed scope, duplicate work, weaken behavior, or lack validation.
3. Merge or cherry-pick results into the integration feature branch in dependency order.
4. Resolve conflicts based on intended behavior, not mechanically.
5. Run combined tests after integration; isolated agent tests are not sufficient.
6. Summarize which workstream produced each integrated result and any deferred items.

## 9. Use Safe Git and PR Discipline

- Never commit or push directly to the default, base, release, or protected branch.
- Keep each independent feature on a named feature branch. Combine branches only through a reviewed integration branch when one PR is desired.
- Do not rewrite user history, discard user changes, or force-push without explicit authorization.
- Local commits are implementation steps; external publication requires user authorization.
- When publication is requested, push only the feature/integration branch and open or update a pull request. Never bypass the PR with a direct base-branch push.
- Stage files explicitly, write focused commit messages, and verify the final diff and branch target.
- Prefer a draft PR while work still needs playtesting or review.

## 10. Hand Off the Outcome

Lead with what is complete. Report:

- implemented behavior and important design choices;
- tests and checks run, including failures or omissions;
- branch, commits, and PR link when published;
- debug-log location and useful markers when diagnostics were added;
- deferred-work entries and remaining risks;
- the next user action, if any.

Do not claim completion while required integration, validation, or publication remains unfinished.
