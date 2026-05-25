# Migrating legacy Garden Plot data

If you used the original **Blazor Server** build of Garden Plot, your plots
were stored in your browser via the older single-key `localStorage` /
IndexedDB layer (`gardenplot-db` / `gardenplot.library.v2`). The new
**Blazor WebAssembly** build (issue [#92](https://github.com/Joe-Engineer/GardenPlot/issues/92))
migrates that data automatically on first load. You don't need to do
anything — open the app and your plots will be there.

## How the implicit migration works

On every page load, `GardenPlot.razor.cs` runs a layered load chain:

```
IDB primary  (gardenplot-structured / kv / library/current)   ← new home
  ↓ if empty
Legacy IDB   (gardenplot-db / kv / gardenplot.library.v2)     ← read-only
  ↓ if empty
Legacy IDB backup keys (.bak1, .bak2, .v1)
  ↓ if empty
localStorage (gardenplot.library.v2 + backups)
  ↓ if empty
Seed an empty library
```

The first hit wins. As soon as you save a plot, the new IDB store becomes
the authoritative source and subsequent loads short-circuit at the first
step. The legacy data is **never deleted** by the migration — that gives
you a rollback option if anything looks wrong. You can clear it manually
from your browser's DevTools once you've confirmed the migration worked.

Telemetry counter `gardenplot.migration.kv.completed` fires once per
browser with tags for `source` (where the data came from) and
`plotCount`. This is local-only — it does not leave the browser.

## I had Garden Plot installed locally — where did my plots/ folder data go?

If you ran the old Blazor **Server** build on your own machine, plot
files lived in `%LocalAppData%\GardenPlot\plots\*.json` and images in
`tile-images/` / `plot-images/` subfolders. WebAssembly can't reach the
filesystem, but you can import them manually via the "Import from
legacy installation" button (when present) — it uses the browser's
File System Access API to read the folder you select.

## Troubleshooting

**My plots aren't there after upgrading.**
Open browser DevTools → Application → IndexedDB → look for both
`gardenplot-structured` and `gardenplot-db`. If `gardenplot-db` is
populated and `gardenplot-structured` is empty, the migration step
didn't run. Hard-refresh the page (Ctrl+Shift+R). File an issue
referencing the contents of both databases.

**I want to start fresh.**
DevTools → Application → Clear storage → tick everything → Clear site
data. Reload. The app will seed an empty library.

**I want to export before upgrading.**
Use the "Export library JSON" button (settings menu). The export
includes plots, custom palette items, and photo references but not the
binary photo blobs themselves — for a full backup of photos, copy
the IndexedDB databases from your browser profile.
