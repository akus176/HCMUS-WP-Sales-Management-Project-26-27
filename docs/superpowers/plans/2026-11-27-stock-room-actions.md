# Stock room actions (#230)

## Goal

Connect the existing Windows ingredient threshold dialog and ingredient/supplier lifecycle actions to the authoritative inventory API, while keeping the in-process midterm path functional.

## Implementation

1. Add focused tests for the Stock room action policy: permissions, valid state transitions, and stale state detection. Run them red, then implement the shared policy.
2. Add the ingredient Edit and Activate/Deactivate controls, confirmation, server state checks, feedback, and reload. Preserve balance edits through adjustments and receipts.
3. Add the supplier Activate/Deactivate action with the same state checks. Preserve supplier Edit. Run focused Ubuntu tests, Windows Release build, and relevant CI; capture native evidence if available.

## Acceptance

Threshold changes refresh balance and low-stock status. State actions require `inventory.manage`, confirm the target and consequence, and refresh the list. Failed or stale mutations report the actual result. Three focused commits and one PR link #230.
