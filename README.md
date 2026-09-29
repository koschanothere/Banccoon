# Banccoon

A private, offline-first personal finance and forecasting app for Windows desktop. Everything
lives in a local SQLite database on your machine: no accounts, no cloud sync, no telemetry.

Banccoon tracks accounts, transactions and recurring (scheduled) items. It projects balances
forward to show what's **free to spend**, and imports bank statements (Sberbank debit-card PDF
today), learning how you categorise each merchant. A guided check-in reconciles the app against
your real bank balance. The UI is in English and Russian and switches language live.

---

## Contents

- [Requirements](#requirements)
- [Build and run](#build-and-run)
- [Tests](#tests)
- [Solution layout](#solution-layout)
- [Architecture](#architecture)
- [Where data lives](#where-data-lives)
- [Common tasks](#common-tasks)
- [Guardrails](#guardrails)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)
- [Further documentation](#further-documentation)

---

## Requirements

| | |
| --- | --- |
| OS | Windows 10 1809 (build 17763) or later. The App project only targets Windows. |
| SDK | .NET 10 SDK |
| Workload | .NET MAUI (`dotnet workload install maui`) |
| IDE (optional) | Visual Studio 2022+ with the *.NET Multi-platform App UI* workload, or Rider |

`Banccoon.Core`, `Banccoon.Infrastructure` and the test project are plain `net10.0` libraries,
so they build and test on any OS. Only `Banccoon.App` needs Windows.

## Build and run

```bash
dotnet build Banccoon.sln
```

```bash
dotnet run --project src/Banccoon.App/Banccoon.App.csproj
```

The app is unpackaged (`WindowsPackageType=None`), so it runs straight from the build output
with no MSIX install step. On first launch with an empty database it opens the first-run setup
screen (language, theme, default categories, bank).

**Debug builds** show an extra *Open first-run setup (dev)* button under
Settings → Data & Security → Diagnostics (`SettingsViewModel.IsDevToolsVisible`). Release
builds hide it.

## Tests

### Core and Infrastructure (any OS)

```bash
dotnet test tests/Banccoon.Tests/Banccoon.Tests.csproj
```

xUnit. The suite covers Core services and the SQLite repositories. Repository tests run
against a temporary database (`StaticDatabasePathProvider`), never the real one in
`%LOCALAPPDATA%`.

### App layer from Linux

`Banccoon.App` can't build outside Windows. `tools/linux-verification/verify.sh` checks as much
of it as possible from Linux: XAML binding paths, translation keys, resx parity, formatters in
both languages, and view-model tests over real Core services. See
[tools/linux-verification/README.md](tools/linux-verification/README.md) for what each step
proves and what it doesn't.

> **Passing tests does not mean an App change works.** XamlC, WinUI rendering, the UI thread
> and Shell navigation are only exercised by a real Windows build and a click-through. The
> "Windows pass" checklist at the top of
> [docs/development-phases.md](docs/development-phases.md) tracks what still needs one.

## Solution layout

```
Banccoon.sln
├─ src/
│  ├─ Banccoon.Core/            Domain models and business logic. No UI, no I/O dependencies.
│  ├─ Banccoon.Infrastructure/  SQLite repositories, caching, JSON backup, PDF statement parsing.
│  └─ Banccoon.App/             .NET MAUI (WinUI) desktop app: pages, view models, formatting, i18n.
├─ tests/
│  └─ Banccoon.Tests/           xUnit tests for Core and Infrastructure.
├─ tools/
│  ├─ icon-font/                Builds the Heroicons button-icon font from the committed SVGs.
│  └─ linux-verification/       App-layer checks runnable from Linux (not in the .sln).
└─ docs/                        Product, UI and visual-design decisions; roadmap.
```

### Banccoon.Core, by folder

| Folder | What's in it |
| --- | --- |
| `Models` | Accounts, transactions, categories, scheduled transactions, settings. |
| `Repositories` | Repository interfaces (implemented in Infrastructure). |
| `Forecasting` | Balance projection, *Free to spend*, historical balances, projected-balance chart data. |
| `Recurrence` | Recurrence rules, the `FREQ=…;START=…` syntax, validation and descriptions. |
| `Transactions` | Applying, reversing and deleting transactions against account balances. |
| `Statements` | Parser contracts and registry, import batches/rows, duplicate detection, category learning. |
| `Reconciliation` | Check-in flow: expected items, matching, grouped spending, balance adjustments. |
| `Categories` | Two-level category hierarchy and category management (merge, rename, etc.). |
| `Savings`, `CreditCards` | Goal-account progress and reservations; credit-card payoff projections. |
| `ImportExport` | Backup/export contracts and validation. |
| `Analytics` | Spending breakdowns and trends. |
| `Setup` | First-run setup and default categories. |
| `Localization` | Plural rules (English and Russian). |
| `Security` | App-lock PIN hashing. |
| `Abstractions` | Cross-cutting seams such as `IDateProvider`. |

### Banccoon.App, by folder

| Folder | What's in it |
| --- | --- |
| `Views` | Pages. Dashboard, Transactions, Accounts, Settings are sidebar tabs. Statement import, Reconciliation, App lock and First-run setup are one-off flows. |
| `ViewModels` | One view model per page, plus small child view models for each section or form. |
| `Formatting` | Money, dates, account numbers and other display text. |
| `Localization` | `Translator` and the `{loc:Translate}` XAML markup extension. |
| `Resources/Strings` | `AppStrings.resx` (English) and `AppStrings.ru.resx` (Russian). |
| `Controls`, `Converters` | Custom controls (e.g. the forecast chart, `IconButton`) and value converters. |
| `Services` | App-level services such as the automatic backup runner. |
| `Diagnostics` | `DiagnosticLog`, the best-effort file logger. |

## Architecture

### Layering

```
Banccoon.App  ──►  Banccoon.Core  ◄──  Banccoon.Infrastructure
     └──────────────────────────────────────────┘
```

Core defines models, services and repository interfaces. Infrastructure implements them against
SQLite and the file system. The App wires both together and owns everything visual. Core must
not return pre-composed English sentences: it returns codes or structured data, and the App
turns them into translated text.

### Dependency injection

Everything is registered in [`MauiProgram.cs`](src/Banccoon.App/MauiProgram.cs):

- **Services and repositories are singletons.**
- **Repositories are wrapped in `Cached*` decorators** over the `Sqlite*` implementation (see
  `Infrastructure/Caching/EntityCache.cs`). The whole database is small, so it's loaded once and
  patched on every write. That's what makes tab switches fast. Two exceptions:
  - Transactions only keep the **last 12 months** in memory (`RecentTransactionWindow`). Older
    ranges are read from SQLite on demand.
  - Statement-import batches and rows aren't cached.
- **Categories get a second decorator**, `HierarchicalCategoryRepository`, which enforces the
  two-level hierarchy and colour inheritance on every save.
- **Tab pages are singletons; their view models are transient.** See
  [Guardrails](#guardrails) for why.

### Database and schema changes

`BanccoonDatabaseInitializer` creates and migrates the schema once per process, before the
first repository call. Migrations are **additive and idempotent**: `CREATE TABLE IF NOT EXISTS`,
`CREATE INDEX IF NOT EXISTS`, and `AddMissingColumnAsync` for new columns. There's no
migration-version table. SQLite runs with `foreign_keys = ON` and `journal_mode = WAL`.

### Localization

- Strings live in `Resources/Strings/AppStrings.resx` (EN) and `AppStrings.ru.resx` (RU).
- XAML uses `{loc:Translate SomeKey}`. C# uses `Translator.Get("SomeKey")` or
  `Translator.GetPlural("SomeKey", count)`.
- Plural keys take suffixes: English uses `_One`/`_Other`, Russian uses `_One`/`_Few`/`_Many`.
  The template takes a single `{0}` for the count.
- A missing key renders as the key itself, never a blank, so gaps are visible.
- Language switches live, with no restart: `Translator` raises `PropertyChanged` and every
  `{loc:Translate}` binding refreshes.
- Bank-statement text (merchant names, bank categories) is data, not UI, and isn't translated.

### Statement import

`IStatementParserRegistry` holds every registered `IStatementParser`. It tries the user's
preferred parsers (the bank chosen at setup) first, then the first parser whose `CanParse`
accepts the file. The only parser today is `SberbankDebitCardStatementParser` (PDF, via
PdfPig). Parsed rows become a pending import batch that the user reviews row by row, or in
bulk, before anything becomes a real transaction.

### Security and privacy

- The optional app-lock PIN is salted and hashed (PBKDF2-SHA256, 100k iterations) by
  `PinHasher`. It's a privacy speedbump, **not encryption**: the SQLite file on disk is
  unencrypted.
- Privacy mode masks amounts in the UI.

## Where data lives

Everything is under `%LOCALAPPDATA%\Banccoon\`:

| Path | Contents |
| --- | --- |
| `banccoon.db` (plus `-wal`/`-shm`) | The SQLite database: all accounts, transactions, settings. |
| `diagnostics.log` | Best-effort log: unhandled exceptions, timings, migration notes. Check here first when something misbehaves. |
| `AutoBackups\banccoon-autobackup-*.json` | Automatic JSON backups, if enabled in Settings. They're checked on launch (there's no background scheduler) and old ones are pruned to the retention count. |

**Manual backup and restore** are under Settings → Data & Security. A backup is one JSON file
covering the whole database. Restore validates it first (e.g. rejects a transaction that
references a missing account), then either **Merge** into or **Replace everything** in the
existing data.

**Starting fresh:** Settings → Data & Security → *Delete all local data*, or close the app and
delete `banccoon.db*`. The next launch opens first-run setup.

## Common tasks

### Add a column to an existing table

1. Add the property to the model in `Banccoon.Core/Models`.
2. Add it to the `CREATE TABLE` statement in `BanccoonDatabaseInitializer` (for fresh databases).
3. Add an `AddMissingColumnAsync` call (for existing databases), with a default value:

   ```csharp
   await AddMissingColumnAsync(connection, "Settings", "PrivacyModeEnabled", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
   ```

4. Read and write it in the matching `Sqlite*Repository`.
5. Include it in backup export/import (`RepositoryExportService`, `RepositoryImportService`) so
   backups round-trip.
6. Add a repository test in `tests/Banccoon.Tests/Infrastructure`.

### Add a translated string

1. Add the key to **both** `AppStrings.resx` and `AppStrings.ru.resx`, with the same `{n}`
   placeholders. For plurals, add every suffix each language needs.
2. Use it from XAML with `{loc:Translate Key}`, or from C# with `Translator.Get("Key")`.
3. From Linux, `tools/linux-verification/scripts/resx_add.py` inserts EN + RU entries together,
   and `verify.sh` checks parity and that every key used from C# exists.

### Give a button an icon

Use `controls:IconButton` with an `Icon` from `Controls/Heroicons.cs` and a `Label` (not `Text`); add
`ShowLabel="True"` to keep the label visible. The glyphs come from a font built from Heroicons SVGs; to add
one, see *Iconography* in [docs/visual-design-language.md](docs/visual-design-language.md).

### Add a statement parser for another bank

1. Implement `IStatementParser` in `Banccoon.Infrastructure/Statements`. Give it a unique
   `Descriptor.Id`, and make `CanParse` cheap and specific: it's called on every candidate file.
2. Register it in `MauiProgram.cs` alongside the existing one:

   ```csharp
   builder.Services.AddSingleton<IStatementParser, MyBankStatementParser>();
   ```

   The registry picks up every `IStatementParser` automatically.
3. Add parser tests in `tests/Banccoon.Tests/Statements`.

### Add a page

- **A sidebar tab:** add a `FlyoutItem` in `AppShell.xaml`, then register the page as a
  **singleton** and its view model as **transient**.
- **A one-off flow** (wizard, modal): register the page as **transient** and add a route with
  `Routing.RegisterRoute` in `AppShell.xaml.cs`.
- Keep it small. Split a growing page into child view models (see the `StatementImport*`
  view models) rather than letting one file absorb the whole feature.

## Guardrails

Each of these comes from a real bug. Break one and you'll likely see a crash or slowdown that's
hard to trace.

1. **Never constructor-inject a `Page` or `Shell` type into `App`.** It causes a deterministic
   native `STATUS_STOWED_EXCEPTION` (`0xC000027B`) in `Microsoft.ui.xaml.dll` on launch, with
   nothing in managed logs. Use `new AppShell()`. Inside a page or Shell's parameterless
   constructor, resolve what you need through
   `IPlatformApplication.Current!.Services.GetRequiredService<T>()`.
2. **Mutate bound state on the UI thread after every `await`.** Microsoft.Data.Sqlite resumes on
   thread-pool threads, and touching a bound `ObservableCollection` from one throws a native
   `COMException` (`0x8000FFFF`) in WinUI. Use `ViewModelBase.RunOnMainThreadAsync(...)`.
3. **Sidebar-tab pages must be singletons.** Shell re-runs a tab's `DataTemplate` on every
   switch, so a transient page rebuilds its whole native visual tree every time (50–150 ms).
4. **Measure before optimising.** Use `Stopwatch` timings written to `diagnostics.log` before
   changing code for a "feels slow" report.

## Troubleshooting

| Symptom | Likely cause / what to do |
| --- | --- |
| App exits immediately on launch, nothing logged | Native WinUI crash. Check **Event Viewer → Windows Logs → Application**. If it's `0xC000027B`, see guardrail 1. |
| Sidebar goes blank or tabs stop navigating | An off-thread UI mutation (guardrail 2). Look for `COMException` in `diagnostics.log`. |
| Text shows a raw key like `Settings_PrivacyModeLabel` | Missing resx key in the current language. Add it to both resx files. |
| Build error from XamlC | A compiled `{Binding}` path doesn't match the `x:DataType`. Fix the path or the type. |
| "No parser is available for this statement yet." on import | No registered parser's `CanParse` accepted the file. Only Sberbank debit-card PDFs are supported today. |
| Tab switches feel slow | Check the page is registered as a singleton (guardrail 3), then measure (guardrail 4). |
| Want to see first-run setup again | In a Debug build, Settings → Data & Security → *Open first-run setup (dev)*. Otherwise reset data. |

## Contributing

- **Branch** off `main` and open a PR back into it.
- **Keep files small.** One view model or service per concern. Split before a file starts
  absorbing a whole feature.
- **Match the surrounding code:** file-scoped namespaces, nullable enabled, a `Cached*` +
  `Sqlite*` pair for new repositories, comments that explain *why*.
- **Tests:** Core and Infrastructure changes come with xUnit tests. App-layer changes come with
  a `verify.sh` run where possible, **and** a Windows click-through in both English and Russian.
- **Definition of done** for anything user-visible: it builds on Windows, it launches, you've
  clicked through it, and `diagnostics.log` is clean. If you can't do the Windows pass, add the
  item to the Windows-pass checklist in `docs/development-phases.md` instead of calling it done.

## Further documentation

| Document | Covers |
| --- | --- |
| [docs/development-phases.md](docs/development-phases.md) | Roadmap, implementation log, Windows-pass checklist, open priorities. |
| [docs/ui-structure-decisions.md](docs/ui-structure-decisions.md) | Information architecture and interaction patterns, including the *Free to spend* money model. Entries the user later overrode are marked **superseded**, with a pointer to the newer decision. |
| [docs/visual-design-language.md](docs/visual-design-language.md) | Colour tokens, typography, icon set (Heroicons). |
| [docs/brand-assets.md](docs/brand-assets.md) | Logo and icon assets. |
| [tools/linux-verification/README.md](tools/linux-verification/README.md) | The Linux-side App verification tooling. |
