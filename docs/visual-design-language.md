# Visual Design Language

Concrete look decisions: typography, shape, density, and color. For the underlying UX principles and interaction patterns these implement, see `ui-structure-decisions.md` — that doc stays principle-level; this one holds the actual tokens.

## Typography

- **Native system font per platform**, not a bundled cross-platform font: Segoe UI Variable on Windows, San Francisco on a future iOS/Mac build. Accepts that the app will look slightly different per platform in exchange for zero bundling and a properly native feel on each OS.
- Type scale (relative, not final pixel values): one oversized/heavy weight for hero numbers (dashboard "Free to spend", Plan-style headline figures), a clear step down for section labels, and a further step down for metadata/timestamps/gray secondary text (e.g. balance-after under a transaction amount).

## Shape Language

- **Moderate corner radius (~8–12px)**, applied consistently to cards, buttons, chips, and input fields. Rounded enough to feel calm and modern, not so rounded it reads as a playful consumer app.
- No hard borders or heavy drop shadows for card separation — surfaces differentiate from the page background via a subtle shade/elevation shift instead (see Color System).

## Density

- **Desktop-first, moderately denser than the mobile reference it was inspired by**, while staying airy rather than cramped. Take advantage of desktop screen space (e.g. more transaction rows visible without scrolling) without collapsing the breathing room that makes the reference feel calm.

## Dark Mode

- Designed **alongside** light mode from the start, not retrofitted later. `AppThemeMode` already models `System/Light/Dark` in `Banccoon.Core.Appearance` — both palettes below need defining together.

## Color System

### Semantic status colors — fixed, never shift with accent/theme

- On-track / positive: green
- Overdue / negative: red
- Warning / due-soon: orange
- These always mean the same thing everywhere (transaction rows, the "resolve upcoming" widget, reconciliation, the Free-to-spend breakdown) and are **not** affected by the user's chosen accent color — preserves "color as signal" regardless of personalization.

### Accent color — architecture ready, one scheme implemented for now

- `AccentColor` already exists as a 6-option enum (Emerald/Teal/Blue/Violet/Rose/Amber) in `Banccoon.Core.Appearance`. Keep the architecture (the picker, the enum, theming plumbed through) so switching is trivial later, but **only implement the Emerald scheme's actual color values now** — the other five stay unimplemented placeholders until revisited.
- Accent color governs primary actions, links, and selected-state highlights — not category identity and not semantic status colors (those are separate, fixed systems; see above and below).

### Category colors — separate dedicated palette, not reused from accent

- Categories get their **own fixed color palette**, distinct from both the accent system and the semantic status colors. Each category consistently uses the same color everywhere it appears (transaction row badge, Analytics breakdown/trend, category management).
- Starting proposal (open to adjustment once seen in the mockup): a rotating set of ~8–10 hues distinct enough to tell apart at a glance (including a couple of neutral/muted tones like tan/brown and gray, not just saturated primaries), assigned to categories in creation order, with manual override available per category in the manage-categories overlay.

### Category icons

- **Colored circular badge icons** per category (a small glyph — basket, coffee cup, etc. — inside a circle filled with that category's color), shown on transaction rows, in Analytics, and in category management. Reinforces "color as signal" the same way the reference app does it.
