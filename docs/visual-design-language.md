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

## Iconography (buttons) — built 2026-09-29, needs the user's review

- **Prior state (superseded below):** every button in the app used a plain text label. Deliberate "get it working first" choice, not a design decision — and deliberately not chased incrementally per-screen while the rest of the app was still being built.
- **Icon set: decided — [Heroicons](https://heroicons.com/)**, the **outline** style (24px, 1.5px strokes): the thinner, quieter of the two, which suits "calm precision, restraint over flourish" better than the filled solid set. Pinned to Heroicons 2.2.0.
- **A Settings toggle controls icons app-wide**: `AppSettings.ShowIcons`, Settings → General → "Icons on buttons", right under privacy mode — **on by default**, deliberately **not** one of the first-run setup steps. Like its neighbours it applies on Save, then live on every page at once, no restart. Off = every icon-bearing button shows its text label exactly as before icons existed.
- **Default per-button treatment, decided 2026-09-29**: icon-only, with the label shown as a hover tooltip (`ToolTipProperties.Text`, the mechanism already used on the import review's amount). A *default*, not a blanket rule — see "Which buttons got what" below for the case-by-case calls.

### How icons are embedded — decided 2026-09-29: an icon font + `FontImageSource`

- **The choice.** The outline Heroicons are bundled as one small TrueType font, `Resources/Fonts/Heroicons-Outline.ttf` (51 glyphs, ~11 KB, registered as font family `Heroicons` in `MauiProgram.cs`), drawn with MAUI's `FontImageSource` as a real `Button`'s `ImageSource`.
- **Why not individual SVGs through Resizetizer** (the other realistic option): Resizetizer rasterises each SVG to a PNG at *build* time, in one fixed colour. This app needs every glyph in its button's own text colour — primary text, white on the accent button, the red/green/purple semantic buttons — in both light and dark themes, plus a dimmed disabled state. With SVGs that means an asset per icon × colour × theme, or icons that ignore their button's colour. A font glyph takes any colour at runtime, so theme switches and semantic colours come for free, and it stays sharp at any size and DPI. `FontImageSource` + `ConfigureFonts` is also MAUI's idiomatic, well-trodden path on Windows, unpackaged apps included.
- **The catch, and how it's handled.** Heroicons ships SVGs only — there's no official font — and the outline set is drawn with *strokes*, which a font can't hold. `tools/icon-font/build_heroicons_font.py` converts each listed icon's strokes to filled outlines (picosvg, the stroke-to-fill step Google's nanoemoji font tooling uses), merges the overlapping pieces (skia-pathops), writes the TrueType font (fontTools) and regenerates `Controls/Heroicons.cs`, the C# constant for each glyph's codepoint (Private Use Area, from U+E000). The source SVGs and Heroicons' MIT licence are committed under `tools/icon-font/heroicons-2.2.0-outline-24/`, so a rebuild needs no network. Checked by rendering every glyph through FreeType and through Windows' own GDI+ font loader: all 51 match the Heroicons originals (round caps, holes, centred in the em square) and tint correctly. **Not yet seen inside the running app** (see the Windows-pass checklist, section 14).
- **Adding an icon:** copy its SVG from the Heroicons 2.2.0 package (`24/outline/<name>.svg`) into that folder, **append** its name to `tools/icon-font/icons.txt` (order = codepoints, so never reorder), then `pip install picosvg fonttools` and `python tools/icon-font/build_heroicons_font.py`. Commit the SVG, the list, the `.ttf` and `Heroicons.cs` together.

### The one reusable pattern: `Controls/IconButton`

- A `Button` subclass, so hover, pressed, focus, disabled and the existing button styles (`QuietButton`, `PrimaryButton`, `DestructiveButton`, …) behave exactly as before. Usage: `<controls:IconButton Icon="{x:Static controls:Heroicons.Funnel}" Label="{loc:Translate Transactions_FilterButton}" Command="…" Style="{StaticResource QuietButton}" />`. Set `Label`, never `Text`; the control decides `Text`.
- Three states: icons on + `ShowLabel="False"` (default) → icon only, `Label` as the tooltip and screen-reader name; icons on + `ShowLabel="True"` → icon and label side by side, no tooltip; icons off (`IconPreference.ShowIcons`) → `Label` as plain text. The glyph is drawn in the button's own `TextColor`, redrawn when a theme switch changes it, and at 40% alpha when disabled (WinUI greys a disabled button's text but not its image).
- `TextOnlyLabel`: what the button says with icons off, when that differs from `Label`. Used where today's text carried its own glyph ("+ Add account", "‹ Previous", "▲"), which would double up next to a real icon: the icon version gets a clean label, and icons-off keeps the exact old text.
- `BoolToGlyphConverter` (instances in `App.xaml`) flips the glyph for toggles whose label already flips: Show/Hide number (eye / eye-slash), Attach… / Cancel (link / x-mark).
- `Services/IconPreference`: the ambient, notifying `ShowIcons` flag every `IconButton` binds to (weakly, through the binding engine, so recycled list rows don't leak through it) — the same "static, no DI" shape as `PrivacyMode` and `Translator`. Set at startup, on Settings → General Save, on every Settings visit, and after a restore from first-run setup.

### Which buttons got what (2026-09-29)

- **Icon-only + tooltip** (small utility actions, mostly in headers and rows): Transactions header Filter / Check-in / Select; every overlay's header Close (x-mark); row actions Delay (clock), Skip (forward), Attach…/Cancel, Edit (pencil-square); reorder arrows (Accounts rows, Settings section order); the Dashboard chart range's Apply (check) / Reset (arrow-path) and Analytics month ‹ › (chevrons); Accounts "Show archived" (archive-box) and the account card's View transactions (list-bullet), Card details (credit-card), Set primary (check-badge — deliberately not a star, which already means "favourite" on the rows), Show/Hide number; learned-rules Edit and paging; the categories and import-review Select toggles; the import select bar's "Apply to selected" (tag). Where the old label was too terse to stand alone as a tooltip it got a fuller one: "Apply" → "Show this date range", "Select" → "Select transactions" / "Select categories" / "Select rows", "Check-in" → "Check in against your bank balance", bare arrows → "Previous month", "Move up", "Previous page"….
- **Icon + visible label**: every `PrimaryButton` (Save, Import, Mark paid, Approve, Continue, Edit account, Add account, Set/Change PIN, Record adjustment, Done, Unlock…) and every coloured semantic button (`Destructive`/`Expense`/`Income`/`Transfer`: Delete, Archive, Forget, Remove PIN, Replace everything, Cancel import, and the "+" menu's Expense / Income / Transfer with arrow-up-right / arrow-down-left / arrows-right-left); the "+ Add" triggers (plus); form-level Cancel next to Save; data operations (Export backup, Choose backup file, Open diagnostics log, Open data folder); wizard Back / Continue (Continue's arrow sits after its label); the import select bar's second row (Select all, Clear selection, Select possible duplicates, Skip selected), which would be ambiguous as bare glyphs and has a row to itself.
- **Deliberately still text** (not icon-bearing, so `ShowIcons` doesn't touch them): choice chips (theme, hero metric, free-to-spend window, setup's language and "how to start" choices, the import's existing/new account choice); disclosure toggles whose text is the point ("How it's calculated", "Show rows"/"Hide rows", "Show more (N)", "Load earlier month"); link-style buttons (the Analytics rows' "View transactions", setup's restore link); actions with no honest glyph ("Finish without adjusting"); the Debug-only "Open first-run setup (dev)"; and the Accounts favourite star (already a glyph toggle).
- **Not converted, flag don't decide**: the drawn chevrons that expand Resolve upcoming and import groups (two rotated `BoxView`s, not buttons, with no text to fall back to) and the Settings side list. Whether they should move to the Heroicons chevron for consistency is open.
- **Screens covered**: Dashboard, Transactions, Accounts, Settings, Statement import, Reconciliation, First-run setup, App lock, and the recurrence editor. Every `<Button>` left in the XAML is one of the "still text" cases above.

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
