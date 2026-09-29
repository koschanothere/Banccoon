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

## Iconography (buttons) — global pass under way, 2026-09-29

- **Prior state (superseded below):** every button in the app used a plain text label. Deliberate "get it working first" choice, not a design decision — and deliberately not chased incrementally per-screen while the rest of the app was still being built.
- **Icon set: decided — [Heroicons](https://heroicons.com/).**
- **New: a Settings toggle controls icons app-wide** (`AppSettings`, Settings → General, next to the other appearance-adjacent switches like privacy mode) — **on by default**, and deliberately **not** one of the first-run setup steps; icons are a display preference, not a first-run decision.
- **Default per-button treatment, decided 2026-09-29**: icon-only, with the label shown as a hover tooltip rather than permanent text — reuse `ToolTipProperties.Text`, the same attached-property mechanism already used on `StatementImportPage.xaml`'s review-row amount, rather than introducing a new tooltip mechanism. This is a *default*, not a hard rule: some buttons keep a permanently visible icon **and** label side by side when the icon alone would be ambiguous or the action is important enough to want on sight (e.g. likely candidates: the colored semantic buttons — `DestructiveButton`/`ExpenseButton`/`IncomeButton`/`TransferButton` — since they're already carrying meaning through color and an icon-only version could read as less clear, not more; and primary calls to action like "+ Add"/Save). Decided **case by case per screen**, not by a blanket icon-only-vs-icon+label split.
- **Process, decided 2026-09-29**: a separate session proposes the actual glyph-per-button mapping, screen by screen, for the user to review afterward (screenshots, the same pattern used for every other App-layer change that can't be verified from Linux) — not a live button-by-button walkthrough in chat.

## Dark Mode

- Designed **alongside** light mode from the start, not retrofitted later. `AppThemeMode` already models `System/Light/Dark` in `Banccoon.Core.Appearance` — both palettes below need defining together.

## Color System

### Semantic status colors — fixed, never shift with accent/theme

- On-track / positive: green
- Overdue / negative: red
- Warning / due-soon: orange
- These always mean the same thing everywhere (transaction rows, the "resolve upcoming" widget, reconciliation, the Free-to-spend breakdown) and are **not** affected by the user's chosen accent color — preserves "color as signal" regardless of personalization.
- **Extends to action buttons, not just status text/amounts** — the same fixed colors are used on buttons so the color itself becomes a habit and the user doesn't have to read the label every time: any destructive action (delete a category, a scheduled rule, a transaction) is red; any "this is an expense" action is red-leaning (money going out, same family as negative amounts); any "this is income" action is green (money coming in, same family as positive amounts). Implemented as `DestructiveButton`/`ExpenseButton`/`IncomeButton` styles in `App.xaml`, all a soft/tinted version of the status color (not the saturated status color itself) so they read as calm chips rather than alarms — `QuietButton` with color swapped in, not a new visual weight class. Apply these to every future delete/expense/income button as it's built (Accounts archive/delete, Categories delete, etc.) rather than leaving them as plain `QuietButton`.
- **Transfers are purple** (decided 2026-09-23, on request): money moving between your own accounts gets its own semantic color, `LightTransfer` `#9333EA` / `DarkTransfer` `#C084FC` (plus `*Soft` tints), next to the green/red status family. It's used for the `TransferButton` style (the "+" menu's Transfer button) and, via `TransactionTypeColorConverter`, for every amount that has a transaction type: the Transactions list, resolve-upcoming rows on Transactions and the check-in, and the statement import review, where the amount's color is also the indicator of the detected type. **One amount rule everywhere**: income green, transfer purple, expense the normal text color. Expense amounts deliberately stay uncolored in lists so a page of ordinary spending doesn't turn red, and red stays reserved for expense/destructive *actions* and negative status. The purple is deliberately not the provisional Violet *category* color (`#7C3AED`), since semantic colors and category colors are separate systems; if that category color is finalized close to this purple, revisit one of them.

### Accent color — architecture ready, one scheme implemented for now

- `AccentColor` already exists as a 6-option enum (Emerald/Teal/Blue/Violet/Rose/Amber) in `Banccoon.Core.Appearance`. Keep the architecture (the picker, the enum, theming plumbed through) so switching is trivial later, but **only implement the Emerald scheme's actual color values now** — the other five stay unimplemented placeholders until revisited.
- Accent color governs primary actions, links, and selected-state highlights — not category identity and not semantic status colors (those are separate, fixed systems; see above and below).

### Category colors — separate dedicated palette, not reused from accent

- Categories get their **own fixed color palette**, distinct from both the accent system and the semantic status colors. Each category consistently uses the same color everywhere it appears (transaction row badge, Analytics breakdown/trend, category management).
- **Implemented**: a 7-color palette (`Banccoon.Core.Appearance.CategoryColor`; hex values in `Banccoon.App.Formatting.CategoryColorPalette`, still provisional — "actual colours can be decided later" per direct instruction). Manual override lives in the manage-categories overlay (tap a swatch to set it), matching the original proposal. Categories without an explicit choice still get a color deterministically derived from their Id, so nothing looks uncolored while waiting to be assigned one.

### Category icons

- **Colored circular badge icons** per category (a small glyph — basket, coffee cup, etc. — inside a circle filled with that category's color), shown on transaction rows, in Analytics, and in category management. Reinforces "color as signal" the same way the reference app does it.
