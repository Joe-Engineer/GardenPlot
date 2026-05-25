# GardenPlot — Copilot / AI contributor instructions

Short rules. Concrete rationale. Linked deep references.

## Architectural principles

### 1. Orthogonal data → orthogonal storage

Data that changes on different clocks MUST live in different storage keys.
Conflating independent concerns into one storage key forces every write to
pay the cost of all the others.

**Concrete:** Before adding a persisted field to `PlotData`, `PlotLibrary`,
or `UiPreferences`, decide whether the new field changes on its own clock
(mouse-event hot path vs. user commit) and whether failure to save it is
tolerable (view state) or unacceptable (user data). If it changes on a
different clock from its neighbors, give it its own storage key and a
dedicated narrow save method on `IPlotRepository`.

**Full design:** [`docs/persistence-architecture.md`](../docs/persistence-architecture.md).

### 2. Save event → storage write matrix

The save policy is part of the architecture, not implementation detail:

| Event                     | What to save                          | What to call                |
|---------------------------|---------------------------------------|-----------------------------|
| Mouse move (no commit)    | nothing                               | (no save)                   |
| View change (zoom / pan)  | viewport snapshot only                | `SaveViewportAsync()`       |
| Item change               | the active plot                       | `SaveAsync()`               |
| Image change              | the image (blob store) + active plot  | `SaveAsync()`               |
| Plot create / delete      | active plot + index                   | `SaveAsync()` / `DeleteCurrentPlot` |
| Explicit export           | full library                          | `SaveLibraryAsync()`        |

**Never** call `SaveAsync()` (or anything heavier) directly inside a wheel,
pan, or pointer-move handler. Route view changes through `SaveViewportAsync`.

### 3. Local-first, user-owned

GardenPlot stores user data in the browser via IndexedDB. There is no
server in the loop. Choices that follow:

- The wheel-tick hot path fires >100 Hz on precision touchpads. Every byte
  counts; every fallback path matters.
- `SaveAsync` (user data) has a localStorage fallback for IDB failure.
- `SaveViewportAsync` (view state) swallows failures — viewport position
  is not user data.
- Background image blobs live in a separate IndexedDB (`gardenplot/images`)
  to avoid shared-ownership schema-version traps.

## Tactical rules

### Persistence

- Add new persisted state through `IPlotRepository`, not by reaching into
  `IndexedDbStorage` directly. Tests substitute an in-memory `IClientKvStorage`
  fake; bypassing the interface breaks that.
- `PlotData` mutations triggered by user commit go through `SaveAsync`.
- Per-plot save (`SavePlotAsync`) rewrites only that plot's storage key and
  updates only that plot's summary in the index. Other plots are untouched.
- `SaveLibraryAsync` is reserved for import, export, and one-time legacy
  migration. It rewrites the whole layout and prunes orphan keys. Never
  call it from a hot path.

### .NET / Blazor

- Target framework: .NET 10 (`net10.0`).
- All projects build with `-warnaserror`. The CI gate is strict.
- `CA1873` is enforced: wrap `LogInformation` / `LogDebug` calls whose
  arguments involve computation (`Stopwatch.Elapsed.TotalMilliseconds`,
  `Guid` formatting, etc.) inside `if (logger.IsEnabled(LogLevel.X))`.
- Per-shape XAML/SVG attribute computation in the WASM render loop is
  performance-critical — see `ShapeCohortRenderer` and follow the cohort
  pattern when introducing new per-shape work.

### First-paint payload

- Brotli budget for first paint: **3.00 MB**. Current: 2.62 MB
  (see [`docs/payload-budget.md`](../docs/payload-budget.md)).
- Before measuring `publish` output, clean `obj/Release`, `bin/Release`, and
  `publish/` (stale hashed `.wasm.br` files inflate the apparent total).
- Adding a NuGet dependency or moving code out of trimming-friendly patterns
  needs a payload-budget check in the PR.

### Testing

- All non-perf tests must pass: `dotnet test GardenPlot.slnx -c Release --filter "Category!=Performance"`.
- Performance tests (`[Trait("Category","Performance")]`) use **relative
  scaling** (N=2000 not more than 4× slower than N=500) — never absolute
  wall-clock thresholds. CI runners vary too much for absolute timing.
- Source-text guard tests (e.g. `SaveAsyncReconcileScopeGuardTests`) verify
  that hot-path code still routes through the cheap path. When you change
  a guarded code section, update the guard so it still asserts the
  invariant rather than deleting it.

### Pull requests

- Branch from a fresh `origin/main` for every PR.
- Frame the PR description honestly — what it does, what it does *not* do,
  and what follow-up work it leaves on the table.
- Reference linked issues. File follow-up issues for known violations or
  deferred work; don't leave them in the back of someone's head.

## Quick references

- Persistence architecture: `docs/persistence-architecture.md`
- Payload budget: `docs/payload-budget.md`
- Legacy data migration: `docs/migrating-legacy-data.md`
- Hosting: `docs/hosting.md`
- Requirements: `docs/Requirements.md`
