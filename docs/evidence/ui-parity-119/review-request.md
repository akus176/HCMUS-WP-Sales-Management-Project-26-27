# Proposed native reference revision for #119 — not approved

The current UI is functionally verified but visibly differs from the original artboards and, in some details, the bounded #117 native approval. Exact visual acceptance remains pending. The [comparison deck](comparison.html) presents the concrete candidates; [the verification matrix](README.md) records conflicts, coverage limits, and cleanup gates.

The decision is whether to correct these candidates to the existing references or approve a targeted concept/spec revision accepting the following current compositions. Approval would apply only to the listed images and differences, identified by the hashes in [manifest.json](manifest.json), and would not waive future review, supported workflows, keyboard operation, meaningful names, or 48-DIP primary targets.

| Composition | Candidate and differences for review |
| --- | --- |
| Shared shell | Current navigation selection inset, icon/label/tracking treatment, native caption/header rendering, live date/clock. These appear in every workspace capture. |
| Counter | [Capture](native-counter-1440.png): current header/search/category arrangement, photo cards, cream ticket body with dark header, local seeded draft, supported service/operator forms, and Save/review/confirm actions. Direct Charge/pay remains on Service board according to the existing payment boundary. |
| Service board | [Capture](native-service-board-1440.png): explicit operator picker, contextual feedback/actions, selected inspector, and the shown tape/folds/shadows and note spacing. Fixture has one Confirmed, one Preparing, and two Ready orders rather than the original sample queue. |
| Stock room | [Capture](native-stock-room-1440.png): native inventory rows/fields and selected detail/balance card. Four tabs and low-stock/adjustment entry behavior retain the approved Stock room workflow. |
| Menu | [Capture](native-menu-1440.png): the later photo/card, list/detail, action, and spacing treatment, plus restored 48-DIP primary actions; compare against both the HTML and approved #117 native capture. |
| Customers | [Capture](native-customers-1440.png): later profile/contact/history/form spacing and type, native search/actions, and restored primary target sizes; compare against both references. |
| Reports | [Capture](native-reports-1440.png): later summary/chart/filter/date/daily-detail arrangement and type, current applied period and real accounting results; compare against both references. |
| Menu editor | [Capture](native-menu-editor.png): native popup framing and existing two-column fields/opposed footer actions, with the shown unsaved text and 48-DIP Save action. It is a popup image, not a matched full-shell 1440 x 900 image. |

Fixture acceptance would cover the actual names, products, selected records, dates, counts, prices, queue population, and editor draft shown in this run. Production values remain database driven. It would not claim pixel equivalence to the original sample fixture or permit hard-coded production data.

Until this decision is made, tasks 8.1 and 8.4 remain unchecked. An approval still requires recording the targeted concept/spec revision and completing the remaining verification/cleanup gates before either item can be checked. No revision or visual acceptance is inferred from this document.
