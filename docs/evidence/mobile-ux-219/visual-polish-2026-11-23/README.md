# Shared mobile polish after the teammate update (#219)

Pulled `3bd2ac6`, retaining the teammate's keyboard/fixture-driver changes and owner acceptance. This pass continues the existing mobile paper/ink direction with presentation changes and bounded order rendering.

| Problem | Change |
| --- | --- |
| Most workspaces still stacked the return control above the page heading. | Reuse Preparation's compact, permission-checked header across all six workspaces. Enlarged text retains the shared stacked layout. |
| Order, table, approval and account records used heavy outlined boxes. | Use the same open ruled records as Inventory and Preparation, including delivery and security-event rows. |
| Feature-specific input styles overrode shared fields with thicker borders and inconsistent padding. | One shared 48dp-minimum input style owns the thin outline and bundled body font. |
| Every record-opening action competed visually with business confirmations. | Use concise, underlined record links with unchanged full accessible names and targets. Business actions and their confirmation/permission/connectivity guards remain intact. |
| Counter and manager search mounted the entire order result. The local API returned 992 orders. | Render ten matching rows initially; Show more orders reveals subsequent batches and focuses the first new record. Search/status filtering still covers the complete result and resets the visible batch. Expanded results and detail return state stay mounted. |

## Fresh visual samples

| Screen | Before, `3bd2ac6` | After, changed source |
| --- | --- | --- |
| Counter tables | [Before](before-tables.png) | [After](after-tables.png) |

The Tables comparison uses unedited emulator captures at the same display profile and normal text, with matching visible records and selected view; clock and Expo Go's Tools overlay are infrastructure. The compact Counter header removes one 48dp return row plus its spacing. Record rows recover the previous horizontal card padding for working content.

The [final Orders view](after-orders.png) is an additional visual sample. Its attempted baseline showed loading and was excluded from comparison. The native visual review is limited to Orders and Tables. Other workspaces reuse the same header, field and row presentation; no full native, keyboard, enlarged-text or TalkBack pass is claimed. [Source hashes, device profile and bounded validation](manifest.json) identify the exact scope.

## Validation and retained acceptance

- TypeScript, ESLint, formatting, strict OpenSpec validation and whitespace checks pass.
- The focused order-operation suite passes **26 tests**, including two new Cashier/Manager cases for bounded rendering, Show more, complete-result search, complete-result filtering and no business writes. Existing payment, cancellation, assignment, conflict and permission cases remain in that suite.
- The 992-order diagnostic was a single local read, not a performance benchmark. This change bounds initially mounted records; it does not claim a measured frame-rate improvement or server-side pagination.
- No complete-suite rerun is claimed. The [owner-accepted verification](../current/README.md) and completed issue item 2.2 are preserved. Named reviewer approval under 3.1 remains outstanding.

The emulator remains available with the preview session. No existing business record was changed for these captures.
