# jsPDF + jspdf-autotable (bundled)

These libraries are bundled locally so the GardenPlot WASM app can generate PDFs
fully offline once cached.

## Versions

| Library             | Version | License | Source                                                           |
|---------------------|---------|---------|------------------------------------------------------------------|
| jsPDF               | 2.5.2   | MIT     | https://github.com/parallax/jsPDF                                |
| jspdf-autotable     | 3.8.4   | MIT     | https://github.com/simonbengtsson/jsPDF-AutoTable                |

## Files

| File                              | Size      | SHA-256                                                          |
|-----------------------------------|-----------|------------------------------------------------------------------|
| jspdf.umd.min.js                  | 365,730 B | 85BA2CC3FF858A20FA49FE6E457BEC863EA40B55A9F3725E58A940E62F6F61A4 |
| jspdf.plugin.autotable.min.js     | 38,960 B  | 2223830CF9A1EC85AF014CC71B37C1B1EB566F3D18B2AB8071E96AF822C58BDB |

## Loading

These are **lazy-loaded** by `js/gardenplot.js` on the first PDF export click,
not by `index.html`, to keep first-paint payload small. See `ensurePdfLibs()`
in `gardenplot.js`.

## Updating

1. Replace the .min.js files with the new versions.
2. Verify `window.jspdf.jsPDF` and `doc.autoTable` are still the public API
   (no breaking changes).
3. Update version + size + SHA-256 in this README.

## License

Both libraries are MIT-licensed. See `jspdf.LICENSE` and
`jspdf-autotable.LICENSE` in this folder.