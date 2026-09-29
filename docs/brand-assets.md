# Banccoon Brand Assets

Place app-ready image assets in:

`src/Banccoon.App/Resources/Images/banccoon/`

Recommended logo variants:

- `banccoon_logo_full_light.svg`: full wordmark plus mascot for light UI.
- `banccoon_logo_full_dark.svg`: full wordmark plus mascot for dark UI.
- `banccoon_mark.svg` or `.png`: compact app mark for navigation and small surfaces.
- `banccoon_mascot_idle_01.png`: friendly default banking raccoon.
- `banccoon_mascot_idle_02.png`: alternate expression or pose for occasional rotation.
- `banccoon_mascot_focus.png`: calmer variant for dashboard or check-in moments.
- `banccoon_mascot_alt_01.png`, `banccoon_mascot_alt_02.png`: optional fun alternates.

Use lowercase filenames with underscores. SVG is best for logos and marks; PNG is best for richer mascot artwork. Keep transparent backgrounds for mascot PNGs when possible.

## What's in the repo today

None of the recommended variants above exist yet. What does:

- `Resources/AppIcon/raccoon_main_icon.png`: the app icon (`MauiIcon` in `Banccoon.App.csproj`). `raccoon_main_icon_v1.png` next to it is an earlier version, not referenced.
- `Resources/Images/raccoon_{dashboard,transactions,accounts,settings}_icon.png`: per-tab images shown in the sidebar header (`AppShellViewModel` picks one by current route). `Images/Originals/` holds their unprocessed sources.
- `Resources/Images/banccoon/banccoon_mark_placeholder.svg`: a placeholder mark.
- `Resources/Images/banccoon/ChatGPT Image Jul 10, 2026, 08_46_00 PM.png`: unreferenced source artwork. Its name breaks the naming rule above; rename it (e.g. `banccoon_mascot_source_01.png`) or move it out of `Resources/Images/` if it's kept only for reference.

`Banccoon.App.csproj` includes only `Resources\Images\*.png` as `MauiImage`, so nothing in the `banccoon/` or `Originals/` subfolders is packaged into the app until that glob changes.
