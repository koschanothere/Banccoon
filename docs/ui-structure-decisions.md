# UI Structure Decisions

Decisions made in the UI planning walkthrough, before any visual/look design. This is the source of truth for information architecture and interaction patterns; it supersedes the structural parts of `.codex/development-phases.md` where they conflict. Implementation phasing still lives in that file.

## Top-Level Navigation

- 4 tabs: **Dashboard, Transactions, Accounts, Settings**. No separate tabs for Scheduled, Statements, Goals, Forecast, Analytics, or Reconciliation.
- Workflow pattern is a **mix**: quick edits (create/edit a single transaction, account, category) are overlays; big multi-step flows (statement import, reconciliation) are full pages.
- General overlay cancel behavior: confirm before discarding only if there's unsaved progress (e.g. some import rows already reviewed). Trivial/empty state closes instantly, no prompt.

## Dashboard

- **Emphasis: cash flow / "what's coming"**, made concrete as a **"Free to spend" hero number** — not a big net-worth number, not per-account detail first. This was the original intent for the app and maps directly onto the already-built `AvailableToSpendService` (see Money Model below), not a new concept.
- The hero number has a collapsible **"how it's calculated" breakdown** beneath it (lowest forecasted balance in the window, minus goal reservations, minus safety buffer, equals free to spend) — collapsed by default, exact arithmetic on demand. Same disclosure pattern used elsewhere, not a one-off.
- Balance/forecast graph: defaults to past 7 days + saved forecast period forward, with a current-time marker, **plus a control to pick any custom date range**. Sits alongside/below the hero number, not competing with it for primary visual weight.
- Account cards on dashboard: balance shown plainly, account number masked (matches global masking default).
- Below the graph, collapsible sections, default order: **Upcoming → Forecast → Analytics → Goals**. Order is customizable in Settings.
- The actionable "resolve this scheduled item" widget (mark paid/skip/delay) does **not** live on the dashboard — see Transactions.
- Statement import is **not** offered as a dashboard entry point.

### Forecast section (on Dashboard)

- Only control: forecast period length (e.g. 1/3/6 months). No what-if toggles, no other controls here.

### Analytics section (on Dashboard)

- Spending by category, current period vs. last period **and** vs. last few periods (multi-period, not just a 2-period comparison).
- Independent date range from the main graph, defaults to current month.
- Functional scope (visual treatment TBD in the "look" phase):
  - Multi-period trend per category (line/bar over last N months)
  - Top movers / biggest changes auto-surfaced (e.g. "Dining out up 40%")
  - Drill-down: clicking a category filters straight into Transactions for that category/period
  - Income vs. expense breakdown, not just expense categories

## Money Model: Free to Spend

Already partially implemented in `Banccoon.Core.Forecasting.AvailableToSpendService` — this section extends it, doesn't replace it.

- **Formula (existing):** `AvailableToSpend = LowestForecastedBalance − ReservedForSavingsGoals`, where `LowestForecastedBalance` is the lowest point the forecast curve dips to within the window (so scheduled/planned outflows landing in that window are already reflected).
- **New: safety-buffer reserve.** Add a user-set **overall** "always keep at least X ₽ free" amount, subtracted alongside goal reservations: `AvailableToSpend = LowestForecastedBalance − ReservedForSavingsGoals − SafetyBuffer`. One global number, not per-account.
- **New: selectable window modes**, all offered in Settings (not one hardcoded behavior):
  - **Rolling N days** — today + N days, N configurable (extends today's fixed 7/30/60/90 enum to any value).
  - **Calendar-aligned** — e.g. this week (Monday–Sunday) or this calendar month; the window resets at the calendar boundary rather than sliding daily.
  - **Dynamic, until next major payment** — window end is derived from the user's own scheduled data (the next big upcoming obligation), matching the reference app's "free until Aug 20" framing. Needs a definition of "major" (e.g. above some amount threshold, or specific categories) — deferred to implementation design, not decided yet.
- The user picks which mode is the default; this becomes part of `AppSettings` alongside the existing saved forecast period.

## Accounts

- **Flat list**, no type grouping, manually/drag orderable.
- Clicking an account does **not** open a separate account-detail screen — it filters the Transactions screen to that account.
- Archiving: explicit "archive" action; archived accounts hidden from the main list by default, kept for history.
- Styling: **one consistent style across account types**, small type indicator (icon/tag) only — no per-type card shapes.
- Goal-type account row shows: name, current amount, progress bar, goal target amount.
- Credit-card account: same base row style; utilization/min payment/payoff estimate live in a separate **card details overlay** opened from the account, not inline.
- **Primary account**: one account can be flagged primary. New concept, not yet built (needs an `AppSettings.PrimaryAccountId` or an `Account.IsPrimary` flag). Used as the default account pre-selected in Expense/Income/Transfer overlays and as the quick-entry account fallback if/when that feature returns.
- **Excluded from totals**: already built — `Account.IncludeInDashboardTotals` exists in Core (`src/Banccoon.Core/Models/Account.cs`). Just needs a UI toggle in account editing; no new Core work. Use case: a savings account whose balance shouldn't count toward the dashboard's "Free to spend" total.

## Transactions

- Row content: **name, category, amount**, with **balance-after shown in gray directly under the amount** (de-emphasized, not removed), and a **small scheduled-mark icon** on the row when the transaction is linked to a scheduled item. Nothing requires expanding the row or opening edit to see these — they're all visible at a glance.
- **"+" add menu**: Expense / Income / Transfer. Attaching to an existing scheduled occurrence is an optional field *inside* these overlays, not a separate 4th type.
- **Statement import** is a separate entry point/button next to "+", not inside the add menu (different shape: multi-step, file-based, batch review). Available from first-run setup and Transactions; not from Dashboard.
- **Scheduled template management** (create/edit recurring rules) lives under **Settings** — it's infrequent, config-like.
- **Persistent "resolve upcoming" widget** pinned at the top of Transactions: for each due/overdue scheduled item, actions are **Mark Paid / Skip / Delay**. Pre-fills from the template on Mark Paid. Persists until resolved — does not disappear on its own.
- **Manual reconciliation "check-in"** action lives in the Transactions header, next to filters and category management — grouped with the other header-level actions rather than a new location.
- Filtering (account, category, date range, type): collapsed filter bar that expands on demand.
- Bulk actions: explicit **"Select" mode toggle** button switches the list into checkbox mode; then mass category assignment, mass scheduled-occurrence assignment, mass delete.
- **Category management** icon lives in the Transactions header next to the filter control, opening a manage-categories overlay (rename/merge/delete/recolor/reorder). This is deliberately **not in Settings** — categories are living financial data you touch constantly, unlike scheduled templates.
  - Category **creation** is inline wherever you pick a category (transaction entry, statement import row, scheduled assignment) — type a new name, confirm, done.
  - Categories are **flat**, no groups/hierarchy.

## Statement Import (full-page guided flow)

- Steps: read statement → confirm/create account match → review rows.
- Row review: **shrinking list** as rows are approved/skipped, plus a bulk **"approve all matching category X"** action — but that action still surfaces the matched rows for review before committing, rather than silently mass-approving.
- Duplicate warnings stay visible pre-approval. Category learning persists on approval.
- **Parser resolution order**: try the user's configured default parser first → if that fails, fall back to full auto-detection across all registered parsers (`IStatementParserRegistry.FindParser`, already built) → if nothing matches, surface a clear failure message with a path to flag the format for a new parser (this is a single-developer personal app, so "contact the developer" effectively means "note it down for yourself to build a parser from a sample").
- **Bank parser backlog — high priority**: Sberbank exists already. **Tinkoff and Alphabank are next**, confirmed as must-have. Each new bank needs a real statement sample to build/verify its parser against; get samples from actual account holders before writing the parser.

## Quick Entry (Free-Text Transaction Parsing) — Deferred QoL

Not part of the active build plan; revisit later as a quality-of-life addition, not abandoned outright.

- Feasible without any external/cloud AI call — a local heuristic parser (regex for amount, fuzzy match against account names, category matching reusing the existing `CategorySuggestionService`/`CategoryLearningRule` engine already built for statement import) plus a confidence-flagged, editable-chip confirmation step before commit.
- Defaults if revisited: expense unless an income signal is present; account falls back to the **primary account** when none is mentioned; low-confidence category defaults to `Other`, editable via chip, never blocking confirmation (consistent with statement import's existing behavior).
- Open question if revisited: keyword-matching robustness across the user's actual vocabulary/language (Russian morphology makes plain substring matching weaker than in English) — not a blocker, just needs fuzzier matching than exact-substring.

## Reconciliation (full-page guided flow)

- Triggers: **auto-suggested right after statement import**, plus a manual "check-in" action in the Transactions header, available anytime.
- UI pattern: same **shrinking-list, confirm/delay/skip** interaction as the Transactions "resolve upcoming" widget, for consistency across the app rather than a second interaction language.
- Keeps actual-balance comparison, grouped spending, and explicit balance-adjustment transactions (for auditability) as steps within the flow.

## Settings

- One Settings section, organized into sub-areas: **Scheduled** (template management), **Preferences**, **Data**, **Appearance**. Categories are explicitly excluded (see Transactions).
- **Data** sub-section groups backup/export/restore clearly; **delete-all-local-data** requires an extra deliberate confirmation step given it's irreversible and there's no cloud sync.
- Dashboard section order (Upcoming/Forecast/Analytics/Goals) is customizable here.

## First-Run / Blank-State Setup

- Full-screen guided overlay, not skippable, offering exactly 3 paths: **Import a bank statement / Manual setup / Restore a backup**. Each routes into its respective existing workflow.

## Design Language (from reference walkthrough)

Principles extracted from a reference app (iOS + Mac, not to be copied visually — see the reference walkthrough for the source screenshots). These are principle-level; concrete tokens (typography, color values, corner radius, density) now live in `docs/visual-design-language.md`.

- **Calm precision, one sentence.** Exact numbers, one loud hero number per screen, color used only when it signals something (status/category identity), never decoratively. The math behind any headline number is always one tap from visible, never hidden.
- **Color as signal, not decoration.** A neutral near-white/light-gray base; each semantic state (on-track, overdue, warning) and each category gets one consistent color reused everywhere it appears.
- **Reusable components over bespoke widgets.** The same disclosure pattern ("how it's calculated"), the same progress-style component, etc. should recur across screens rather than each screen inventing its own visual metaphor. Whether the specific "progress bar + expected-pace tick mark" widget is one of these reused components is still open — revisit once something is mocked up.
- **One deliberate fast path per core action**, not a menu of options competing for attention — this is already reflected in the Transactions "+" (Expense/Income/Transfer) and separate import button decisions above.
- **Platform-appropriate chrome.** Desktop and any future mobile client can use genuinely different navigation shapes (e.g. a persistent sidebar on desktop) rather than one layout stretched across both — relevant once/if a second platform is built.
- **No AI-generated look**: no gradients-as-decoration, no glassmorphism, no generic card-with-shadow-everywhere pattern, no cheerful/filler copy in the product UI. Dry, functional copy; restraint over flourish.

## Brand / Mascot

- The raccoon mascot (per `docs/brand-assets.md`) is **confined to empty states, onboarding, and the app icon**. Working screens (Dashboard, Transactions, Accounts, Settings) stay mascot-free and clean, matching the "no AI-generated look" / calm-precision direction rather than a character-branded feel throughout.

## Open / Deferred

- Visual design system ("the look") — colors, type, spacing, exact component shapes — still to come once you share concrete look references.
- Whether the progress-bar/pace-marker widget gets adopted, and where — revisit once mocked up.
- Definition of "major payment" for the dynamic free-to-spend window mode — deferred to implementation design.
- Exact overlay busy/error state visuals — deferred to look phase.
- Desktop notifications/reminders (Phase 7 item) — not covered in this pass, revisit later.
