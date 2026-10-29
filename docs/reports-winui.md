# Reports workspace

The original layout is retained: four summary cards above a two-column dashboard,
with daily revenue and best sellers on the first row, and payment/channel
breakdowns and current low stock on the second. Narrow windows keep the existing
stacked order and two-by-two summary cards.

Page-local styles increase text, action, and numeric sizes while retaining the
cream, charcoal, and brass palette. Scroll areas reserve space beside their
content. Best sellers show quantity sold and revenue; payment methods have direct
labels, amounts, and percentages. Low-stock rows distinguish available balances
from reorder thresholds.

Daily revenue keeps the original vertical bars. Every reported day has an exact
value and date; longer series scroll within the chart, with visible guidance.
Signed amounts use a zero baseline. Daily details provide a keyboard-accessible
list of dates, amounts, and recognized-order counts.

The reporting-period flyout labels both inclusive dates and validates reversed
ranges in place. Refresh reloads the applied period rather than an unsubmitted
date draft. Payment and channel filters show their active selections and provide
Reset filters. They affect their respective breakdowns; summary cards continue
to show the full period. Current low stock remains independent of the period.
Application queries, recognition rules, and report calculations are unchanged.

## Verification

Build the WinUI project with the Windows App SDK, then run the existing
`CoffeeShop.WinUI.Tests` suite and `dotnet format CoffeeShop.slnx
--verify-no-changes --no-restore`.

The native `--verify-shell --verify-reports` launch checks selection/date state
through seven window sizes from 600 to 1440 logical pixels. It checks chart data
counts and panel bounds, payment/channel filtering and reset, daily details,
invalid dates, applied-period refresh, empty periods, and independent stock
balances. It only queries data; it submits no business writes. Reviewed captures
and reports are in [evidence/reports](evidence/reports).
