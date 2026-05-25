# Garden Plot

A **local-first**, browser-based garden-planning tool. Lay out raised-bed kits, draw freehand shapes, drop trees / bushes / vegetables / herbs from curated palettes, measure with a multi-segment ruler, see live plant-spacing feedback, get Wikipedia summaries on plant species, and export to PNG or PDF — all without an account or server-side data store.

> **Status:** active development. See [`docs/Requirements.md`](docs/Requirements.md) for the canonical feature/boundary specification.

## Highlights

- **SVG canvas in feet** with grid, zoom (Shift+wheel), pan (Ctrl+drag), and per-shape rotated-AABB clamping.
- **Multiple plots**, each with its own dimensions, shapes, and palette state — all persisted to `localStorage` (no server data).
- **Categorized palettes** behind a combobox: Bed Kits, Trees — Fruit, Trees — Ornamental, Bushes — Edible, Bushes — Ornamental, Vegetables, Herbs.
- **Stylized plant rendering** with trait-based accents (fruit dots, nut ovals, flower glyphs, conifer overlay).
- **Plant spacing rings** color-coded green / yellow / red based on overlap with neighbors.
- **Companion-planting** rules + nearby-plant verdicts in the selection panel.
- **Multi-segment ruler** with live length, formatted ft+inch readout, and auto-closed-polygon area.
- **Multi-select** via Shift+click; group drag, group rotate, alignment toolbar, distribute horizontally / vertically.
- **Per-shape stroke / fill / opacity** with mixed-value indicators in the selection panel.
- **Stamp preview** shows full plant details (badges, Wikipedia summary, companion lists) before you click to place.
- **Takeoff list** with CSV download; **PNG** and **Print/PDF** export.
- **Floating, draggable info panels** (ruler + selected item), positions persisted.
- **Wikipedia REST integration** (no API key) for tree/bush species summaries.

## Tech stack

- **.NET 10** / C# `latest`
- **Blazor WebAssembly** (PWA, offline-capable, installable)
- **IndexedDB** for browser-local persistence (plots, photos, custom catalog)
- **SVG** drawing surface, ES JS modules for IndexedDB key/value (`client-store.js`) and image blobs (`client-images.js`)
- **NetTopologySuite** for polygon math (clipping, area)
- **StyleCop.Analyzers** + `latest-recommended` analyzers, `EnforceCodeStyleInBuild`, **`TreatWarningsAsErrors=true`**

## Project layout

```
GardenPlot.slnx
├── .editorconfig            ← solution-wide style + analyzer rules (Garden Plot SA1633 header)
├── stylecop.json            ← Garden Plot company name + copyright template
├── Directory.Build.props    ← TreatWarningsAsErrors, AnalysisMode, StyleCop pkg
├── docs/
│   ├── Requirements.md      ← canonical feature/boundary specification
│   ├── hosting.md           ← static-host deployment requirements
│   ├── migrating-legacy-data.md  ← upgrade path from old Blazor Server build
│   └── payload-budget.md    ← first-paint payload tracking & investigation guide
├── README.md                ← this file
└── GardenPlotWeb/           ← Blazor WebAssembly app
    ├── Components/Pages/GardenPlot.razor (+ .razor.css)
    ├── Models/(GardenPlotModels.cs, PlantRendering.cs)
    ├── Services/Persistence/  ← IPlotRepository, IndexedDbPlotRepository, IndexedDbStorage
    ├── Build/PayloadBudget.targets  ← enforces 3 MB Brotli first-paint budget
    └── wwwroot/
        ├── js/client-store.js     ← IndexedDB key/value (gardenplot-structured DB)
        ├── js/client-images.js    ← IndexedDB blob store (gardenplot DB)
        ├── manifest.webmanifest   ← PWA manifest
        ├── service-worker.js      ← PWA precache (offline)
        └── web.config             ← IIS/SWA SPA fallback + MIME types
```

## Getting started

### Prerequisites

- **.NET 10 SDK** (project targets `net10.0`)
- **Visual Studio 2026**, **VS Code with C# Dev Kit**, or any IDE with .NET 10 support

### Build and run

```pwsh
# From the repository root
dotnet build GardenPlot.slnx

# Run the WASM app via the dev server (browser opens automatically)
dotnet run --project GardenPlotWeb

# Publish a release build (also enforces the 3 MB first-paint Brotli budget)
dotnet publish GardenPlotWeb -c Release
```

Open the URL printed in the console; navigate to **Garden Plot** in the side menu. To install as a PWA, look for the install icon in your browser's address bar.

### Build is strict

`TreatWarningsAsErrors=true` and `EnforceCodeStyleInBuild=true` are in effect via `Directory.Build.props`. Every new `.cs` file must include the SA1633 file header:

```csharp
// <copyright file="Foo.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>
```

## Privacy

Garden Plot is **browser-local first**. Everything you create — plots, custom palette items, tile/photo uploads, panel positions — is stored in your browser's **IndexedDB**, never sent to a server. Two IndexedDB databases are used:

- `gardenplot-structured` — the plot library document (key `library/current`).
- `gardenplot` — image blobs (tile images, plot background images, dossier photos).

There's no account, no telemetry, no sync server. If you want your data on another machine, use the export button (or simply copy your browser profile). Server-backed sync for paying users is tracked in issue #100 and is out of scope for this build.

The only outbound network call is to the public **Wikipedia REST summary API** (no API key, sends only the species name) when you select a tree or bush. Disable the Wikipedia integration in settings if you want truly zero outbound traffic.

Upgrading from the old Blazor Server / `localStorage` build? See [`docs/migrating-legacy-data.md`](docs/migrating-legacy-data.md) — your data migrates automatically on first load.

## Roadmap (deferred items)

These are intentionally **out of scope** today and are listed in [`docs/Requirements.md`](docs/Requirements.md#7-out-of-scope-this-iteration) with concrete extension hooks:

- Cloud sync / multi-user
- Color customization for trees / bushes / plants
- Frost-date / planting-window overlays per USDA zone
- Elevation data / heightmap layer
- Box-select rubber-band
- Undo / redo
- Mobile / touch gestures (pinch-zoom, two-finger pan)

## Contributing

This is a personal project, but if you'd like to extend it:

1. Read [`docs/Requirements.md`](docs/Requirements.md) — especially **§9 Change Discipline** (append-only enums, nullable additions, storage-key bumps for breaking changes, file-header rule).
2. `dotnet build` must produce **0 warnings, 0 errors**.
3. Update the requirements doc whenever you add a feature or move a boundary.

## License

Copyright © Garden Plot. All rights reserved.
