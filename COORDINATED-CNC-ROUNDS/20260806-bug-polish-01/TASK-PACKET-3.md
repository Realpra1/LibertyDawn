# Task packet
- Task ID: CNC-43
- Title: MCV crush flavor
- Status at selection: pending
- Required base/prerequisites: Use the coordinated round's recorded common base, `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf` (`agent/cnc38-early-viki-infantry-rush`). No explicit prerequisite is stated in the task sheet.
- Related active tasks or PRs: CNC-45 (Economy troop production/use) later adds bounded Mammoth crush orders, but is pending and is not an active PR. No active branch or PR matching CNC-43/MCV crush was found at selection.
- Cross-worker concern: This task is config-only and must not introduce MCV AI behavior. Preserve the Mammoth's existing crush capability values rather than independently tuning them, so later Mammoth crush work (CNC-45) remains behavior-scoped.

## Authoritative task text

**MCV crush flavor.** Config only: give MCVs the Mammoth tank's crush capabilities. Do not change AI behavior or unrelated balance.

## Relevant linked notes

- CNC-45: **Economy troop production/use.** Make economy armies primarily Mammoth tanks with riflemen and the artillery squad; use medium tanks, not Mammoths, for harassment. Occasionally give Mammoths bounded crush orders and make their approach distance account for the shortest-ranged usable weapon so cannon damage is not wasted. Preserve ordinary behavior and CPU performance.

## Relevant deferred constraints

No deferred-work constraint specifically concerns MCV crush behavior.

## Selection rationale

CNC-43 is the first eligible pending task in task-sheet order after the coordinator-excluded/claimed CNC-39 and CNC-39A. It is a focused bug/polish content correction with no stated prerequisite, user-question gate, or pinned-final restriction. CNC-26C is permanently pinned final and ineligible; completed and first-iteration tasks are not selected.
