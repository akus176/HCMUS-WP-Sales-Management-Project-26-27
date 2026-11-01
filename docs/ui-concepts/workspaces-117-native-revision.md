# Approved native compositions for #117

On 2026-10-29, the user reviewed the four native compositions from implementation commit `9e619622f30366f3f258716a9e004a602879e46d` beside the HTML reference at `0ec5463dcd850d00fbc0223a7e4528a09d12d879` and answered **“Approve these native compositions.”** This is the targeted concept/spec revision requested under the OpenSpec native-rendering conflict rule. It follows the earlier, separate capability approval.

The approved comparison is preserved in the [#117 evidence archive](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/tree/d877088/docs/evidence/workspaces-117). The current native captures are indexed in [Current UI screenshots](../ui-evidences/README.md):

| Composition | Approved native reference | HTML baseline |
| --- | --- | --- |
| Menu | [1440 × 900](../ui-evidences/native-menu-1440.png) | [menu.html](screens/menu.html) |
| Customers | [1440 × 900](../ui-evidences/native-customers-1440.png) | [customers.html](screens/customers.html) |
| Reports | [1440 × 900](../ui-evidences/native-reports-1440.png) | [reports.html](screens/reports.html) |
| Drink editor | [1442 × 933 native window](../ui-evidences/native-menu-editor.png) | [editor capture](menu-picture-editor.png) |

## Approved differences

- Native fields, checkboxes, category rows, functional icons, tab labels/counts, selected navigation/card rules, and header spacing use the shown treatment. Menu and Customers retain header Refresh; category creation remains in the Menu rail and Categories view.
- Menu uses the shown record metadata, brass selected-card framing, compact size action flyouts, first-active-size recipe summary, compatible-topping actions, and footer positioning. Every size retains price, recipe, and deactivation actions.
- Customers uses initials avatars, the balance strip, address action flyouts, search/inactive controls, ledger paging, real recent-order rows, and the explicit profile/delivery form in the dark next-order panel. Their shown spacing and density are accepted.
- Reports uses the shown donut/legend, seller progress tracks, payment/channel filter flyout, daily-detail flyout, chart labels, stock rows, panel spacing, and selected-period copy.
- The 760-DIP drink dialog places name/category in two columns, description across both columns, and Cancel/Save at opposite footer edges. Native field treatment and the shown header decoration are accepted; Cancel and Escape close it without requiring the HTML close affordance.
- Real seeded people, products, dates, counts, empty ledger state, and report values are accepted for this review. The editor's native window framing and 1442 × 933 capture are explicitly accepted instead of requiring a matched 1440 × 900 editor image.

This supplement governs only these approved differences and four compositions. The original HTML/assets remain the baseline for other details and other workspaces. It does not claim that the captures pixel-match the original HTML or permit future visual drift. Supported actions, accounting rules, meaningful accessibility names, visible keyboard focus, responsive state preservation, and 48-DIP primary actions remain required. Historical verification and limitations are recorded in the [#117 evidence archive](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/tree/d877088/docs/evidence/workspaces-117); current screenshots are listed in [Current UI screenshots](../ui-evidences/README.md).
