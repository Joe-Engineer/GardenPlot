# Payload budget

Garden Plot is a Blazor WebAssembly PWA, and **first-paint latency =
first-paint download size**. We hold ourselves accountable to a
**3 MB Brotli budget** for everything in `wwwroot/_framework/`
(the .NET runtime, app assemblies, ICU data, timezone data,
glue JS, and so on).

The budget is enforced on every `dotnet publish` by
[`GardenPlotWeb/Build/PayloadBudget.targets`](../GardenPlotWeb/Build/PayloadBudget.targets).
A `<UsingTask>` with `RoslynCodeTaskFactory` runs after `Publish`,
sums every `*.br` file under `_framework/`, and emits a friendly
`<Error>` if the total exceeds `$(WasmFirstPaintBudgetBytes)`.

```
Payload budget: 56 Brotli files under _framework/ total
2,741,705 bytes (2.61 MB); budget 3,145,728 bytes (3.00 MB).
```

## Why Brotli, not gzip, not raw

The Blazor SDK ships pre-compressed `*.br` (Brotli) and `*.gz` (gzip)
companions for every asset. Modern browsers prefer Brotli, so that's
the realistic first-paint payload for a fresh user with a warm CDN.
We don't count raw `.wasm` / `.dll` bytes because they're never
shipped over the wire.

## What's currently in the budget (May 2026)

| Asset | KB | Notes |
|-------|----|-------|
| `dotnet.native.wasm.br` | ~912 | Mono runtime, AOT'd interpreter |
| `System.Private.CoreLib.wasm.br` | ~573 | BCL |
| `NetTopologySuite.wasm.br` | ~295 | Polygon math (clipping, area). Used heavily by the canvas. |
| `GardenPlotWeb.wasm.br` | ~263 | App code |
| `System.Private.Xml.wasm.br` | ~135 | Pulled in by NetTopologySuite serialization |
| `System.Text.Json.wasm.br` | ~129 | Plot library round-trip |
| `System.Linq.Expressions.wasm.br` | ~110 | Pulled in by JSON serialization |
| `Microsoft.AspNetCore.Components.wasm.br` | ~96 | Blazor framework |
| (everything else) | ~228 | Glue JS, Components.WebAssembly, other BCL leaves |
| **Total** | **~2,741** | **3,145,728 byte budget, 390 KB headroom** |

## Knobs we've already turned

- `InvariantGlobalization=true` — removes the three ICU `.dat` files
  (~600 KB combined: CJK, no-CJK, EFIGS). Date display falls back to
  invariant culture (always English month names); task sorting falls
  back to invariant ordinal comparison. Safe because we don't ship
  with a localized UI.
- `BlazorEnableTimeZoneSupport=false` — removes bundled IANA
  timezone data (~30 KB). Safe because no code calls
  `TimeZoneInfo.FindSystemTimeZoneById`; `DateTime.ToLocalTime()`
  works off the browser's UTC offset and doesn't need the database.
- `PublishTrimmed=true` — default for WASM Release builds. The
  trimmer drops unreachable types and methods. We tolerate a couple
  of unavoidable trimmer warnings from NetTopologySuite reflection.

## If the budget fails

The error message lists the available knobs in order of impact. Start
with the cheapest:

1. **Inspect the publish output.** Run `dotnet publish -c Release`
   and `Get-ChildItem publish/wwwroot/_framework/*.br | Sort-Object Length -Descending | Select -First 10`
   to find the biggest contributors.
2. **Check for a new transitive dependency.** A single accidentally
   added `[Pack]` or transitive reference (e.g. ImageSharp,
   System.Reactive) can balloon the runtime payload by hundreds of KB.
   Was your last change supposed to pull that in?
3. **Add a `[JsonSerializable]` source-gen context** for hot paths
   (e.g. `PlotLibrary`, `PlotData`). This can shave 30-80 KB by
   reducing `System.Linq.Expressions` usage from `System.Text.Json`.
4. **Lazy-load NetTopologySuite.** It's 295 KB and is only needed when
   the user opens a plot with polygons. The Blazor SDK supports
   `LazyAssemblies` for this kind of split. Track as follow-up — the
   work isn't trivial because NTS types are touched at app startup
   through DI.
5. **Raise the budget with documentation.** As a last resort, edit
   `<WasmFirstPaintBudgetBytes>` in `GardenPlotWeb.csproj` and add
   an "as of YYYY-MM-DD" note here explaining what justified the
   raise. Don't normalize budget creep.

## Why a budget at all?

- A 3 MB cold-cache load over 5 Mbit DSL is ~4.8 seconds before
  the user sees anything interactive. Above that, abandonment
  climbs quickly.
- The PWA precache fetches every framework asset on first
  install, so an oversized first-paint is also an oversized
  install. Bad mobile UX.
- Without a gate, payload regresses silently. The .NET WASM
  toolchain is good but not magic; it pulls everything you
  reference, including transitive metadata.

If you ever look at a publish output that's over budget, the
build will fail with the file list. Investigate before raising
the number.
