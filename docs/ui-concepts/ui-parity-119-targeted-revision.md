# Targeted native revision for #119

## Sidebar and Counter polish restoration — 2026-10-31

The user subsequently requested restoration of the polish from PRs #126 and #127
after the parity cleanup replaced it. For the sidebar and Counter, the approved
visual baseline is the implementation at `42a5117` (also present at `af72f07`):
inset navigation content, the compact Counter header, a full-width search above
the categories, filled and bordered product cards, the wider ticket, and three
complete item rows. These later instructions take precedence over older artboard
spacing for these parts of the UI. The #119 accessible names, verification hooks,
48-DIP primary targets, workflow behavior, and legacy cleanup remain in place.

Native verification now records and checks navigation content insets and Counter
row geometry, in addition to the existing behavior and resize checks.

## Earlier targeted exceptions

On 2026-10-31, the user approved a narrow revision for the final six-workspace verification. This revision covers only the four conflicts below. The original [#119 capture set](https://github.com/akus176/HCMUS-WP-Sales-Management-Project-26-27/blob/3a43986b29dd2462868f9fadc11c11bd8125514b/docs/evidence/ui-parity-119/) is archived in Git history; its images were review candidates, not approved whole-screen references. Current screenshots are in [ui-evidences](../ui-evidences/).

1. **Real fixture values.** Product and customer names, order identifiers, dates, amounts, counts, and selected records may reflect the seeded PostgreSQL database. Visual comparison must still use equivalent UI state and data shape; production values remain database-driven. The artboard sample values are not a reason to hard-code production data.
2. **Required operator selection.** Service board may show an operator selector because supported fulfillment and payment commands require an operator. Its presence is approved; its placement and styling must still fit the artboard composition, and disabled actions must explain the prerequisite.
3. **Payment boundary.** Counter retains Save, review, and confirm. Payment and Charge belong to Service board after confirmation under the existing order rules. The concept's direct Counter payment controls are replaced by this supported flow; the remaining ticket layout and styling still require visual parity.
4. **Native editor framing.** The Menu editor may use the native dialog window framing rather than a 1440 by 900 shell capture. Its fields, typography, spacing, actions, and focus behavior remain subject to the approved #117 reference and the current visual contract.

The approved #117 composition revision remains limited to the four captures and differences in `workspaces-117-native-revision.md`. This #119 revision does not approve later visual drift in Menu, Customers, Reports, or the editor. It does not accept the current shell, Counter, Service board, or Stock room screenshots as whole-screen replacements for the HTML artboards. Exact visual review, keyboard and workflow checks, and the 48-DIP primary target requirement remain acceptance gates for tasks 8.1 and 8.4.
