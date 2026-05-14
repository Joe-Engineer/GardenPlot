// Garden Plot interop. Loaded as an ES module via JSRuntime.
// Wheel: always preventDefault over the plot canvas so wheel controls stay app-specific.
export function attachWheel(el, dotnetRef) {
    const handler = (e) => {
        e.preventDefault();
        dotnetRef.invokeMethodAsync('OnWheelFromJs', e.deltaY, e.shiftKey, e.ctrlKey, e.altKey);
    };
    el.addEventListener('wheel', handler, { passive: false });
    return {
        dispose: () => el.removeEventListener('wheel', handler),
    };
}

export function panBy(el, dx, dy) {
    if (!el) return;
    el.scrollLeft -= dx;
    el.scrollTop -= dy;
}

export function getViewCenterFt(wrapEl, svgEl, pxPerFt, zoom) {
    if (!wrapEl || !svgEl || !pxPerFt || !zoom) return { x: 0, y: 0 };
    const wrapRect = wrapEl.getBoundingClientRect();
    const svgRect = svgEl.getBoundingClientRect();
    const scale = pxPerFt * zoom;
    const xPx = (wrapRect.left + (wrapRect.width / 2)) - svgRect.left;
    const yPx = (wrapRect.top + (wrapRect.height / 2)) - svgRect.top;
    return { x: xPx / scale, y: yPx / scale };
}

export function setViewCenterFt(wrapEl, svgEl, pxPerFt, zoom, xFt, yFt) {
    if (!wrapEl || !svgEl || !pxPerFt || !zoom) return;
    const current = getViewCenterFt(wrapEl, svgEl, pxPerFt, zoom);
    const scale = pxPerFt * zoom;
    wrapEl.scrollLeft += ((xFt - current.x) * scale);
    wrapEl.scrollTop += ((yFt - current.y) * scale);
}

// Captures the pointer to el so subsequent pointermove/up events fire on el
// regardless of where the cursor moves. Used for floating-panel dragging.
export function capturePointer(el, pointerId) {
    if (!el) return;
    try { el.setPointerCapture(pointerId); } catch { /* ignore */ }
}

// Returns the current viewport size (used to clamp dragged panels on screen).
export function viewportSize() {
    return { width: window.innerWidth, height: window.innerHeight };
}

// Returns the current top-left client position of an element.
export function elementClientPosition(el) {
    if (!el) return { x: 0, y: 0 };
    const r = el.getBoundingClientRect();
    return { x: r.left, y: r.top };
}

// Returns normalized (0..1) coordinates for a client point relative to an element's box.
export function normalizedPointInElement(el, clientX, clientY) {
    if (!el) return { x: 0, y: 0 };
    const r = el.getBoundingClientRect();
    if (!r.width || !r.height) return { x: 0, y: 0 };
    const x = Math.max(0, Math.min(1, (clientX - r.left) / r.width));
    const y = Math.max(0, Math.min(1, (clientY - r.top) / r.height));
    return { x, y };
}

// Serializes the SVG element to a PNG and triggers a browser download.
export async function exportPng(svgEl, filename, scale) {
    if (!svgEl) return;
    scale = scale || 2;
    const rect = svgEl.getBoundingClientRect();
    const xml = new XMLSerializer().serializeToString(svgEl);
    const dataUrl = 'data:image/svg+xml;base64,' + btoa(unescape(encodeURIComponent(xml)));
    const img = new Image();
    img.crossOrigin = 'anonymous';
    await new Promise((resolve, reject) => {
        img.onload = resolve;
        img.onerror = reject;
        img.src = dataUrl;
    });
    const canvas = document.createElement('canvas');
    canvas.width = Math.max(1, Math.round(rect.width * scale));
    canvas.height = Math.max(1, Math.round(rect.height * scale));
    const ctx = canvas.getContext('2d');
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
    canvas.toBlob((blob) => {
        if (!blob) return;
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename || 'garden-plot.png';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }, 'image/png');
}

// Opens a print preview window with the given SVG element. The user can save as PDF from the print dialog.
export function printSvg(svgEl, title) {
    if (!svgEl) return;
    const xml = new XMLSerializer().serializeToString(svgEl);
    const win = window.open('', '_blank');
    if (!win) return;
    win.document.write(`<!doctype html><html><head><title>${title || 'Garden Plot'}</title>
        <style>
          body { margin: 0; padding: 16px; font-family: sans-serif; }
          h1 { font-size: 14pt; margin: 0 0 12px 0; }
          svg { max-width: 100%; height: auto; }
          @media print { body { padding: 0; } h1 { display: none; } }
        </style>
      </head><body>
        <h1>${title || 'Garden Plot'}</h1>
        ${xml}
      </body></html>`);
    win.document.close();
    win.focus();
    setTimeout(() => { win.print(); }, 300);
}

// Triggers a browser download for an arbitrary text payload (used for CSV export).
export function downloadText(filename, text, mime) {
    const blob = new Blob([text], { type: mime || 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename || 'download.txt';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}

const gpDbName = 'gardenplot-db';
const gpStore = 'kv';
let gpDbPromise = null;

function openGardenPlotDb() {
    if (gpDbPromise) return gpDbPromise;
    gpDbPromise = new Promise((resolve, reject) => {
        const req = indexedDB.open(gpDbName, 1);
        req.onupgradeneeded = () => {
            const db = req.result;
            if (!db.objectStoreNames.contains(gpStore)) {
                db.createObjectStore(gpStore);
            }
        };
        req.onsuccess = () => resolve(req.result);
        req.onerror = () => reject(req.error || new Error('indexedDB open failed'));
    });
    return gpDbPromise;
}

// Durable client storage read. Returns string or null.
export async function idbGet(key) {
    if (!key) return null;
    try {
        const db = await openGardenPlotDb();
        const result = await new Promise((resolve, reject) => {
            const tx = db.transaction(gpStore, 'readonly');
            const store = tx.objectStore(gpStore);
            const req = store.get(key);
            req.onsuccess = () => resolve(req.result ?? null);
            req.onerror = () => reject(req.error || new Error('indexedDB get failed'));
        });
        const len = typeof result === 'string' ? result.length : (result == null ? 0 : -1);
        console.log('[gardenplot] idbGet', key, len === 0 ? 'EMPTY' : (len > 0 ? `${len} chars` : 'non-string'));
        return result;
    } catch (e) {
        console.warn('[gardenplot] idbGet failed', key, e);
        throw e;
    }
}

// Durable client storage write.
export async function idbSet(key, value) {
    if (!key) return;
    try {
        const db = await openGardenPlotDb();
        await new Promise((resolve, reject) => {
            const tx = db.transaction(gpStore, 'readwrite');
            const store = tx.objectStore(gpStore);
            const req = store.put(value ?? null, key);
            req.onsuccess = () => resolve();
            req.onerror = () => reject(req.error || new Error('indexedDB set failed'));
        });
        const len = typeof value === 'string' ? value.length : (value == null ? 0 : -1);
        console.log('[gardenplot] idbSet', key, len >= 0 ? `${len} chars` : 'non-string');
    } catch (e) {
        console.warn('[gardenplot] idbSet failed', key, e);
        throw e;
    }
}
