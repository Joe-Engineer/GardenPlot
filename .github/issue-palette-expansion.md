## Background

Our `PaletteCatalog` covers the structural side of garden design well (bed kits, trees, bushes, focal points, edging, ground-cover materials and surfaces). The plant lists themselves, however, are noticeably thinner and less consistently organized than what gardeners encounter in mainstream nursery and seed-catalog taxonomies. In particular:

- Vegetables and culinary herbs are limited to ~25 and ~8 entries respectively, missing many staples (brassicas beyond cabbage/broccoli/cauliflower, root crops, alliums beyond onion/garlic, cucurbits beyond squash/cucumber, greens beyond lettuce/spinach/kale, stalk crops, etc.).
- Flowering annuals are limited to a small companion-planting set (marigold, nasturtium, sunflower, borage, calendula, zinnia). Many widely sold annuals are absent (petunia, snapdragon, cosmos, pansy/viola, larkspur, stock, alyssum, celosia, strawflower, scabiosa, sweet pea, nigella).
- The `Vines` categories (`VinesEdible`, `VinesOrnamental`) exist in `PaletteCategory` but the `Plants` array contains **no items** with `vine-edible` or `vine-ornamental` traits, so those tabs are empty.
- Shrubs are split only by edible / flowering / evergreen. There is no "deciduous foliage shrub" bucket (smokebush, ninebark, weigela, witch hazel, beautyberry, fothergilla) and no "dwarf conifer" bucket (dwarf Mugo, dwarf Alberta spruce, Hinoki cypress, Boulevard cypress).
- Trees split only by fruit / nut / flower / shade / evergreen / foliage. There is no "ornamental form" bucket for weeping, columnar, and topiary specimens, which most outdoor-plant catalogs treat as a first-class group.
- Berries are not sub-typed by growth habit (cane vs. bush vs. groundcover vs. unusual fruit), which matters for layout and trellising.
- Bulbs do not distinguish spring-planted vs. fall-planted.
- Cover crops lack a sub-trait (legume / grass / brassica / forb), even though our `cover-crop` set is otherwise solid.
- `PaletteItem` records sun and water for **Plants only**. Trees and shrubs carry no sun/water metadata, so we cannot filter "shade tree for partial shade" or "drought-tolerant evergreen shrub."
- We have no lifecycle (annual / biennial / perennial / tender perennial), no hardiness zone range, and no boolean flags for: container-friendly, native, pollinator-friendly, deer-resistant, deciduous, cut-flower, culinary, tea/medicinal, edible.

These omissions limit our ability to support common workflows: cottage/cutting gardens, pollinator gardens, edible landscapes, foundation plantings, mixed shrub borders, container plantings, and crop-rotation planning.

## Goals

1. Bring the **breadth** of our plant items closer to what gardeners expect from a general-purpose catalog.
2. Refine the **taxonomy** so each item lands in a meaningful, filterable category.
3. Enrich `PaletteItem` **metadata** so categories can be derived from flags (lifecycle, zone, native regions, pollinator, container, deciduous, edible, cut-flower) rather than from fragile string traits.
4. Keep the change additive and backward compatible: existing item codes, traits, and categories continue to work.

## Proposed changes

### 1. Extend `PaletteItem` with optional metadata

Add nullable fields (so existing records continue to compile):

- `Lifecycle` (`annual | biennial | perennial | tender-perennial`)
- `MinZone`, `MaxZone` (USDA hardiness range)
- `Deciduous` (bool?) — meaningful for trees/shrubs
- `NativeRegions` (set of region codes) — regions where the plant grows natively. "Native" is not a single boolean: a species native to coastal southern Florida is not native to the western Cascades of Washington. Suggested initial region vocabulary uses USDA/EPA Level III ecoregions or a coarser practical set such as: `pacific-northwest-west`, `pacific-northwest-east`, `california-coastal`, `california-interior`, `southwest-desert`, `rocky-mountain`, `great-plains-north`, `great-plains-south`, `midwest`, `northeast`, `mid-atlantic`, `southeast-piedmont`, `southeast-coastal-plain`, `florida-peninsula`, `gulf-coast`, `appalachian`, `boreal`. Filters then ask "native to my region" rather than "native (anywhere)."
- `PollinatorFriendly` (bool?)
- `DeerResistant` (bool?)
- `ContainerFriendly` (bool?)
- `CutFlower` (bool?)
- `Edible` (bool?)
- `Culinary`, `Tea`, `Medicinal` (bools, complement existing trait strings)
- `SunSecondary` / `WaterSecondary` for trees/shrubs (extend the sun/water columns to all kinds, not just `Plant`)

### 2. New / promoted categories in `PaletteCategory`

- `TreesOrnamentalForm` — weeping, columnar, topiary specimens (weeping cherry, weeping Japanese maple, columnar hornbeam, columnar oak, topiary boxwood, espalier apple).
- `ShrubsDeciduous` — smokebush, ninebark, weigela, witch hazel, beautyberry, mock orange, fothergilla, oakleaf hydrangea.
- `ShrubsDwarfConifer` — dwarf Mugo pine, dwarf Alberta spruce, Hinoki cypress, Boulevard cypress, dwarf blue spruce.
- `BerriesCane`, `BerriesBush`, `BerriesUnusual` — split the current `ShrubsBerry` set by growth habit; add unusual fruit (goji, sea buckthorn, lingonberry, honeyberry, aronia).
- `BulbsSpringPlanted`, `BulbsFallPlanted` — replace the single `Bulbs` bucket, or keep `Bulbs` and add a `PlantingSeason` flag.
- `CoverCropsLegume`, `CoverCropsGrass`, `CoverCropsBrassica`, `CoverCropsForb` — sub-split the existing cover-crop set.

### 3. Populate the empty `Vines*` categories

Add a `vine-edible` set (grape, hardy kiwi, hops, passionfruit, hardy kiwi 'Issai') and a `vine-ornamental` set (clematis, climbing rose, wisteria, honeysuckle, climbing hydrangea, jasmine, morning glory, hyacinth bean, mandevilla, sweet pea as annual vine).

### 4. Expand `Plants` breadth

**Vegetables** — add brassicas (brussels sprouts, collards, kohlrabi, bok choy, arugula, mustard greens), roots (parsnip, turnip, rutabaga, celeriac, salsify), alliums (leek, shallot, scallion, walking onion), cucurbits (melon, watermelon, gourds), nightshade (tomatillo, ground cherry), greens (Swiss chard, sorrel, endive, radicchio, claytonia/miner's lettuce), stalks (rhubarb, fennel bulb, celery, artichoke, cardoon), grains/pseudo-grains (quinoa, amaranth, sorghum-grain). Tag each with a vegetable family for crop-rotation tooling.

**Culinary herbs** — add marjoram, tarragon, savory, lemongrass, lemon verbena, bay laurel, lovage, hyssop, stevia, anise hyssop (culinary), fennel (herb form), cumin, coriander-as-herb (alongside cilantro).

**Annual flowers** — add petunia, snapdragon, cosmos, pansy/viola, larkspur, stock, alyssum, celosia, strawflower, scabiosa, nigella, ageratum, gomphrena, cleome, salvia (annual), bachelor button.

**Perennial flowers** — add hellebore, blanket flower (gaillardia), sedum 'Autumn Joy', penstemon (ornamental form distinct from the native), hardy geranium, achillea (yarrow ornamental cultivars), foxglove (digitalis), monkshood, liatris.

**Bulbs** — add muscari, freesia, camas, snowdrop, fritillaria varieties, narcissus varieties, allium ornamental varieties; tag each with `PlantingSeason`.

**Fruit trees** — add jujube, loquat, quince, medlar, cold-hardy banana, Asian pear, Asian persimmon (distinct from American), dwarf mulberry.

### 5. Reclassify a few existing items

- Move `Strawberry` and `Wild Strawberry` into a `BerriesGroundcover` set (already partially present on the surface side).
- Promote `Raspberry`, `Blackberry`, `Boysenberry`, `Loganberry`, `Tayberry` from `Bushes` into `BerriesCane`.
- Keep `Blueberry`, `Currant`, `Gooseberry`, `Elderberry`, `Honeyberry`, `Aronia`, `Cranberry (Highbush)` in `BerriesBush`.
- Tag `Goji`, `Sea Buckthorn`, `Lingonberry` as `BerriesUnusual`.

### 6. Tests and UI

- Extend `GardenPlot.Tests/PaletteCatalogTests.cs` with assertions that each new category returns a non-empty list and that the new metadata round-trips through `For(PaletteCategory)` and `CategoryFor(PaletteItem)`.
- Surface the new flags (lifecycle, container, native regions, pollinator, cut-flower, edible, deciduous) as filter toggles in the palette UI so users can do queries like "container-friendly perennial for partial shade, native to the Pacific Northwest" without us having to invent a new category for each combination.

## Non-goals

- No change to the rendering pipeline, texture keys, or material categories.
- No change to bed-kit, focal-point, edging, or ground-cover material lists.
- No automated import from external catalogs; entries are still authored by hand in `PaletteCatalog.cs`.

## Acceptance criteria

- `Plants`, `Bushes`, and `Trees` arrays each grow to roughly double their current entry count, with all new entries carrying lifecycle and (where applicable) zone range, sun, and water.
- `PaletteCategory.VinesEdible` and `PaletteCategory.VinesOrnamental` each return at least 8 items.
- `PaletteCategory` gains `TreesOrnamentalForm`, `ShrubsDeciduous`, `ShrubsDwarfConifer`, `BerriesCane`, `BerriesBush`, `BerriesUnusual`, and cover-crop sub-categories, each returning a non-empty list.
- All `PaletteCatalogTests` pass, including new tests asserting the categorization changes and the new metadata.
- The palette UI exposes at least one new filter flag (suggestion: `Container-friendly`) end-to-end as a proof of the metadata expansion.
