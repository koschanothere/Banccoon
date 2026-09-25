# Linux verification tools

`Banccoon.App` (MAUI, `net10.0-windows`) can't be built or run on Linux. These tools check as
much of it as possible anyway, for sessions working from a Linux sandbox. None of these projects
are in `Banccoon.sln`, so a normal Windows build never sees them.

Run everything with:

```
tools/linux-verification/verify.sh
```

It needs the .NET 10 SDK (on the Ubuntu sandbox: `apt-get update && apt-get install dotnet-sdk-10.0`)
and `python3`.

| Step | What it proves | What it doesn't |
| --- | --- | --- |
| Core/Infrastructure tests | The real test suite (`tests/Banccoon.Tests`) passes. | Anything App-layer. |
| XAML/resx well-formed | Every `.xaml` and `.resx` parses as XML. | That XamlC accepts it. |
| resx parity | `AppStrings.resx` and `AppStrings.ru.resx` have the same keys (ignoring plural suffixes) and the same `{n}` placeholders. | That the Russian is good. |
| C# translation keys | Every `Translator.Get("Key")` / `Translator.GetPlural("Key", n)` in App code names a key that exists (a plural base needs its `_Other` form). | Keys built at runtime (e.g. `Enum_{Type}_{Member}`), which aren't literal strings. |
| `AppTypecheck` | Every view model, formatter, service and the listed page code-behind compiles against `Microsoft.Maui.Graphics` plus thin stubs of the other MAUI APIs they use (`Stubs/`). Running it then checks every compiled `{Binding}` path, scoped by `x:DataType` the way compiled bindings resolve them, plus every `{loc:Translate}` key, against the real types and the neutral resx. It reports 0 errors on the Windows-verified XAML, and planted typos are caught. | XamlC itself, bindings in files without `x:DataType` (resolved by reflection at runtime, so skipped), styles, layout. |
| `FormatterChecks` | `Translator` and every `Formatting/*.cs` produce the expected text in English and Russian from the real resx files. | Anything bound through XAML. |
| `ViewModelTests` | The real view models behave correctly when driven over real Core services and a temp SQLite database, with `MainThread` stubbed to run inline. | The real UI thread (so no proof against off-thread COM crashes), WinUI, Shell navigation, how anything looks. |

**Passing all of this does not mean the app works.** An App-layer change still needs a Windows
build, a launch and a click-through before it counts as done; see the "Needs a Windows pass"
checklist at the top of `docs/development-phases.md`.

## Keeping the stubs honest

`Stubs/MauiStubs.cs` declares only the MAUI members the App actually uses, with the same
signatures. When App code starts using a new MAUI API, `AppTypecheck` fails to compile. Add the
member to the stub with its real signature, never a looser one, or the type-check stops meaning
anything. When a new page's code-behind should be type-checked, add a line for it in
`Stubs/PageStubs.cs` and in `AppTypecheck.csproj`.

## Scripts

- `scripts/resx_parity.py`: the parity check above.
- `scripts/cs_keys.py`: the C# translation-key check above.
- `scripts/resx_add.py entries.json`: inserts EN + RU keys after an anchor key in both resx files,
  XML-escaping values and refusing duplicates. Format:
  `{"anchor_en": "SomeKey" | null, "en": [["Key", "English"]], "ru": [["Key", "Русский"]]}`.
