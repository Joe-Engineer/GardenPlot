# Garden Plot — Software Requirements

> Living document capturing the features, boundaries, technical stack, and conventions established for the Garden Plot application. Update this as new requirements are added or existing ones evolve.

## 1. Vision

Garden Plot is a **local-first, single-user, browser-based garden-planning tool**. It supports a measured 2D plot canvas where the user lays out raised-bed kits, free-form shapes, trees, bushes, and individual garden plants, with measurement tools, plant-specific guidance (spacing, sunlight, water, companions), and export. All persistence is local to the user's browser — no server-side data store, no account.

## 2. Software Stack

| Layer | Choice | Rationale |
|---|---|---|
| Runtime / language | .NET 10 / C# `latest` | Already targeted by all `csproj` in the solution. |
| UI | **Blazor WebAssembly** (PWA, offline-capable, installable) | Migrated from Blazor Server in 2025 to make Garden Plot a true local-first app — no SignalR connection, no server-side data store, installable as a PWA. See `docs/migrating-legacy-data.md` for the upgrade path from the legacy build. |
| Host | **`GardenPlotWeb.Host`** — minimal ASP.NET Core static-file project that serves the published WASM payload | Required so Azure App Service Linux can run the app (App Service Linux can't serve a pure-WASM publish directly — see PR #211). |
| Drawing surface | Inline **SVG** with viewBox in feet | Vector, zoomable, exportable. |
| Persistence | **IndexedDB** (browser-local), via two cooperating ES modules under `wwwroot/js/`: `client-store.js` (structured key/value DB `gardenplot-structured`) and `client-images.js` (blob DB `gardenplot`) | Replaces the previous `localStorage`-based persistence (#92). The structured DB stores the plot library document; the blob DB stores tile / plot-background / dossier photos. |
| JS interop | Three ES modules under `wwwroot/js/`, all loaded via `IJSRuntime.InvokeAsync<IJSObjectReference>("import", …)`: `gardenplot.js` (canvas interop — wheel/pointer/touch, viewport, PNG/PDF/CSV export, generic helpers), `client-store.js` (IndexedDB key/value for the plot library), `client-images.js` (IndexedDB blobs for tile / plot-background / dossier photos). | Canvas concerns and persistence concerns are intentionally segregated; image-blob IDB is split from structured-data IDB to avoid shared-ownership schema-version traps. |
| External API | Wikipedia REST `/api/rest_v1/page/summary/{title}` | Free, no API key, CORS-friendly. Called **client-side** (WASM) via the scoped `HttpClient`. Polite `User-Agent`. The Wikipedia integration can be disabled in settings for zero outbound traffic. |
| Static analysis | `StyleCop.Analyzers` + `latest-recommended` analyzers, `EnforceCodeStyleInBuild=true`, **`TreatWarningsAsErrors=true`** | Configured centrally via `Directory.Build.props` and `.editorconfig`. |

### 2.1 Project layout

```
GardenPlot.slnx
├── .editorconfig            ← solution-wide style + analyzer rules
├── .gitattributes           ← line-ending policy (CRLF / LF for .sh / binary) — see #239
├── stylecop.json            ← Garden Plot company name + copyright template
├── Directory.Build.props    ← TreatWarningsAsErrors, AnalysisMode, StyleCop pkg
├── docs/                    ← this file + hosting, persistence-architecture,
│                              migrating-legacy-data, payload-budget
├── GardenPlot.DataFetcher/  ← console tool for catalog seed-data refresh
├── GardenPlotWeb/           ← Blazor WebAssembly app (main feature)
│   ├── Components/
│   │   ├── App.razor, Routes.razor, _Imports.razor
│   │   ├── Layout/(MainLayout, NavMenu, ReconnectModal)
│   │   └── Pages/
│   │       └── GardenPlot.razor (+ .razor.cs + .razor.css) ← main feature
│   ├── Models/
│   │   ├── GardenPlotModels.cs, PlantRendering.cs
│   │   ├── Jigs/ (#95 — extracted drawing-tool jigs)
│   │   └── LayerResolver.cs, Takeoff.cs, etc.
│   ├── Services/Persistence/
│   │   ├── IPlotRepository.cs, IndexedDbPlotRepository.cs
│   │   ├── IndexedDbStorage.cs (IClientKvStorage wrapper)
│   │   └── PlotLibraryLoader.cs (per-version JSON loader / migrator)
│   ├── Services/ (ProjectDossierService, CatalogService, CitationService,
│   │              UnhandledErrorRecorder, …)
│   ├── Build/PayloadBudget.targets ← enforces 3 MB Brotli first-paint budget
│   └── wwwroot/
│       ├── js/gardenplot.js      ← canvas interop (wheel/pointer/touch, viewport, PNG/PDF/CSV export)
│       ├── js/client-store.js    ← IndexedDB key/value (gardenplot-structured DB)
│       ├── js/client-images.js   ← IndexedDB blob store (gardenplot DB)
│       ├── manifest.webmanifest, service-worker.js (passthrough),
│       │   service-worker.published.js (precache for offline)
│       └── web.config             ← IIS/SWA SPA fallback + MIME types
├── GardenPlotWeb.Host/      ← ASP.NET Core static-file host (App Service Linux)
└── GardenPlot.Tests/        ← unit + integration tests (xUnit; 1,400+ cases)
```

> **Migration history.** The original Garden Plot was a Blazor Server app with all
> data in `localStorage`. The WASM migration (issue #92, completed 2025) moved
> persistence to IndexedDB across two databases. Auto-migration of legacy
> `localStorage` plot data is implemented in `PlotLibraryLoader`; see
> [`docs/migrating-legacy-data.md`](migrating-legacy-data.md) for details.

## 3. Functional Requirements

### 3.1 Plot library

- **Multiple plots**, persisted as a JSON document in IndexedDB under the `gardenplot-structured` database at key `library/current`. Image blobs (tile images, plot backgrounds, dossier photos) live in a separate `gardenplot` database. Auto-migrated from the legacy `localStorage` key `gardenplot.library.v1` on first load (#92).
- A **plot selector** (dropdown) above the canvas lists every saved plot as `Name (W ft × H ft)`.
- **+ New Plot** opens a modal accepting Name, Shape (currently *Rectangle (flat)* — extension point), Width (ft), Height (ft). Width / Height clamped to 1–500 ft.
- Plot create/edit dialogs include a transient **Lock aspect ratio** chain toggle between Width and Height. It is **off by default**, captures the current ratio when enabled, keeps that ratio while editing, and resets to unlocked each time the dialog opens.
- **Delete Plot** (visible only when ≥ 2 plots exist) removes the current plot and falls back to the first.
- The last-active plot ID is remembered (`PlotLibrary.LastPlotId`) and auto-restored on page load.
- A default 60 × 8 ft *"My Garden"* plot is created on first visit if no library exists.

### 3.2 Canvas

- SVG `viewBox` is in **feet** (`0 0 W H`); rendered size is `W × H × 16 × zoom` CSS pixels.
- Grid pattern at 1 ft, with a brown plot border.
- The user's pointer / wheel / keyboard interactions are anchored to the SVG element which is `tabindex="0"` and auto-focused on `pointerdown`.

### 3.3 Tools

| Tool | Behavior |
|---|---|
| **Select** | Click a shape to select; Shift+click toggles multi-select; click empty area clears selection (Shift preserves). Click and drag a selected shape to move all selected shapes together (rotated-AABB clamped to plot). |
| **Free Draw** | Click-drag captures a polyline. Stroke gray. |
| **Rectangle** | Click-drag draws a rect. |
| **Oval** | Click-drag draws an ellipse. |
| **Ruler** | Click anchors first vertex, then live segment to cursor; **Ctrl+click** appends a vertex; plain click finalizes. Renders red polyline with vertex dots and per-segment length labels. |
| **Stamp** | Auto-engaged when a palette item is clicked. Ghost preview follows the cursor; click drops a copy at that center, clamped to plot. Stays in stamp mode for repeated drops. |

#### Cross-tool interactions

- **Ctrl + drag** in any non-Ruler tool **pans** the canvas by scrolling the wrap container (3 px movement threshold to differentiate click from drag).
- **Shift + wheel**: zoom (×1.1 / ÷1.1 per tick, clamped 25 % – 600 %).
- **Ctrl + wheel**: rotate the active shape(s) or stamp ghost by 1° per tick.
- No-modifier wheel scrolls the page normally (JS handler only `preventDefault`s on Shift / Ctrl).
- **Delete** / **Backspace** removes every selected shape.
- **Esc** cancels in-progress drafting and clears selection.
- While a tool other than Select is active, placed shapes have `pointer-events="none"` so clicks pass through to the canvas (e.g., dropping a plant on top of a planter, drawing a ruler segment over a bed).
- **Rotation memory per palette item** — last stamp rotation is saved in `PlotData.KitRotations[Code]` and recalled on re-selection.

### 3.4 Palettes

The palette sidebar has a **combobox** at the top (always visible, anchored above the scrollable item grid) listing the seven user-facing categories below. Switching categories clears any active stamp and returns to Select. Each palette item is rendered as a small SVG preview + code/name + detail line.

| Category | Source data | Notes |
|---|---|---|
| **Bed Kits** | `PaletteCatalog.BedKits` (9) | C2080, C3565, C2065, C5050, C3550, C3535, C2050, C2035, C2020. Width × Height in feet, piece count. |
| **Trees — Fruit** | `Trees` filtered to `Trait ∈ {fruit, nut}` | Edible canopy. Stylized SVG with red/brown accent dots. |
| **Trees — Ornamental** | `Trees` filtered to `Trait ∈ {flower, shade, foliage, evergreen}` | Stylized canopy with flower glyphs or evergreen overlay. |
| **Bushes — Edible** | `Bushes` filtered to `Trait = fruit` | Berry-bearing shrubs. |
| **Bushes — Ornamental** | `Bushes` filtered to `Trait ∈ {flower, foliage, evergreen}` | Cluster of overlapping circles. |
| **Vegetables** | `Plants` filtered to `Trait ∈ {vegetable, flower}` | Companion flowers (Marigold, Nasturtium, etc.) live here because they're typically planted alongside vegetables. |
| **Herbs** | `Plants` filtered to `Trait = herb` | Spacing diameter, sunlight, water, days to maturity. |

`PaletteCatalog.For(PaletteCategory)` and `PaletteCatalog.CategoryFor(PaletteItem)` are the canonical mappings; the underlying `PaletteKind` (BedKit / Tree / Bush / Plant) still drives the resulting `Shape.Kind` when stamped.

Plant sizes are based on commonly cited horticultural references (extension service / nursery guides) and are intended as typical landscape mature sizes; the codebase notes this caveat in source comments.

### 3.5 Selection and shape manipulation

- **Multi-select** via **Shift+click**.
- **Group drag**: snapshot all selected shapes' origins on `pointerdown`, apply the same delta on move, clamp the **union** of rotated AABBs to the plot.
- **Group rotate**: Ctrl+wheel rotates each selected shape around its own center, with per-shape post-rotation reclamp.
- **Group / Ungroup toolbar actions**:
  - **Group** is enabled whenever **≥ 2** shapes are selected, even if the selection spans existing groups; regrouping assigns a fresh `GroupId` and contiguous `GroupIndex` values to the full selection.
  - **Ungroup** is enabled whenever **any** selected shape has a `GroupId`; a multi-group selection ungroups every selected group in one operation.
- **Alignment toolbar** (visible when ≥ 2 selected): six SVG-icon buttons.
  - Align Left / Right / Top / Bottom — uses rotated-AABB edges.
  - Distribute Horizontally / Vertically (≥ 3 selected) — anchors outermost shapes, equal gaps for the middle ones: `gap = (span − Σwidths) / (n − 1)`.
- **Delete Selected** removes all.

### 3.6 Selection / preview info panel

Floating panel (default lower-right; draggable; position persisted in `PlotLibrary.Ui.InfoPanelX/Y`).

| Mode | Trigger | Content |
|---|---|---|
| **Single shape** | Exactly one placed shape selected | Title `Type · Name`. Per-kind detail lines. Wikipedia summary for trees / bushes. Companions (good / bad rule lists + nearby plants within 2× spacing) for plants. Appearance section if the kind supports it. |
| **Multi-select** | ≥ 2 selected | Header `Selection · N items`. Compact grouped list `Type: Name × count`. Appearance section (multi-aware). |
| **Stamp preview** | No selection, but a palette item is active | Header `Preview · Type · Name`. Same per-kind details and Wikipedia preview as a placed shape. Appearance section hidden (nothing to color yet). For plants, only the static *Plant near* / *Avoid* lists are shown (no positional "nearby" check). |

#### Appearance controls

For shape kinds that support styling (**Rectangle, Oval, Free Draw, Bed Kit, Ruler**):

- **Line color** — `<input type="color">` writes `Shape.Stroke`.
- **Fill color** — writes `Shape.Fill`.
- **Fill opacity** — `<input type="range">` writes `Shape.FillOpacity` (0..1).
- **Mixed values** — when the selection has differing effective values, the input gets a dashed/striped border and an `(mixed)` label appears beside it. Picking a value writes through to all customizable shapes in the selection, unifying them.
- **Reset to defaults** clears `Stroke`/`Fill`/`FillOpacity` on the selected customizable shapes so they revert to kind defaults.
- Trees / Bushes / Plants use stylized rendering and are **not** color-customizable in this iteration (a hint is shown if the selection contains only those).

### 3.7 Ruler info panel

Separate floating panel (default upper-right; draggable; position persisted in `PlotLibrary.Ui.RulerPanelX/Y`).

- Visible whenever a ruler is being drafted **or** a placed ruler is selected.
- Shows: segment count, total length (formatted feet+inches and decimal feet), and the **enclosed polygon area** in ft² computed by treating the polyline as auto-closed (shoelace formula, ≥ 3 vertices).
- During drafting, an inline hint reminds the user: *Click to finish · Ctrl+click to add a vertex*.

### 3.8 Plant spacing visualization

For every placed `Plant` shape, the canvas renders concentric translucent rings + a dashed perimeter at the recommended spacing radius. Each plant gets one verdict computed against the **worst-overlap neighbor**:

- **Green** — no overlap (`distance ≥ summed spacing radii`).
- **Yellow** — partial overlap (worst overlap fraction `< 50 %`).
- **Red** — heavy overlap (worst overlap fraction `≥ 50 %`).

Rings render under shapes (so the sprite always stays visible) and ignore pointer events.

### 3.9 Companion planting

`CompanionRules.Map` is a static dictionary keyed by plant code, returning `(Good, Bad)` neighbor lists. Used by:

- The selection panel's **Companions nearby** list (proximity + verdict).
- The static **Plant near / Avoid** lists shown on every plant info panel (and on stamp previews).

### 3.10 Takeoff list

Collapsible `<details>` panel below the canvas:

- Groups every placed shape by `(Kind, Name)`. `Name` is `Label` for kits/species, falling back to size for unnamed Rectangle/Oval, and a placeholder for FreeDraw / Ruler.
- Summary line shows distinct-item count and total placed.
- **Download CSV** button calls `gardenplot.js#downloadText` with `Type,Name,Count` rows. Filename derives from the plot name via `Sanitize`.

### 3.11 Export

| Action | Mechanism |
|---|---|
| **PNG** | `gardenplot.js#exportPng` serializes the canvas SVG, draws onto an offscreen canvas at 2× scale, downloads as `<plot-name>.png`. |
| **Print / PDF** | `gardenplot.js#printSvg` opens a new window with the inline SVG and a small title, calls `window.print()`. User chooses *Save as PDF* in the print dialog. |
| **CSV (takeoff)** | See § 3.10. |

## 4. Data Model

### 4.1 Persisted shapes (`Models/GardenPlotModels.cs`)

```csharp
public enum ShapeKind { Rectangle, Oval, FreeDraw, BedKit, Ruler, Tree, Bush, Plant }
public enum PaletteKind { BedKit, Tree, Bush, Plant }

public record struct Point(double X, double Y);

public record PaletteItem(string Code, PaletteKind Kind, double WidthFt, double HeightFt,
                          string Trait = "", int Pieces = 0,
                          string Sunlight = "", string Water = "",
                          int DaysToMaturity = 0, string Notes = "");

public class Shape
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ShapeKind Kind { get; set; }
    public double X, Y, W, H;     // expressed as properties
    public double Rotation { get; set; }
    public List<Point> Points { get; set; } = new();
    public string? Label { get; set; }
    public string Trait { get; set; } = string.Empty;
    public string? Stroke { get; set; }
    public string? Fill { get; set; }
    public double? FillOpacity { get; set; }
}

public class PlotData
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Untitled";
    public double WidthFt { get; set; } = 60;
    public double HeightFt { get; set; } = 8;
    public List<Shape> Shapes { get; set; } = new();
    public Dictionary<string, double> KitRotations { get; set; } = new();
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
}

public class PlotLibrary
{
    public Guid? LastPlotId { get; set; }
    public List<PlotData> Plots { get; set; } = new();
    public UiPreferences Ui { get; set; } = new();
}

public class UiPreferences
{
    public double? RulerPanelX { get; set; }
    public double? RulerPanelY { get; set; }
    public double? InfoPanelX { get; set; }
    public double? InfoPanelY { get; set; }
}
```

### 4.2 Persistence rules

- **IndexedDB-backed** via `IPlotRepository` / `IndexedDbPlotRepository`. The plot library document lives in the `gardenplot-structured` database (`IClientKvStorage` → `wwwroot/js/client-store.js`) under key `library/current`; image blobs (tile images, dossier photos, plot backgrounds) live in the separate `gardenplot` database (`wwwroot/js/client-images.js`). The two databases are intentionally segregated to avoid shared-ownership schema-version traps. Legacy `localStorage` key `gardenplot.library.v1` is auto-migrated on first WASM load (see [`docs/migrating-legacy-data.md`](migrating-legacy-data.md)).
- **Auto-save** after every mutating action: stamp drop, draft completion, drag end, rotate, delete, clear, plot create / delete / switch, color change, panel reposition.
- All types are public and have parameterless constructors / settable properties so `System.Text.Json` round-trips cleanly.
- Backward-compatible: nullable additions (`Stroke`, `Fill`, `FillOpacity`, `Trait`, `UiPreferences` fields) deserialize cleanly from older saves.

## 5. Non-Functional Requirements

### 5.1 Privacy / data residency

- **No personal data leaves the browser.** All plots, kit rotations, panel positions, custom palette items, and uploaded images live in the browser's IndexedDB (two databases: `gardenplot-structured` for the plot library document, `gardenplot` for image blobs). There is no server-side data store and no account.
- The only outbound network call is to Wikipedia's public REST API, called **client-side** from the WASM runtime, and only when the user selects a tree / bush species. The Wikipedia call sends only the species name (parenthetical stripped) — no user data. The integration can be disabled in settings for truly zero outbound traffic.

### 5.2 Performance

- Drawing is SVG with viewBox in feet; redraws are O(N) over `currentPlot.Shapes`. Realistic plot sizes (≤ a few hundred shapes) render comfortably without virtualization.
- Wikipedia results are cached per-topic in a `Dictionary<string, WikiSummary?>` for the lifetime of the page.
- Spacing-overlap computation is O(P²) over `Plant`-kind shapes; acceptable for typical garden scales.

### 5.3 Build / static analysis

- `TreatWarningsAsErrors = true` solution-wide.
- `AnalysisMode = Recommended`, `AnalysisLevel = latest-recommended`.
- StyleCop configured per `.editorconfig`. Disabled rules are explicit and minimal — primarily documentation rules (no XML-doc requirement on every public member), file-organization (we ship intentional model bundles), and a few stylistic spacing rules that conflict with column-aligned data tables.
- Garden Plot SA1633 file header is **required** on every `.cs` file:

  ```csharp
  // <copyright file="Foo.cs" company="Garden Plot">
  // Copyright (c) Garden Plot. All rights reserved.
  // </copyright>
  ```

- Build target: **0 warnings, 0 errors**. Razor `.razor.g.cs` and other generated code is excluded.

### 5.4 Code style

- File-scoped namespaces.
- Top-level statements in `Program.cs`.
- Implicit usings on; `Nullable` enabled.
- Fields camelCase, properties PascalCase. Records used for value-like data (`Point`, `PaletteItem`, `BedKit`, `WikiSummary`).
- All UI lives in **`GardenPlot.razor`** + scoped CSS; data and rendering helpers live in **`Models/`**; cross-component services live in **`Services/`**.
- JS interop uses three ES modules (`gardenplot.js`, `client-store.js`, `client-images.js`); never inline `<script>`.

### 5.5 Payload budget

- **First-paint Brotli budget: 3 MB** for the `_framework/` directory of the published WASM app. Enforced by `GardenPlotWeb/Build/PayloadBudget.targets` (runs `AfterTargets="Publish"`).
- See [`docs/payload-budget.md`](payload-budget.md) for the budget rationale, current usage by category, and investigation guidance when the budget is exceeded.
- Two project-level mitigations are in `GardenPlotWeb.csproj`:
  - `InvariantGlobalization=true` (saves ~600 KB by removing ICU locale data; only invariant culture sorting is used)
  - `BlazorEnableTimeZoneSupport=false` (saves ~30 KB by removing the IANA tz DB; only `DateTime.ToLocalTime()` is used, which works off the browser's UTC offset)
- The PR-validate CI workflow runs `dotnet publish` so payload-budget regressions are caught **before merge** instead of in the deploy workflow.

### 5.6 Continuous integration

Two GitHub Actions workflows in `.github/workflows/`:

- **`pr-validate.yml`** — runs on every PR to `main`. Steps (in order):
  1. **Verify line-ending policy** — `git add --renormalize .` + `git diff --cached --quiet` to reject any file violating `.gitattributes` (issue #239). Fails fast (~10 s) before expensive steps.
  2. Set up .NET 10 SDK preview + install `wasm-tools` workload.
  3. `dotnet restore` / `dotnet build --configuration Release -warnaserror`.
  4. `dotnet test --configuration Release` — full xUnit suite (1,400+ tests).
  5. `dotnet publish` — enforces the 3 MB payload budget via `PayloadBudget.targets`.

- **`main_gardenplot.yml`** — runs on push to `main`. Builds, publishes, and deploys the WASM payload to Azure App Service Linux (via `GardenPlotWeb.Host` static-file project — see #211).

## 6. Visual / Interaction Style

| Element | Color / spec |
|---|---|
| Plot background | `#f3efe3` |
| Plot border | `#7a6a4a` |
| Grid lines | `#cfd8c5` (1 ft pattern) |
| Bed kit fill / stroke / opacity | `#e2725b` / `#7a3520` / 0.5 (terra cotta, semi-transparent so plants on top stay visible) |
| Rectangle / Oval default fill / stroke | `#4a7c59` (alpha 0.35) / `#2f5a3a` |
| Free Draw stroke | `#3a3a3a` |
| Ruler stroke | `#c81e1e` |
| Selection outline | `#0d6efd` dashed |
| Tree canopy palette | trait-driven (shade / fruit / nut / flower / evergreen / foliage) |
| Bush lobes palette | trait-driven |
| Plant sprites | green stem + leaves; yellow flower dot for `flower` trait |
| Spacing rings | green / yellow / red 3-step |
| Floating panels | white card, colored border (red for ruler, green for selection info) |

- Default canvas scale: **16 px / ft**, multiplied by `zoom`.
- Drag-grip glyph `⠿` reveals on panel header hover.
- Mixed-value color inputs render with a striped/dashed background.

## 7. Out of Scope (this iteration)

The following are deliberate non-goals at this stage; they are documented so future work has clear hooks:

- **Multi-user or cloud sync.** Local-only by design; introducing accounts requires explicit revisit of § 5.1.
- **Color customization for trees / bushes / plants.** Stylized rendering is intentional; allow it later by extending `PlantRendering` helpers to take optional override colors.
- **Frost-date / planting-window overlays.** Requires a USDA hardiness zone selector per `PlotData` and per-plant planting-window math relative to last frost.
- **Elevation data / map view.** Initial requirement was "optional elevation; for now flat". Add a heightmap layer (SVG `<image>` or per-cell rects) under the grid.
- **Rotation of point-based shapes around their own center** is supported; rotation around an arbitrary pivot is not.
- **Box-select / rubber-band selection** — only Shift+click is implemented.
- **Undo / redo.** No history stack yet.
- **Mobile / touch gestures** (pinch-zoom, two-finger pan) — pointer events work but UX is desktop-first.

## 8. Glossary

| Term | Meaning |
|---|---|
| **Plot** | A named canvas with its dimensions, drawn shapes, and per-kit rotation memory. Lives in `PlotData`. |
| **Shape** | Any item drawn on the canvas: Rectangle, Oval, FreeDraw, BedKit, Ruler, Tree, Bush, Plant. |
| **PaletteItem** | An entry in a palette catalog (BedKit / Tree / Bush / Plant). The canonical data source for stamping. |
| **Stamp** | A click that drops a copy of the active palette item onto the canvas at the cursor. |
| **Trait** | Free-form classification used for stylized plant rendering and badges: `fruit`, `nut`, `flower`, `shade`, `evergreen`, `foliage`, plus app-internal `vegetable` / `herb` / `flower` for plants. |
| **Effective stroke / fill / opacity** | `Shape.Stroke ?? DefaultStroke(s)` etc. — used by the renderer so customization is opt-in. |
| **Customizable shape** | A `ShapeKind` whose stroke/fill/opacity may be overridden via the selection panel: Rectangle, Oval, FreeDraw, BedKit, Ruler. |
| **Companion** | A relationship from `CompanionRules.Map`: good / bad / neutral, drives the colored marker beside nearby plant entries in the info panel. |
| **Takeoff** | Construction-industry term for a counted bill of items. Here, a tabular list grouped by `(Kind, Name)` with counts, exportable as CSV. |

## 9. Change Discipline

When adding a new feature:

1. Update this document — at minimum add it to the relevant § 3 subsection or § 7 if it's a new boundary.
2. New shape kinds extend `ShapeKind` (only ever **append**, never reorder, to keep saved JSON forward-compatible).
3. New persisted fields on existing types must be **nullable** with sensible defaults.
4. New JSON shape changes that aren't backward-compatible **must bump the storage key suffix** (`gardenplot.library.v2`) and write a migration step.
5. Every new `.cs` file gets the SA1633 *Garden Plot* header (the build will fail otherwise).
6. Every code-style suppression added to `.editorconfig` must include a comment justifying it.
