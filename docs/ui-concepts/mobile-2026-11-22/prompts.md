# Mobile UI concept prompts

Generated on 2026-11-22 with the built-in Image Generation tool.

All three calls received these local images as visual references:

- `docs/evidence/mobile-quality-186/phone-home.png`
- `docs/ui-concepts/service-board.png`

The first reference was captured at 200% system text scale; the concepts target normal text scale. Images are concept art, not rendered React Native screens. The requested viewport is 390 × 844 logical pixels; generated raster dimensions are recorded in the research README.

## Shared art direction

Use case: ui-mockup. Create realistic, production-quality UI designs with clear hierarchy, strong typography, intentional imagery, and purposeful spacing for Corner Cafe's existing Android STAFF operations app, not a consumer ordering or coffee marketing app.
Target dimensions: a single edge-to-edge 390 x 844 logical-pixel mobile app content frame, portrait aspect 390:844. Render high resolution at the same aspect ratio. APP CONTENT ONLY. No bezel, phone body, OS status bar, clock in status bar, notch, Dynamic Island, battery, home indicator, browser chrome, rounded device mask, or device shadow. Do not put the screen inside a card against a presentation background. One screen only.
Current date anchor 2026-11-22, Sunday, Asia/Bangkok. Do not display calendar dates or invented arrival timestamps.
Reference images are visual grounding, NOT edit targets. First reference is the current native mobile app at 200% system text scale: preserve the warm paper, handwritten personality and chocolate ink, but design at ordinary default text scale rather than copying its large-text layout. Second reference is the approved desktop Service Board: borrow only restrained sticky-paper craft/material cues where explicitly requested; do NOT import its desktop columns, serif typography or gold luxury system.
Hard constraints: paper background #fff9ed; main text #302820; primary cocoa #6d4936 with cream labels; blue informational ink #154d70 on #e8f0f5; butter highlight #f5e6b4; red #aa2c2c reserved for error/urgent meaning. Existing typography: Kalam for clear handwritten headings, Patrick Hand for body and control labels, only these two fonts. Crisp, readable real typography rather than messy simulated handwriting. Headings 30px, row headings24px, body18px, metadata14px minimum. Controls min48 logical px high, primary56. 24px page margins, spacing8/16/24/32, control corners8px, optional ticket corners12px. Conventional small line icons always with visible labels when navigational. Excellent warm-paper contrast. No gradients, glossy effects, floating dashboard cards, excessive ornament, sticker clutter, tilted text, food photography or invented productivity analytics. Use only subtle material grain.
Only current implemented barista workflow: view confirmed/preparing tickets, filter, refresh, open a ticket, inspect size/add-ons, start preparing or mark ready through a confirmation step. No per-item checkboxes, swipe-only actions, drag and drop, undo, assignment, scheduling or unsupported ticket features.
Hierarchy: focused primary screen, one hero use case, one clear main action and only essential supporting content. Use space, alignment, type and hairline rules before boxes. Don't nest cards. Don't advertise every app module. Keep all visible content and actions comfortably in bounds. Navigation for a barista is a clear Staff home back route, no six-module bottom bar. Put compact connection feedback beside the header, not a large banner. Nothing rotated except any decorative paper tape.


## Displayed image 1 — Queue ledger

Direction name: Queue ledger. Show this name nowhere in the app.
Design an elegant paper-ledger preparation queue with airy ruled list rows, NOT cards. Top small brand text "Corner Cafe", a plainly labeled "Staff home" back control. Title "Preparation" in Kalam at30px, small blue "Online" connection label alongside. Supporting line "Confirmed & preparing tickets".
One compact filter row of three min48px-high text controls labeled "All active", "Confirmed", "Preparing"; selected All active has dark chocolate fill and cream text.
Below it, three rows, separated by brown hairlines and generous vertical space, with order identifier left and textual status right, item quantity summary and Table information below. Exact row copy:
"A-106" / "Confirmed"; "3 items · Table 02"; "Cappuccino, Cold Brew, Croissant".
"A-105" / "Preparing"; "4 items · Takeaway"; "2 Flat Whites, 2 Banana Breads".
"A-108" / "Confirmed"; "2 items · Delivery"; "Matcha Latte, Almond Croissant".
Descriptions may wrap naturally. First row distinguished with a narrow butter left margin accent and a clearly labeled blue text link "Open ticket". Other rows have a right-facing chevron within their full accessible row target.
At bottom one full width cocoa primary button "Open A-106". A secondary "Refresh queue" action with refresh icon above or below it, comfortable48px target. No duplicate Open ticket link on first row if primary Open A-106 is sufficient; prefer only the primary. Bottom content stays at least24px from screen edge.
Visual feel: calm working notebook, editorial open layout, handcrafted warmth, operational clarity. Subtle notebook marginal rule permitted. No tape, no sticky note cards in this direction.

## Displayed image 2 — Paper ticket stack

Direction name: Paper ticket stack. Show this name nowhere in app.
Design a preparation queue in a distinctly tactile ticket-stack arrangement. Compact header contains small "Corner Cafe", labeled "Staff home" back action and small blue "Online". Prominent heading "Preparation" in Kalam. Supporting line "Confirmed & preparing tickets".
Below one row of three48px-high filter controls "All active", "Confirmed", "Preparing", with All active selected in dark cocoa.
Main content is exactly two vertically stacked standalone order tickets, taking most of the screen, NOT a horizontal kanban. Tickets straight/aligned. One small translucent tape strip at top of each paper note and very subtle folded bottom corner, delicate actual paper shadows ONLY on tickets. Borrow the approved desktop reference's tangible paper treatment, but use the mobile handwriting and palette.
First ticket butter yellow #f5e6b4, larger hero:
"A-106" at top left, "Confirmed" in clear text at top right;
"3 items · Table 02";
hairline separator;
"1 × Cappuccino · M"
"1 × Cold Brew · L"
"1 × Butter Croissant";
at bottom a full-width accessible cocoa button "Open A-106" with cream text. Opening a ticket happens BEFORE changing preparation state. Do not put Start preparing on queue.
Second ticket warm pale peach, smaller:
"A-105" and "Preparing";
"4 items · Takeaway";
"2 × Flat White · M";
"2 × Banana Bread";
labeled supporting action "Open A-105" with arrow at right.
Below tickets a plain "Refresh queue"48px touch target with conventional refresh icon. No footer bar, decorative coffee illustration, page numbering, workflow metrics or unsupported queue counts.
Use clear item rows, spacious note interiors, no borders on every line. Fit two notes plus controls naturally in390x844 with at least24px bottom clearance. Readable18px body,14px metadata. Calm, handcrafted, useful cafe working slips.

## Displayed image 3 — Ticket focus

Direction name: Ticket focus. Show this name nowhere in the app.
Design a single preparing ticket detail screen, distinct from a queue. An airy paper workspace, NOT a note card inside the screen. One narrow brown marginal rule is permitted.
Top compact brand "Corner Cafe" and small blue "Online". A generous labeled back control "Preparation" with left arrow.
Hero identifier "A-106" in Kalam at38px and a restrained blue "Preparing" text status alongside. Below "Table 02 · 3 items". Do not include customer names, prices, time counters or unsupported details.
Big open, ruled item rows take center stage; each quantity distinct from item name:
"1 × Cappuccino" then metadata "M · Oat milk · Extra shot"
"1 × Cold Brew" then metadata "L · No add-ons"
"1 × Butter Croissant" then metadata "Standard · No add-ons".
Use soft butter highlighting ONLY under important add-ons "Oat milk · Extra shot". No item checkboxes or per-item completion controls. Add-ons readable14-16px minimum, item names22px, plenty of space. No pricing or upsell.
Bottom of viewport has an inline confirmation band on pale butter #f5e6b4, separated by a fine line rather than a floating modal. Exact visible question "Mark this ticket ready?" with small explanatory line "This updates the whole ticket." This is a proposed clearer copy treatment for existing confirmation; status remains Preparing until server confirmation.
One large cocoa primary button "Confirm ready",56px high. Below it a clearly labeled secondary "Keep preparing" control48px high, which dismisses confirmation without mutation.
A small labeled "Refresh ticket" control with conventional refresh icon may sit beside the top back row, NOT between confirmation choices. No competing primary action, no nav bar, no page-level metrics. Keep all controls and content within390x844,24px page margins and bottom clearance.
Visual feel: confident, quiet, tactile working recipe sheet; full-screen paper surface, disciplined spacing, existing Kalam/Patrick Hand pairing and brown/cream colors. Maximum one subtle material detail, no tape, no heavy paper folds, no repeated boxes.

