# Ready-confirmation captures at fea0541

All five cases pass in the fresh `confirmation-01` run on application `fea0541`,
API `5876e57`, Samsung SM-A155F / Android 16. Each selects and opens preparing
ticket A-106, opens ready confirmation, chooses Keep preparing and verifies that
confirmation closes and Mark ready returns.

| Profile        | Display / font setting | Confirmation                           | Canceled                           | JUnit                    |
| -------------- | ---------------------- | -------------------------------------- | ---------------------------------- | ------------------------ |
| Reference      | 390×844dp content, 1.0 | [Image](ref100/after-confirmation.png) | [Image](ref100/after-canceled.png) | [Pass](ref100/junit.xml) |
| Phone          | 360×800dp window, 1.0  | [Image](p100/after-confirmation.png)   | [Image](p100/after-canceled.png)   | [Pass](p100/junit.xml)   |
| Phone enlarged | 360×800dp window, 2.0  | [Image](p200/after-confirmation.png)   | [Image](p200/after-canceled.png)   | [Pass](p200/junit.xml)   |
| Large          | 800×1280dp window, 1.0 | [Image](t100/after-confirmation.png)   | [Image](t100/after-canceled.png)   | [Pass](t100/junit.xml)   |
| Large enlarged | 800×1280dp window, 2.0 | [Image](t200/after-confirmation.png)   | [Image](t200/after-canceled.png)   | [Pass](t200/junit.xml)   |

[The summary](summary.json) contains five passing cases. [Independent API
snapshots](cancellation-state.json) before and after all five cases are equal and
remain preparing. The focused fault suite separately verifies zero business POSTs
and unchanged history for canceled ready confirmation.

[The manifest](images.json) records all 20 original queue, selected, confirmation
and canceled PNGs, source/backend revisions, capture times and raw SHA-256 hashes.
These results predate the direct-sign-in and compact-header follow-ups. Queue
frames here show A-106 preparing. The controlled queue comparison retains
the earlier confirmed-state reference and its recorded revision; the
[comparison viewer](../comparison.html) uses these recorded detail
images for confirmation and cancellation.

[Native geometry](ref100/geometry.json) measures 1170×2664px at 480dpi, with 87px
status and 45px navigation bars: exactly 390×844dp content. PNGs remain unedited;
the viewer clips measured system bars using CSS. Large layouts use display
overrides on the same phone. The app retains its light palette under light/dark
Android settings. TalkBack focus is recorded separately from these visual checks.
