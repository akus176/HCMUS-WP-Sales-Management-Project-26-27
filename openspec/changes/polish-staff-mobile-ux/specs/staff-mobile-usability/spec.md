## Purpose

Help staff reach operational records and complete existing workflows efficiently with clear navigation, accessible controls and accurate submission feedback on Android.

## ADDED Requirements

### Requirement: Compact accessible operational layout
The application SHALL prioritize working content with compact connection information, clear page and section hierarchy, single ownership of safe-area edges, scalable text and controls with at least 48dp touch targets.

#### Scenario: Enlarged text on a small phone
- **WHEN** staff use a small Android layout at 200% system text size
- **THEN** controls and text wrap without clipping and operational records and actions remain reachable with the keyboard open where applicable

#### Scenario: Connection changes
- **WHEN** connectivity changes between checking, online, reconnecting and offline
- **THEN** the interface communicates the current state, exposes manual refresh and explains disabled writes without claiming API availability from network status alone

### Requirement: Predictable workflow navigation
The application SHALL keep the active order filter visible when its options are collapsed, preserve all existing filters, separate staff account and security-event views and make account detail entry and return explicit.

#### Scenario: Select an order status
- **WHEN** staff expand the order filter and choose an existing status
- **THEN** the result list uses that status and the collapsed control identifies the current selection

#### Scenario: Open and return from an account
- **WHEN** an administrator opens an account and returns to the account list
- **THEN** details appear in the working viewport, accessible focus identifies the new view and the previous search, selection and originating position are restored where available

### Requirement: Consistent accessible forms and feedback
The application SHALL provide persistent field labels, readable helper/error feedback, visible disabled/loading states and distinct error and success presentation while retaining existing field validation and permission checks.

#### Scenario: Account deactivation fails
- **WHEN** the service rejects deactivation
- **THEN** the application presents an accessible error near the action and does not present the failure as success or informational confirmation

### Requirement: Accurate write outcomes
The application MUST distinguish a confirmed write from subsequent data refresh and from an unknown transport outcome. It MUST retain existing idempotency, explicit-retry and online-only-write behavior.

#### Scenario: Confirmed write followed by slow refresh
- **WHEN** a write succeeds and refreshing affected data is slow or fails
- **THEN** staff receive the confirmed outcome without waiting for unrelated reads and dependent actions remain guarded until authoritative data is available

#### Scenario: Response is lost
- **WHEN** a write receives no confirmed response
- **THEN** the application describes the outcome as unconfirmed, instructs staff to refresh/reconcile and does not claim that no request was sent or replay the write automatically

### Requirement: Auditable complete-screen verification
The change SHALL inventory all user-facing screens and role-specific paths, record reproducible findings by severity, extend meaningful regressions and retain before/after native evidence with explicit remaining limitations.

#### Scenario: Audit result is submitted for review
- **WHEN** the implementation is submitted to the named reviewer
- **THEN** evidence identifies source revision, device layouts, text scale, system appearance, TalkBack and keyboard results, quality commands, remaining issues and reviewer approval status
