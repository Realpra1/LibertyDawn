---
name: read-cnc-task
description: Read the Liberty Dawn autonomous CNC task sheet and select exactly one next eligible task into a small isolated task packet for a speccing agent. Use for one fresh task-selection pass in a coordinated autonomous development round while keeping the coordinator and implementation workers away from the full task sheet.
---

# Read One CNC Task

Use Terra 5.6 medium. Do not modify Git, product code, task-sheet state, or
coordinator state; write only the requested task-packet file.

1. Read applicable `AGENTS.md`, `AUTONOMOUS-CNC-TASKS.md`, the supplied exclusion
   list, and only the state/report/PR metadata needed to interpret eligibility.
2. Select the first eligible task not excluded by the coordinator. Respect pinned,
   blocked, prerequisite, user-question, completed, first-iteration, and active-PR
   rules written in the task sheet.
3. Return exactly one task. Do not summarize the remainder of the sheet.
4. Include closely related task IDs, prerequisites, likely overlapping active PRs,
   and any warning that another worker's commits may materially influence this
   task. Do not reject ordinary overlap; the speccer and worker will monitor it.
5. Copy user-authored requirements literally before adding interpretation.
6. Write the packet to the requested path with this shape:

```markdown
# Task packet
- Task ID:
- Title:
- Status at selection:
- Required base/prerequisites:
- Related active tasks or PRs:
- Cross-worker concern:

## Authoritative task text

## Relevant linked notes

## Selection rationale
```

Return only the selected ID, title, packet path, and any hard blocker to the
coordinator. Never edit the task sheet or coordinator state.
