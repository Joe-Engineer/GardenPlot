// Development service worker: intentionally a passthrough so DevServer reloads
// always reach the wire. The published build replaces this with
// service-worker.published.js (precache + offline fallback) via the
// Microsoft.NET.Sdk.BlazorWebAssembly service-worker pipeline.
self.addEventListener('fetch', () => { });
