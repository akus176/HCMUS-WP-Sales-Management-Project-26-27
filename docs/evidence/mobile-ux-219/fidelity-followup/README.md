# Running-app fidelity and usability correction (#219)

The running-app review found two separate problems: the earlier launch served an older checkout, and the newer PR still used heavy controls and scrolling action placement that did not reproduce the concepts well. This follow-up corrects the application rather than treating the concept pack or a passing behavioral suite as visual acceptance.

## Findings and changes

| Observed problem | Resulting behavior |
| --- | --- |
| Ticket selection required scrolling to the end of the queue to open it. | At normal scale, Open and Refresh remain in a reserved bottom region while records scroll. |
| Confirmation actions could fall below the working content. | Detail uses the concept's bottom confirmation band. Above 150% text or below 560dp height, actions reflow into the page so enlarged controls cannot cover records. |
| Thick return buttons and control borders competed with records. | Return navigation is a plain labeled action; thin control outlines do not change size when pressed. |
| Branding changed between modules. | One compact Corner Cafe header is shared throughout the app; brand/status stack at enlarged text. |
| Home was a wall of identical filled buttons. | Permission-derived workspaces use ruled rows with descriptive secondary text and visible press/focus feedback. |
| Inventory used large blue boxes and repeated long action text. | Ingredient rows are open and ruled. Concise visible actions retain full ingredient-specific accessible names. |
| The first screen said the tools were being prepared and added an extra sign-in step. | The signed-out root now shows working sign-in directly. Session cleanup and protected routes remain unchanged. |
| Android Back could abandon a local ticket view. | Back dismisses an open confirmation without a write; the next Back restores the selected queue and filter. Pending submissions remain guarded. |

The reference targets remain [Queue ledger](../../../ui-concepts/mobile-2026-11-22/01-queue-ledger.png) and [Ticket focus](../../../ui-concepts/mobile-2026-11-22/03-ticket-focus.png). The alternative paper stack is not another implemented mode. Live order IDs/items replace the art's synthetic examples.

## Captures and checks

[Open the concept comparison](comparison.html). The viewer clips the measured Android status/navigation bars with CSS; source PNGs remain unedited.

- [Fresh qualitative inventory baseline](before-inventory.png): the previously running UI, at its original viewport. Its JavaScript revision was not revalidated, so it is not a matched-state pixel baseline.
- Normal scale: [sign-in](signin-normal.png), [home](home-normal.png), [inventory](inventory-normal.png), [queue](queue-normal.png), [selected queue](queue-selected.png), [detail](detail-normal.png), [ready confirmation](confirmation-normal.png).
- 200% text: [home](home-large.png), [queue top](queue-large-top.png), [reachable queue action](queue-large-actions.png), [detail](detail-large-top.png), [both confirmation choices visible](confirmation-large.png).
- [Cancellation/API verification](cancellation-verification.json): Keep preparing and Android Back dismiss confirmation; the server detail remains unchanged and preparing; Back restores the selected queue.
- [Large-text verification](large-text-verification.json): records and actions remain reachable; both confirmation choices can be visible together without a fixed overlay.
- [Capture and source provenance](manifest.json).

Device: Android Emulator, API 36; Expo Go 58.0.5. Reference captures are 1170×2676px at 480dpi, giving a 390×844dp app-owned viewport after 24dp top/bottom system bars. The circular Tools control belongs to Expo Go and remains visible in the unedited captures.

The initial enlarged-text automation attempt incorrectly assumed the queue would remain the entry view after Expo Go handled the system configuration change. It stopped on Home. Its diagnostic is retained under [attempts](attempts/profile-restart-on-home.png); it is not passing queue evidence. The corrected run enters the workspace after configuring the profile.

## Validation

- Mobile quality: **28 suites / 344 tests pass**, plus **7 forwarding-harness checks**, TypeScript, ESLint and formatting. New regressions cover direct entry, normal/enlarged action placement, and Android Back cancellation/queue restoration.
- Contract regeneration and TypeScript: pass with zero generated drift.
- Android Hermes export: pass.
- API Release build: pass, zero warnings/errors.
- OpenSpec strict validation and whitespace checks: pass.
- Native read/cancel/navigation checks: direct sign-in, actual API authentication, home/inventory navigation, selected-ticket opening, confirmation cancellation, Android Back, and 100%/200% text. No existing order was advanced by these checks.

Local JavaScript checks used Node 22.14.0 with TypeScript stripping enabled; the repository's pinned Node 26.5.0 CI gate remains distinct. Native follow-up uses ADB-driven interactions in Expo Go, not a new standalone-development-build Maestro full-suite result. Earlier full-suite reports belong to their recorded revisions. Full item 2.2 revalidation and named reviewer approval remain open; this is not an accessibility compliance or release-acceptance claim.
