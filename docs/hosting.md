# Hosting Garden Plot

Garden Plot is a pure **Blazor WebAssembly** static site. There's no
ASP.NET Core server, no database, no API. You can host the contents of
`GardenPlotWeb/bin/Release/net10.0/publish/wwwroot/` on any HTTPS static
host that can do a single rewrite rule.

## Tested hosts

- **Azure Static Web Apps** — drop `staticwebapp.config.json` next to
  `index.html`; SWA's free tier is more than enough.
- **GitHub Pages** — works, but the SPA fallback requires the
  [`404.html` trick](https://github.com/rafgraph/spa-github-pages). Use
  a sub-path base href.
- **IIS / Azure App Service** — `web.config` ships in the publish
  output and provides the SPA fallback + MIME types.
- **nginx** — add the SPA fallback yourself:
  `try_files $uri $uri/ /index.html;`

## Required headers / MIME types

Every host must serve:

| Extension | MIME type |
|-----------|-----------|
| `.wasm` | `application/wasm` |
| `.dat` | `application/octet-stream` |
| `.blat` | `application/octet-stream` |
| `.webmanifest` | `application/manifest+json` |
| `.br` | (mirror the underlying file's type) |

Most modern hosts already do this; IIS gets configured by `web.config`.

## Required hosting capabilities

- **HTTPS.** Service workers and IndexedDB persistence work over HTTPS
  or `localhost` only.
- **SPA fallback rewrite.** All `*` requests that don't match a real
  file or directory must be rewritten to `/index.html` so the Blazor
  router can pick up deep links (`/garden-plot/<guid>`, etc.).
- **Brotli passthrough.** The publish output ships `*.br` companions for
  every framework asset. Hosts that recompress assets in flight (some
  CDNs) can corrupt them. Configure your CDN/host to serve `.br`
  bytes as-is when the request has `Accept-Encoding: br`.

## Recommended headers (security)

These are not strictly required but harden the install:

```
Content-Security-Policy: default-src 'self'; img-src 'self' data: blob:;
  connect-src 'self' https://en.wikipedia.org;
  script-src 'self' 'wasm-unsafe-eval';
  style-src 'self' 'unsafe-inline'
Permissions-Policy: camera=(), microphone=(), geolocation=()
X-Content-Type-Options: nosniff
Referrer-Policy: strict-origin-when-cross-origin
Strict-Transport-Security: max-age=31536000; includeSubDomains
```

The `'wasm-unsafe-eval'` directive is required for Mono's WASM JIT.
The `connect-src` exception for `wikipedia.org` is only needed if you
keep the Wikipedia summary integration enabled (settings ▸ "Show
Wikipedia summaries for plants").

## Cache strategy

Don't cache `index.html`. Long-cache everything under `_framework/`
because the SDK hashes the filenames (`dotnet.native.<hash>.wasm.br`).
A typical setup:

```
/                       cache: no-cache
/_framework/*           cache: public, max-age=31536000, immutable
/manifest.webmanifest   cache: no-cache
/service-worker.js      cache: no-cache, no-store
/icon-*.png             cache: public, max-age=31536000
*.png, *.jpg, *.svg     cache: public, max-age=86400
```

The service worker still works correctly even with aggressive caching
because it sees hashed asset names and the assets-manifest file is
`no-cache`.

## Deployment workflow

```pwsh
# Publish (also runs the payload-budget gate)
dotnet publish GardenPlotWeb -c Release -o publish

# The static site is under publish/wwwroot/
# Deploy that directory to your host.
```

If the budget gate fails, read `docs/payload-budget.md` for guidance.
