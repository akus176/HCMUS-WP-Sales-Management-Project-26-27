# Card spacing and selected-tab follow-up — 2026-11-23

Shared record buttons now set horizontal and vertical padding explicitly to 16dp. This prevents text-button padding from overriding the card inset, fixing flush icons, text and chevrons throughout shared cards and navigation rows. Home shortcut cards stretch evenly when a caption wraps.

Selected bottom destinations use a 64 × 32dp pill with clipped rounded corners and a retained native view. The Android check switches Today → More → Today and captures the highlight after each change.

- [Today](padding-today.png)
- [More](padding-more.png)
- [Today after returning](padding-today-return.png)

TypeScript, ESLint, Prettier and seven existing shell/navigation tests pass. The native flow passes and all three screenshots were inspected. This targeted follow-up was checked at normal text size against the real local API. Earlier 100%/200% mockup captures retain their earlier source hashes; no new full role, enlarged-text, tablet or TalkBack acceptance is claimed. Exact updated source hashes are in `verification.json`.
