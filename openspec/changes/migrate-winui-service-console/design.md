# Design

## Context

See `proposal.md` for motivation and `specs/staff-service-console/spec.md` for observable behavior.

The current WinUI client already implements the required midterm workflows through separate pages and presentation workspaces. `MainWindow` uses a standard `NavigationView` with eleven entries, while individual pages mix layout, event wiring, and workflow feedback. Shared resources currently cover only a small subset of the visual language. The approved concepts under `docs/ui-concepts` define a six-workspace shell and detailed 1440 by 900 reference layouts using browser-specific CSS effects.

The migration must coexist with uncommitted concept assets, preserve the direct-PostgreSQL composition path, avoid changes to domain or persistence behavior, and retain the existing Compact, Medium, and Expanded breakpoint contract.

## Goals / Non-Goals

**Goals:**

- Translate the concept system into reusable WinUI resources and compositions instead of duplicating styles per page.
- Preserve the current application-service and domain boundaries while reorganizing the presentation layer.
- Provide one navigable shell and six cohesive workspaces with reliable state and feedback behavior.
- Keep each workspace independently testable and migrate it behind stable navigation tags.
- Maintain keyboard, touch, contrast, and assistive-technology usability across breakpoints.

**Non-Goals:**

- Changing order, inventory, catalog, customer, loyalty, payment, discount, delivery, or reporting rules.
- Adding API-backed authentication or replacing the direct database adapter.
- Implementing concept-only federated search, notifications, persisted pictures, export, or shift management.
- Reproducing HTML structure, CSS, or browser SVG symbols directly in production XAML.
- Achieving pixel identity at the expense of native behavior or accessibility.

## Decisions

### 1. Keep a native NavigationView as the shell controller

`MainWindow` will retain `NavigationView` for selection semantics, keyboard support, pane adaptation, and automation behavior, but its pane header, menu item templates, resources, and content framing will be restyled to match the Corner Cafe shell. Its menu will contain only the six stable tags `counter`, `service-board`, `menu`, `stock-room`, `customers`, and `reports`.

The database connection state remains owned by the shell. The verbose connection banner becomes a compact status treatment when connected and expands into an actionable blocking state when unavailable. The default destination remains Counter after a successful connection.

Alternative considered: replace `NavigationView` with a custom grid and clickable borders. Rejected because it would recreate pane responsiveness, selection semantics, keyboard traversal, and accessibility behavior already supplied by the platform.

### 2. Translate concept tokens into layered WinUI resource dictionaries

Shared resources will be separated by responsibility:

- foundation colors, brushes, typography, spacing, corner radii, strokes, and focus visuals;
- reusable control styles for buttons, fields, tabs, pills, status stamps, list rows, and navigation items;
- reusable surface styles for paper panels, selected cards, warning rows, ticket sheets, and offset shadows;
- workspace-specific resources only when a pattern is not shared elsewhere.

Concept colors become the authoritative palette. Kalam and Patrick Hand remain embedded application fonts, with a native UI or monospace font allowed for dense numeric data. Offset shadow layers and uneven four-value corner radii reproduce the intended paper aesthetic using supported XAML primitives. Functional icons use accessible WinUI icon elements with explicit automation names; SVG symbol markup from the concept remains documentation-only.

Alternative considered: copy visual properties into each page. Rejected because token drift would make six workspace migrations inconsistent and expensive to maintain.

### 3. Recompose pages around existing presentation workspaces

The current application and domain services remain unchanged. Existing presentation workspaces continue to own loading, commands, selection, and workflow feedback. New XAML composition binds to or invokes those presentation boundaries rather than calling repositories or domain entities directly.

Where code-behind currently contains reusable presentation state, it will be moved incrementally into the corresponding workspace type with characterization tests before the visual page is replaced. Dialogs remain dialogs when they represent short create/edit tasks; persistent selection and detail inspection remain inside the workspace.

Alternative considered: rewrite every page using a new MVVM framework. Rejected because the migration does not justify a new dependency or a simultaneous state-management rewrite.

### 4. Consolidate inventory through a Stock room workspace host

Stock room becomes one primary page with section navigation for Balances, Low stock, Receipts, Suppliers, Adjustments, and Movements. Each section owns its existing loading and command lifecycle, while shared Stock room context coordinates the selected ingredient and transitions such as opening an adjustment from a balance row.

Existing standalone inventory pages are retained during migration until their corresponding section reaches parity. Reusable content is extracted into section views rather than nesting full `Page` instances. Navigation aliases may temporarily redirect legacy tags to the appropriate Stock room section for internal compatibility, but they are not exposed in the primary navigation.

Alternative considered: place the six existing pages in nested frames. Rejected because nested page navigation would fragment selection state, back behavior, focus, and feedback placement.

### 5. Keep workflow ownership explicit across Counter and Service board

Counter owns draft construction, customer/order context, discount and loyalty requests, total review, and confirmation. Service board owns post-confirmation preparation, readiness, completion, payment recording, discount approval, cancellation, table visibility, and delivery progression, matching current application behavior.

The concept's charge action in Counter is interpreted as a route to the selected confirmed order's payment context unless payment behavior is separately changed by a future spec. It will not bypass the existing confirmation and payment rules.

Alternative considered: move payment recording into Counter as part of this visual migration. Rejected because that changes workflow semantics and acceptance criteria beyond a presentation migration.

### 6. Use explicit visual states at the established breakpoints

Each workspace defines Compact, Medium, and Expanded visual states using the existing width boundaries. Expanded layouts follow the concepts closely. Medium layouts reduce simultaneous columns and move secondary detail into a selectable pane. Compact layouts stack regions or use an in-workspace list/detail transition while preserving the current task and entered data.

Scrolling is assigned to bounded content regions rather than the entire desktop shell. Primary actions remain visible or are placed in a predictable sticky/footer region. Layout transitions change presentation only and do not recreate workspace state.

Alternative considered: scale the 1440 by 900 design uniformly. Rejected because scaled controls would harm readability and touch targets while still producing poor narrow-window behavior.

### 7. Model loading and feedback as reusable presentation states

Every workspace will render the same conceptual state model: initial/loading, loaded with data, loaded empty, command in progress, recoverable validation or conflict, transient failure, and success. Feedback remains local to the affected action, while connection failure remains shell-level.

Workspace state survives layout transitions and normal navigation where the current workflow requires continuity, especially the active Counter ticket and the customer selected for the next order. Refreshes replace authoritative read data without discarding recoverable input after a failed command.

Alternative considered: use only global InfoBars. Rejected because global messages lose action context and can obscure simultaneous workspace state.

### 8. Treat concepts as reference states, not additional product promises

The checked-in HTML and PNG concepts define visual hierarchy, density, copy tone, and representative populated states. Production controls appear only when backed by existing behavior. Sample numbers, people, dates, queue counts, and statuses are not hard-coded into the client.

Concept-only features remain absent until a separate change defines their data contracts and behavior. Layouts reserve no mandatory empty chrome for omitted features; neighboring regions expand naturally.

Alternative considered: render disabled placeholders for fidelity. Rejected because disabled or inert controls imply unavailable permissions or temporary failure rather than an intentionally unimplemented capability.

### 9. Verify parity through presentation and smoke tests

Existing workspace tests remain the behavioral safety net. New tests cover six-tag navigation mapping, default navigation after connection, legacy inventory alias routing during migration, presentation-state transitions, breakpoint state selection where testable, and accessible naming for critical icon-only actions.

Manual smoke verification uses the seeded midterm database and exercises one representative workflow in every workspace at Compact and Expanded widths. Visual comparison checks hierarchy and component use against `docs/ui-concepts`, while acceptance is based on the spec rather than hard-coded screenshot pixels.

Alternative considered: screenshot-only verification. Rejected because it is brittle for fonts and rendering and cannot prove workflow parity or accessibility.

## Risks / Trade-offs

- **[Large XAML pages become difficult to maintain]** -> Split each workspace into named section views and keep shared styles in resource dictionaries.
- **[Restyling NavigationView cannot reproduce every concept detail]** -> Prioritize hierarchy, spacing, palette, and interaction semantics; introduce a custom template only for isolated visual gaps.
- **[Inventory consolidation loses page-local state]** -> Add a Stock room context and parity tests before retiring each standalone page.
- **[Code-behind refactoring changes behavior during a visual migration]** -> Characterize current commands first and move state incrementally behind existing workspace tests.
- **[Handwritten typography reduces numeric scan speed]** -> Use the approved native or monospace exception for dense totals, tables, timestamps, and identifiers.
- **[Concept density fails at smaller widths]** -> Design each workspace's three visual states before marking its expanded layout complete.
- **[Uncommitted concept files change during implementation]** -> Treat the concept snapshot present when implementation begins as the reference and report material later changes rather than silently chasing them.

## Migration Plan

1. Add the new token and component resource dictionaries while leaving current page navigation functional.
2. Replace the shell navigation with the six stable destinations and temporary internal aliases for legacy inventory routes.
3. Migrate Menu and Customers first to validate reusable list/detail, field, card, dialog, and feedback patterns.
4. Build Stock room and move Balances, Low stock, Receipts, Suppliers, Adjustments, and Movements one section at a time; retain each old route until parity is verified.
5. Migrate Service board using existing operations commands and introduce the shared order-detail presentation.
6. Migrate Counter after its menu, ticket, customer context, discount, loyalty, and confirmation states have characterization coverage.
7. Migrate Reports using reusable dashboard panels and native presentation of the existing result sets.
8. Run automated tests and seeded manual smoke checks at Compact and Expanded widths, then remove obsolete page registrations, local duplicate styles, and temporary aliases.

Rollback is page-oriented: until a workspace passes parity checks, its stable shell destination can point back to the existing page. No database rollback is required because this change introduces no schema or persisted-data migration.
