# Persistence Architecture

> **Principle:** Orthogonal data → orthogonal storage.
>
> Data that changes independently MUST be stored independently. Conflating
> independent concerns into one storage key forces every write of one concern
> to pay the cost of serializing and rewriting all the others — quietly turning
> a cheap UI interaction into a 500 KB IndexedDB write.

## Why this matters

GardenPlot is a client-only app. All user data lives in the browser via
IndexedDB. There is no server to absorb wasteful writes, and the wheel-tick
hot path on a precision touchpad fires at >100 Hz. Every microsecond and every
byte counts.

The pre-2026-Q1 layout stored an entire `PlotLibrary` (all plots × all shapes
× all takeoff × all groups + UI prefs + custom palette + custom catalog +
drawing sets + viewport state) inside a single `library/current` key. A wheel
zoom on a 5-plot canvas with 2700 plants per plot serialized 2.5 MB on every
tick. The UI was unusable. The fix wasn't faster JSON or debouncing — it was
*separating concerns that were never supposed to be coupled in the first place*.

## Current storage layout

| Key                            | Concern                                                            | Change frequency           | Size       |
|--------------------------------|--------------------------------------------------------------------|----------------------------|------------|
| `library/index`                | Cross-plot library metadata: UI prefs, custom palette/catalog, drawing sets, plot summaries | Item commits, plot CRUD | ~few KB    |
| `plot/{guid:N}`                | One plot's body: shapes, takeoff, drop groups, layer states, grid + background config       | Item commits within a plot | ~100–500 KB |
| `viewport/{guid:N}`            | One plot's view state: zoom, center X/Y                            | Wheel zoom, pan end        | ~80 bytes  |
| `gardenplot/images` (separate DB) | Background image blobs                                          | Image picker              | up to MBs each |
| `library/current` (legacy)     | Pre-split monolithic blob — read only during one-time migration    | n/a                        | n/a        |

Each row above represents data that changes on its own clock. The principle
is what gave us those rows; the rows were not the goal.

## Save event → storage write matrix

The user-facing rule (stated in code review on PR #119):

```
on mouse move        → don't save
on view change       → save only the viewport
on item change       → save the item
on image change      → save the image and scale
no "save everything" on a mouse event, except an explicit export button
```

| Event                         | Razor method        | Repository call(s)                                | Bytes written |
|-------------------------------|---------------------|---------------------------------------------------|---------------|
| Mouse move (no commit)        | (none)              | none                                              | 0             |
| Wheel zoom                    | `SaveViewportAsync` | `SaveViewportAsync(id, snapshot)`                 | ~80           |
| Pan end (mid-move or up)      | `SaveViewportAsync` | `SaveViewportAsync(id, snapshot)`                 | ~80           |
| Shape drag commit             | `SaveAsync`         | `SavePlotAsync(currentPlot)` + `SaveIndexAsync`   | ~100–500 KB + few KB |
| Item add/edit/delete commit   | `SaveAsync`         | `SavePlotAsync(currentPlot)` + `SaveIndexAsync`   | ~100–500 KB + few KB |
| Background image picked + applied | `SaveAsync`     | `SavePlotAsync(currentPlot)` + `SaveIndexAsync`   | ~100–500 KB + few KB |
| Plot delete                   | `DeleteCurrentPlot` | `DeletePlotAsync(id)` (removes plot + viewport)   | few KB        |
| Export / Import / Legacy migration | (file flow)    | `SaveLibraryAsync(library)` (atomic full rewrite + orphan prune) | full library  |

Note: viewport state is carried on `plot.Ui.Zoom`/`ViewCenterXFt`/`ViewCenterYFt`
in the in-memory model. It is persisted to *both* `plot/{id}` (whenever an
item commit fires) *and* `viewport/{id}` (every view change). On load, the
`viewport/{id}` value is layered over the plot's Ui — it is the authoritative
most-recent. The duplication is intentional: it makes export round-trips and
legacy import work without any extra plumbing, while keeping the hot path
cheap.

## Applying the principle in new code

Before introducing a new persisted field, ask:

1. **Does this change on a different clock than the data it sits next to?**
   If yes, it needs its own storage key.
2. **Is this user data, or is it derived/view state?**
   User data goes through `SaveAsync`. View state goes through a dedicated
   narrow save like `SaveViewportAsync`.
3. **Could a mouse-event trigger this write?**
   If yes, it MUST be in its own narrow key so the wheel-tick hot path stays
   tiny. Bundling it with anything chunkier is a regression in waiting.
4. **Is failure tolerable?**
   View state survives a missed save (load falls back to the plot body's
   last item-commit value). User data does not — `SaveAsync` has a
   localStorage fallback path; `SaveViewportAsync` swallows errors.

## Known violations / future work

These are concerns that currently live inside `PlotData.Ui` (the per-plot
`UiPreferences`) but arguably change on their own clock:

- **Panel positions** (`RulerPanelX/Y`, `InfoPanelX/Y`, `TakeoffPanelX/Y`,
  `CalibrationPanelX/Y`, `LayersPanelX/Y`) — change on panel drag, not on
  plot edit. Currently piggy-back on item-commit saves. Defer until we
  observe a real perf hit.
- **Takeoff view mode + cost column toggles** — change on user pref toggle,
  not on plot edit. Same story.
- **Layer states** (`LayerStates` dictionary on `PlotData`) — change on layer
  visibility/lock toggle. Currently goes through full `SaveAsync`. Could move
  to `layers/{plotId}` if layer toggles ever become a hot path.

These violations are documented here so they're handled deliberately rather
than re-discovered as bug reports.

## See also

- [`Models/PlotViewportState.cs`](../GardenPlotWeb/Models/PlotViewportState.cs)
- [`Services/Persistence/IPlotRepository.cs`](../GardenPlotWeb/Services/Persistence/IPlotRepository.cs)
- [`Services/Persistence/IndexedDbPlotRepository.cs`](../GardenPlotWeb/Services/Persistence/IndexedDbPlotRepository.cs)
- [`Services/Persistence/IClientKvStorage.cs`](../GardenPlotWeb/Services/Persistence/IClientKvStorage.cs)
- [`docs/payload-budget.md`](payload-budget.md) — first-paint Brotli budget
