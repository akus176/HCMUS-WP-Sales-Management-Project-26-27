# Design

## Context

See `proposal.md` for motivation and `specs/staff-service-console/spec.md` for observable behavior.

The current WinUI client already implements the required midterm workflows through separate pages and presentation workspaces. `MainWindow` uses a standard `NavigationView` with eleven entries, while individual pages mix layout, event wiring, and workflow feedback. Shared resources currently cover only a small subset of the visual language. The approved concepts under `docs/ui-concepts` define a six-workspace shell and detailed 1440 by 900 reference layouts using browser-specific CSS effects.

The updated concept assets committed with this change are the authoritative visual specification. The migration must preserve the direct-PostgreSQL composition path, avoid changes to domain or persistence behavior, and retain the existing Compact, Medium, and Expanded breakpoint contract.

### Approved foundation and shell target revision

During part 1 implementation, the user approved revising the artboards to use 48-pixel primary controls and navigation rows and omit unsupported shell controls. The revised HTML/PNG artboards are the implementation target. Global search, notifications, open-ticket chrome, operator avatars, and shift-management chrome are omitted; a workspace breadcrumb and the current date remain. Native caption buttons retain their operating-system behavior. The Stock room shell temporarily offers a section selector for the six existing inventory routes so consolidation does not remove workflows before the section views are migrated. Individual workspace interiors are migrated in later parts; part 1 visual review covers the common shell and shared component gallery.

### Approved Menu, Customers, and Reports capability revision

On 2026-10-29, the user approved the targeted #117 concept changes: use one fixed decorative drink icon and a text-only drink editor; omit seasonal flags, catalog performance/audit labels, and customer lifetime totals; replace report comparisons and peak-hour metrics with current-period totals and order channels; replace Export with Refresh. Existing editing, recipes, compatibility, addresses, loyalty, next-order selection, payment filters, and report date filters remain required. The revised HTML and regenerated PNGs are the reference for these capability changes. This approval does not accept unrelated native layout, typography, control, or selection differences; exact matched-state visual verification remains required.

### Approved Menu, Customers, Reports, and editor native compositions

In a subsequent 2026-10-29 review, the user explicitly answered “Approve these native compositions” after seeing a side-by-side comparison of the four native compositions at implementation commit `9e619622f30366f3f258716a9e004a602879e46d` with reference commit `0ec5463`. The exact approved differences and immutable capture revision are recorded in `docs/ui-concepts/workspaces-117-native-revision.md`. That supplement governs the listed native controls/header spacing, Menu action/detail arrangement, Customers form/history density, Reports flyouts/rows, and editor field/footer arrangement. It also approves real seeded fixture state and native editor window framing instead of an equivalent-fixture 1440 by 900 editor comparison. This targeted revision satisfies the visual acceptance conflict for #117; it does not change other workspaces, supported workflows, accessibility, accounting, or future visual review requirements.

## Goals / Non-Goals

**Goals:**

- Translate the concept system into reusable WinUI resources and compositions instead of duplicating styles per page.
- Match the concept art exactly at 1440 by 900, including typography, palette, imagery, patterns, decorative details, alignment, density, selection, and warning states.
- Preserve the current application-service and domain boundaries while reorganizing the presentation layer.
- Provide one navigable shell and six cohesive workspaces with reliable state and feedback behavior.
- Keep each workspace independently testable and migrate it behind stable navigation tags.
- Maintain keyboard, touch, contrast, and assistive-technology usability across breakpoints.

**Non-Goals:**

- Changing order, inventory, catalog, customer, loyalty, payment, discount, delivery, or reporting rules.
- Adding API-backed authentication or replacing the direct database adapter.
- Implementing concept-only federated search, notifications, persisted pictures, export, or shift management.
- Reproducing HTML structure, CSS, or browser SVG symbols directly in production XAML.
- Hard-coding sample customers, dates, amounts, or counts solely to imitate a screenshot.

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

The authoritative resources are `docs/ui-concepts/assets/app.css`, `assets/refinements.css`, the fonts, photos, ornaments, and patterns they reference, and the six screen HTML files. Regenerated screen PNGs and `menu-picture-editor.png` provide matched review captures. `docs/luxury-theme.md` supplies supporting guidance; the screen-specific treatment takes precedence where it is more specific.

Use EB Garamond for display headings, prices, and display numerals, and Instrument Sans for controls, labels, and reading text, with the exact weights, sizes, tracking, and line heights shown in the concepts. Replace the obsolete Kalam / Patrick Hand presentation resources. Preserve the cream (#f3eee4), taupe (#e4dbcc), charcoal (#262321), and brass (#b69355) palette and the explicit small-text accent colors. Do not introduce a native or monospace typography substitution that changes the reference appearance.

Reproduce square geometry, fine and double rules, inset photo frames, café illustrations, lattice/hatch patterns, and spacing using native XAML templates or packaged assets. Service board order cards use pale yellow Confirmed, peach Preparing, and sage Ready paper surfaces, translucent tape, folded corners, subtle paper shadows, and aligned card baselines. Retain the selected-note outline and left accent. This shadow treatment is specific to the sticky notes; other surfaces retain their concept styling. Decorative layers must not intercept input or appear in the accessibility tree. Functional icons must match the concept shapes and stroke treatment while exposing meaningful automation names.

Alternative considered: copy visual properties into each page. Rejected because token drift would make six workspace migrations inconsistent and expensive to maintain.

### 3. Recompose pages around existing presentation workspaces

The current application and domain services remain unchanged. Existing presentation workspaces continue to own loading, commands, selection, and workflow feedback. New XAML composition binds to or invokes those presentation boundaries rather than calling repositories or domain entities directly.

Where code-behind currently contains reusable presentation state, it will be moved incrementally into the corresponding workspace type with characterization tests before the visual page is replaced. Dialogs remain dialogs when they represent short create/edit tasks; persistent selection and detail inspection remain inside the workspace.

Alternative considered: rewrite every page using a new MVVM framework. Rejected because the migration does not justify a new dependency or a simultaneous state-management rewrite.

### 4. Consolidate inventory through a Stock room workspace host

Stock room exposes four primary tabs: Inventory, Receipts, Suppliers, and History. The user approved this refinement on 2026-10-28 to remove overlapping navigation and improve visual harmony. Low stock is an Inventory filter; adjustments are an intentional action on the selected ingredient, rather than separate tabs. All six existing inventory functions remain available, and legacy low-stock and adjustment routes enter the corresponding filter or editor.

Inventory opens with readable balances and read-only ingredient details. An explicit Adjust stock action opens the quantity, optional reason, operator, and projected-result editor. Drafts belong to their ingredient, survive navigation and resize, and are discarded only by that editor's Cancel action or a successful submission. Invalid quantity, missing operator, and out-of-range result states explain why submission is unavailable. A blank reason records a manual stock adjustment. History has an explicit ingredient selector and meaningful empty feedback. Header actions follow the active tab; repeated receipt/supplier creation buttons and disabled posting actions on completed receipts are omitted. Stock room's revised HTML artboards and styles are the visual reference for these authorized changes. The subsequent native card refinement uses a flat cream record panel, a charcoal balance block, and a brass lattice border; its lower edge follows the table's patterned rule and its adjustment actions remain visible below the scrolling form.

Existing standalone inventory pages are retained during migration until their corresponding section reaches parity. Reusable content is extracted into section views rather than nesting full `Page` instances. Navigation aliases may temporarily redirect legacy tags to the appropriate Stock room section for internal compatibility, but they are not exposed in the primary navigation.

Alternative considered: place the six existing pages in nested frames. Rejected because nested page navigation would fragment selection state, back behavior, focus, and feedback placement.

### 5. Keep workflow ownership explicit across Counter and Service board

Counter owns draft construction, customer/order context, discount and loyalty requests, total review, and confirmation. Service board owns post-confirmation preparation, readiness, completion, payment recording, discount approval, cancellation, table visibility, and delivery progression, matching current application behavior.

The concept's charge action in Counter is interpreted as a route to the selected confirmed order's payment context unless payment behavior is separately changed by a future spec. It will not bypass the existing confirmation and payment rules.

Alternative considered: move payment recording into Counter as part of this visual migration. Rejected because that changes workflow semantics and acceptance criteria beyond a presentation migration.

### 6. Use explicit visual states at the established breakpoints

Each workspace defines Compact, Medium, and Expanded visual states using the existing width boundaries. At 1440 by 900, Expanded layouts must match the concepts exactly. At other widths, retain the same visual tokens, components, content hierarchy, and decorative language while reflowing regions. Medium layouts reduce simultaneous columns and move secondary detail into a selectable pane. Compact layouts stack regions or use an in-workspace list/detail transition while preserving the current task and entered data.

Scrolling is assigned to bounded content regions rather than the entire desktop shell. Primary actions remain visible or are placed in a predictable sticky/footer region. Layout transitions change presentation only and do not recreate workspace state.

Native keyboard behavior and accessibility remain required. If a reference detail conflicts with the specified touch targets, supported behavior, or native rendering constraints, record the exact conflict and obtain approval for a targeted concept/spec revision before claiming completion. Do not silently replace the reference with an approximate native default.

Alternative considered: scale the 1440 by 900 design uniformly. Rejected because scaled controls would harm readability and touch targets while still producing poor narrow-window behavior.

### 7. Model loading and feedback as reusable presentation states

Every workspace will render the same conceptual state model: initial/loading, loaded with data, loaded empty, command in progress, recoverable validation or conflict, transient failure, and success. Feedback remains local to the affected action, while connection failure remains shell-level.

Workspace state survives layout transitions and normal navigation where the current workflow requires continuity, especially the active Counter ticket and the customer selected for the next order. Refreshes replace authoritative read data without discarding recoverable input after a failed command.

Alternative considered: use only global InfoBars. Rejected because global messages lose action context and can obscure simultaneous workspace state.

### 8. Treat the updated concepts as the exact visual contract

The six checked-in screen HTML files, shared styles, bundled assets, and regenerated PNGs define the exact appearance of the shell and workspaces. Layout, component dimensions, visual copy, typography, image crop, icon treatment, patterns, tape/folds, borders, and selection states must match. A merely similar palette or hierarchy is insufficient. Browser-specific implementation details may be translated into WinUI techniques only when their visible result is preserved.

Sample numbers, people, dates, queue counts, and statuses come from live application state. Use equivalent seeded data for visual verification instead of hard-coding sample data. The existing supported-affordance and payment-boundary requirements still apply. Any difference they require from the visible concept, including omission of an unsupported control or a different action label, must be documented and approved in the concept/spec before that screen can be accepted. An unsupported capability must not be simulated to satisfy visual comparison.

Alternative considered: render disabled placeholders for fidelity. Rejected because disabled or inert controls imply unavailable permissions or temporary failure rather than an intentionally unimplemented capability.

### 9. Verify parity through presentation and smoke tests

Existing workspace tests remain the behavioral safety net. New tests cover six-tag navigation mapping, default navigation after connection, legacy inventory alias routing during migration, presentation-state transitions, breakpoint state selection where testable, and accessible naming for critical icon-only actions.

Manual smoke verification uses the seeded midterm database and exercises one representative workflow in every workspace at Compact and Expanded widths. Capture each workspace and the Menu editor at a 1440 by 900 content viewport with equivalent data, selected items, and UI state; record the concept commit used. Compare each capture directly with its concept using side-by-side views or overlays. Check shell and column geometry, alignment and spacing, fonts and line breaks, palette, photos and crops, icons, patterns, ornaments, borders, paper/tape/fold/shadow treatments, and selected/warning states. Resolve all visible differences before checking off the corresponding implementation task. Behavioral tests alone do not satisfy this visual gate.

Alternative considered: hierarchy-only visual review or screenshot-only testing. Rejected because hierarchy-only review permits visible drift, while screenshots alone cannot prove workflow parity or accessibility. Both exact visual verification and behavioral checks are required.

## Risks / Trade-offs

- **[Large XAML pages become difficult to maintain]** -> Split each workspace into named section views and keep shared styles in resource dictionaries.
- **[Default NavigationView styling differs from the concepts]** -> Use custom templates and shared resources to reproduce the reference appearance while retaining navigation semantics; unresolved differences block visual acceptance.
- **[Inventory consolidation loses page-local state]** -> Add a Stock room context and parity tests before retiring each standalone page.
- **[Code-behind refactoring changes behavior during a visual migration]** -> Characterize current commands first and move state incrementally behind existing workspace tests.
- **[Font substitution changes alignment and number widths]** -> Package the exact EB Garamond and Instrument Sans font files and preserve the concept's tabular numeral settings in dense data regions.
- **[Concept density fails at smaller widths]** -> Design each workspace's three visual states before marking its expanded layout complete.
- **[Concept updates invalidate previous verification]** -> Record the reference commit, reset affected completion claims, and repeat matched-state screenshot review against the latest approved concepts before rechecking tasks.

## Migration Plan

1. Add the new token and component resource dictionaries while leaving current page navigation functional.
2. Replace the shell navigation with the six stable destinations and temporary internal aliases for legacy inventory routes.
3. Migrate Menu and Customers first to validate reusable list/detail, field, card, dialog, and feedback patterns.
4. Build Stock room and move Balances, Low stock, Receipts, Suppliers, Adjustments, and Movements one section at a time; retain each old route until parity is verified.
5. Migrate Service board using existing operations commands and introduce the shared order-detail presentation.
6. Migrate Counter after its menu, ticket, customer context, discount, loyalty, and confirmation states have characterization coverage.
7. Migrate Reports using reusable dashboard panels and native presentation of the existing result sets.
8. Run automated tests, seeded manual smoke checks at Compact and Expanded widths, and exact matched-state screenshot comparisons for all six workspaces and the Menu editor at 1440 by 900, then remove obsolete page registrations, local duplicate styles, and temporary aliases.

Rollback is page-oriented: until a workspace passes parity checks, its stable shell destination can point back to the existing page. No database rollback is required because this change introduces no schema or persisted-data migration.
