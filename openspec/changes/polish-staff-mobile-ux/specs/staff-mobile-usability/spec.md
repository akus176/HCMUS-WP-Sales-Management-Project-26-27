## Purpose

Help staff reach operational records and complete existing workflows efficiently with clear navigation, accessible controls and accurate submission feedback on Android.

## ADDED Requirements

### Requirement: Mobile concept visual contract
The application SHALL align Preparation with the ledger queue and focused ticket concepts in `docs/ui-concepts/mobile-2026-11-22`, retaining the mobile warm-paper palette and bundled Kalam/Patrick Hand fonts. Explicit design rules SHALL resolve raster ambiguities.

#### Scenario: Scope the alternative queue art
- **WHEN** implementation scope is derived from the concept pack
- **THEN** `01-queue-ledger.png` governs the queue and `03-ticket-focus.png` governs detail/ready confirmation
- **AND** `02-paper-ticket-stack.png` remains an alternative reference without requiring another queue mode or mode switch

#### Scenario: Compare the implemented reference state
- **WHEN** the queue or ticket detail is captured at a 390×844dp content viewport and normal text scale with equivalent seeded data
- **THEN** the capture is compared side by side with the corresponding concept for header hierarchy, ruled rows, typography, palette, spacing, selected state, add-on emphasis and confirmation placement
- **AND** comparison excludes native OS chrome without distorting the image, records source/reference revisions and resolves material deviations before acceptance

### Requirement: Ledger preparation queue
The application SHALL present existing preparation tickets as open ruled rows with live order number, textual status, item count, table or channel context and an item preview. It SHALL retain All active, Confirmed and Preparing filters, refresh, pagination and a labeled Staff home return. It SHALL expose an accessible selected-row state and a single primary action that opens the selected ticket without mutating it.

#### Scenario: Select and open a ticket
- **WHEN** a permitted staff member selects an active ticket
- **THEN** the selection and primary action identify that ticket and opening it displays its authoritative detail without starting preparation or marking it ready
- **AND** returning to the queue preserves filter, page and selection where the record remains valid

#### Scenario: Selected ticket leaves the visible results
- **WHEN** refresh, filtering or pagination removes the selected ticket from the current results
- **THEN** the obsolete selection is cleared and opening is disabled until another valid ticket is selected

### Requirement: Focused ticket detail and confirmation
The application SHALL present quantity, item name, size and add-ons in readable rows with highlighted add-on text, current order state, labeled Preparation return and refresh. It SHALL retain both start-preparing and ready confirmation flows, with an inline ready-confirmation area offering Confirm ready and Keep preparing. It MUST retain server-confirmed, permission-checked, online-only writes.

#### Scenario: Dismiss ready confirmation
- **WHEN** a preparing ticket's ready confirmation is open and staff choose Keep preparing
- **THEN** confirmation closes without sending a mutation and the server ticket remains preparing

#### Scenario: Confirm the whole ticket ready
- **WHEN** staff choose Confirm ready on a valid preparing ticket
- **THEN** the application submits the existing whole-ticket transition, prevents duplicate submission while busy and reports success only after the service confirms it
- **AND** the interface does not introduce per-item completion, undo or automatic replay

#### Scenario: Begin a confirmed ticket
- **WHEN** a confirmed ticket is opened
- **THEN** Start preparing and Confirm start remain available through the existing confirmation flow before the service changes the state to preparing

### Requirement: Compact accessible operational layout
The application SHALL prioritize working content with compact connection information, clear page and section hierarchy, single ownership of safe-area edges, scalable text and controls with at least 48dp touch targets.

#### Scenario: Enlarged text on a small phone
- **WHEN** staff use a small Android layout at 200% system text size
- **THEN** controls and text wrap without clipping and operational records and actions remain reachable with the keyboard open where applicable

#### Scenario: Connection changes
- **WHEN** connectivity changes between checking, online, reconnecting and offline
- **THEN** the interface communicates the current state, exposes manual refresh and explains disabled writes without claiming API availability from network status alone

### Requirement: Reachable preparation actions
The application SHALL keep preparation actions in a reserved bottom region outside scrolling records at normal phone sizes. Above 150% text or below 560dp viewport height, it SHALL reflow those actions into the page so they remain reachable without covering content.

#### Scenario: Act on a selected ticket in a long queue
- **WHEN** staff select a ticket and scroll the normal-size queue
- **THEN** its Open action remains available without scrolling to the end of the records

#### Scenario: Use Android Back while preparing
- **WHEN** staff press Android Back with a preparation confirmation open
- **THEN** only the confirmation is dismissed and no mutation is sent
- **AND** a subsequent Back from detail returns to the retained queue selection, filter and page while no submission is pending

### Requirement: Coherent entry and workspace presentation
The application SHALL open working sign-in directly at the signed-out root, use a consistent compact branded header, and present permission-derived workspace and inventory navigation with the same paper/ink hierarchy. Concise visible action text SHALL retain record-specific accessible names.

#### Scenario: Enter the app and choose a workspace
- **WHEN** staff open the app signed out and then authenticate
- **THEN** no unfinished starter page precedes sign-in and the home lists only workspaces allowed by current permissions
- **AND** existing route guards, session cleanup and operational workflows remain effective

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
The change SHALL reuse the completed user-facing screen/role inventory and reproducible findings, extend meaningful regressions for the concept alignment and retain fresh before/after native evidence with explicit remaining limitations. Previously recorded results SHALL remain historical and SHALL NOT satisfy the new concept-alignment verification gate.

#### Scenario: Audit result is submitted for review
- **WHEN** the implementation is submitted to the named reviewer
- **THEN** evidence identifies source revision, device layouts, text scale, system appearance, TalkBack and keyboard results, quality commands, remaining issues and reviewer approval status

### Requirement: Maestro Android E2E verification
The change SHALL extend the existing Maestro CLI Android YAML flows and npm runners rather than introduce another E2E framework. Verification SHALL use a development build, a working Android device or emulator and a disposable shared-API fixture, and SHALL record tool version, device/API level, source revision, fresh JUnit reports, sanitized screenshot/failure artifacts and independent API outcome checks.

#### Scenario: Run the concept workflow
- **WHEN** the concept-aligned implementation is verified
- **THEN** Maestro exercises queue entry and all three filters, selected-ticket opening, size/add-on visibility, start confirmation, canceled ready confirmation, confirmed ready progression and return navigation
- **AND** the existing workflow harness verifies offline/stale/forbidden outcomes, canceled/offline no-write behavior and no automatic replay against independent API state

#### Scenario: Required native checks are incomplete
- **WHEN** a required flow fails, is skipped, lacks a fresh report or cannot run because the device or API is unavailable
- **THEN** the limitation and artifacts are recorded and the verification task remains incomplete; stale reports or partial reruns are not combined into a claimed passing suite

#### Scenario: Complete visual and accessibility acceptance
- **WHEN** automated native behavior passes
- **THEN** matched-state concept captures and small-phone/tablet checks at normal/200% text, light/dark system appearance with the light-only app, keyboard reachability, safe areas, measured touch targets and manual TalkBack traversal are reviewed separately
- **AND** a Maestro pass alone does not establish visual acceptance or named-reviewer approval
