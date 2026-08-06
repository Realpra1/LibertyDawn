# Task packet
- Task ID: CNC-39A
- Title: Engineer/commando target coordination
- Status at selection: pending
- Required base/prerequisites: Use the coordinated round's recorded common base. No explicit prerequisite is stated in the task sheet; coordinate with CNC-39, which is claimed in this round and changes the adjacent engineer capture-selection logic.
- Related active tasks or PRs: CNC-39 (Engineer correction) is claimed for this round in `TASK-PACKET-1.md`. No active branch or PR matching CNC-39A/engineer/commando target coordination was found at selection.
- Cross-worker concern: CNC-39 and CNC-39A share engineer target eligibility, reassessment, pairing, and reservation semantics. Avoid overwriting the CNC-39 worker's threshold/reassessment changes; integrate against its task PR when available and keep CNC-39A focused on shared engineer/commando reservations and queued-C4 ownership revalidation.

## Authoritative task text

**Engineer/commando target coordination.** Prevent engineers and commandos from entering or acting on the same building concurrently. Share deterministic target reservations between capture and demolition assignments, and revalidate ownership/relationship when a queued C4 order executes so a building captured in the meantime can never be detonated after becoming friendly. Test both simultaneous selection and capture-during-commando-travel races.

## Relevant linked notes

- CNC-39: **Engineer correction.** A lone engineer may capture an enemy building below 80% health (rather than the old 50% threshold). Revalidate five-second/value-distance reassessment, valuable husks, coordinated healthy-building captures, and pairing without stale long routes.
- CNC-50: **Late-game engineer stall recovery.** Coordinate with CNC-39/CNC-39A rather than duplicating or regressing their value, pairing, and reservation rules.

## Relevant deferred constraints

- Healthy-building engineer pairs previously stayed committed while the target remained healthy, which could suppress useful reassessment. The engineer-correction work changes the capture threshold to 80% and should revalidate pair retargeting.

## Selection rationale

CNC-39A is the first eligible pending bug/polish task in task-sheet order after the coordinator-excluded CNC-39. It has no stated prerequisite, user-question gate, or pinned-final restriction. Its direct relationship to the concurrently claimed CNC-39 is documented for coordinated integration.
