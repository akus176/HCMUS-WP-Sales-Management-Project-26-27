# Current UI screenshots

Captured from the current WinUI application on 2026-11-01 after applying all EF migrations to a clean disposable PostgreSQL database. The database used the tracked `midterm-seed.sql` (5 categories, 20 menu items, 12 customers, 24 orders, and 9 ingredients) followed by the documented `ui-parity-seed.sql` supplement (3 add-ons and the required payment/queue states). No business command was submitted during capture.

Capture provenance: [manifest](current-capture-manifest-2026-11-01.json) · [native verification](current-parity-verification-2026-11-01.json). The capture used implementation commit `f64939b6cebe80ef4533357e2eade46736455ce1`; the working tree was intentionally dirty because the parity-fixture and documentation fixes were being audited before commit.

## Pages

| Page | Wide | Narrow |
| --- | --- | --- |
| Counter | [1440 × 900](native-counter-1440.png) | [600 × 900](native-counter-600.png) |
| Service Board | [1440 × 900](native-service-board-1440.png) | [600 × 900](native-service-board-600.png) |
| Menu | [1440 × 900](native-menu-1440.png) | [600 × 900](native-menu-600.png) |
| Stock Room | [1440 × 900](native-stock-room-1440.png) | [600 × 900](native-stock-room-600.png) |
| Customers | [1440 × 900](native-customers-1440.png) | [600 × 900](native-customers-600.png) |
| Reports | [1440 × 900](native-reports-1440.png) | [600 × 900](native-reports-600.png) |

## Forms and detail views

- [Counter customization](native-counter-customization.png)
- [Counter delivery](native-counter-delivery.png)
- [Counter dine in](native-counter-dine_in.png)
- [Counter populated](native-counter-populated.png)
- [Counter takeaway](native-counter-takeaway.png)
- [Counter ticket](native-counter-ticket.png)
- [Customers delivery](native-customers-delivery.png)
- [Customers editor](native-customers-editor.png)
- [Menu addons](native-menu-addons.png)
- [Menu categories](native-menu-categories.png)
- [Menu editor](native-menu-editor.png)
- [Reports breakdowns](native-reports-breakdowns.png)
- [Reports daily details](native-reports-daily-details.png)
- [Reports empty](native-reports-empty.png)
- [Reports filters](native-reports-filters.png)
- [Reports period validation](native-reports-period-validation.png)
- [Service board delivery](native-service-board-delivery.png)
- [Service board payment](native-service-board-payment.png)
- [Stock room adjustment](native-stock-room-adjustment.png)

## Refreshing these images

Build the current client in Release. Use `--verify-shell --verify-parity --verify-exit` to capture all six pages, and `--verify-shell --verify-counter` / `--verify-shell --verify-stock-room` for the additional service forms and adjustment editor. Set `COFFEESHOP_UI_EVIDENCE` to a fresh folder under `artifacts/ui-current-capture/`, then copy only the reviewed PNGs here. Keep the seed and capture manifest together so displayed values remain reproducible.

Verification uses reads and unsaved local form drafts; it does not submit business changes. Generated reports, logs, and comparison pages stay outside this image folder. Earlier evidence remains available in Git history.
