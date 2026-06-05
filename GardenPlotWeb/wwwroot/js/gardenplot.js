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

// Cheap status-bar X/Y updater. The GardenPlot page calls this from the
// "idle" branch of OnPointerMove so we can show fresh cursor coordinates
// without forcing a Blazor parent re-render (which, on a 2000+ shape canvas,
// re-runs viewport culling + cohort fingerprinting on every pointer event
// even though the canvas content itself didn't change). The Blazor render
// path still owns the spans on real renders; this just patches the text
// content in-between. Refs #128.
export function updateStatusPos(xText, yText) {
    const xEl = document.getElementById('garden-status-x');
    if (xEl) xEl.textContent = xText;
    const yEl = document.getElementById('garden-status-y');
    if (yEl) yEl.textContent = yText;
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

// Pushes the visible scroll window of the canvas back to Blazor so the render
// path can cull off-screen shapes. Reports raw px values; C# applies its own
// zoom + viewBox offset to convert to plot-feet.
export function attachViewport(wrapEl, dotnetRef) {
    if (!wrapEl) return { dispose: () => { } };

    let pending = false;
    const push = () => {
        pending = false;
        dotnetRef.invokeMethodAsync(
            'OnViewportFromJs',
            wrapEl.scrollLeft,
            wrapEl.scrollTop,
            wrapEl.clientWidth,
            wrapEl.clientHeight);
    };
    const schedule = () => {
        if (pending) return;
        pending = true;
        requestAnimationFrame(push);
    };

    // Initial sync so culling kicks in before the user pans.
    schedule();

    wrapEl.addEventListener('scroll', schedule, { passive: true });
    const resizeObserver = new ResizeObserver(schedule);
    resizeObserver.observe(wrapEl);

    return {
        dispose: () => {
            wrapEl.removeEventListener('scroll', schedule);
            resizeObserver.disconnect();
        },
    };
}

// Captures the pointer to el so subsequent pointermove/up events fire on el
// regardless of where the cursor moves. Used for floating-panel dragging.
export function capturePointer(el, pointerId) {
    if (!el) return;
    try { el.setPointerCapture(pointerId); } catch { /* ignore */ }
}

// Multi-touch gesture handler for mobile users: two-finger pan and pinch-to-zoom.
// Single-touch events pass through to Blazor pointer handlers (used for drawing).
// When a 2nd touch lands, any in-progress draft is cancelled via dotnetRef so the
// user doesn't accidentally draw while gesturing, and subsequent move/up events
// from those two touches are intercepted at the capture phase so Blazor never sees them.
export function attachTouchGestures(canvasEl, wrapEl, dotnetRef) {
    if (!canvasEl || !wrapEl) return { dispose: () => { } };

    const touches = new Map(); // pointerId -> { x, y }
    let gestureActive = false;
    let startDist = 0;
    let startZoom = 1;
    let lastMidX = 0, lastMidY = 0;
    let currentZoom = 1;
    let pendingZoom = null;
    let zoomRafScheduled = false;

    const isTouch = (e) => e.pointerType === 'touch';

    const midpoint = () => {
        let sx = 0, sy = 0, n = 0;
        for (const p of touches.values()) { sx += p.x; sy += p.y; n++; }
        return n ? { x: sx / n, y: sy / n } : { x: 0, y: 0 };
    };
    const distance = () => {
        if (touches.size < 2) return 0;
        const it = touches.values();
        const a = it.next().value;
        const b = it.next().value;
        return Math.hypot(a.x - b.x, a.y - b.y);
    };

    const flushZoom = () => {
        zoomRafScheduled = false;
        if (pendingZoom == null) return;
        const z = pendingZoom;
        pendingZoom = null;
        try { dotnetRef.invokeMethodAsync('SetZoomFromJs', z); } catch { /* circuit gone */ }
    };
    const requestZoom = (z) => {
        pendingZoom = z;
        currentZoom = z;
        if (!zoomRafScheduled) {
            zoomRafScheduled = true;
            requestAnimationFrame(flushZoom);
        }
    };

    const startGesture = () => {
        gestureActive = true;
        startDist = distance();
        // Read the latest Blazor-side zoom so pinch deltas compose correctly even
        // if the user has changed zoom via the toolbar/wheel between gestures.
        const dz = parseFloat(canvasEl.dataset.zoom);
        currentZoom = isFinite(dz) && dz > 0 ? dz : currentZoom;
        startZoom = currentZoom;
        const m = midpoint();
        lastMidX = m.x; lastMidY = m.y;
        try { dotnetRef.invokeMethodAsync('CancelActiveDragFromJs'); } catch { /* ignore */ }
    };

    const endGesture = () => {
        gestureActive = false;
        startDist = 0;
    };

    const onDown = (e) => {
        if (!isTouch(e)) return;
        touches.set(e.pointerId, { x: e.clientX, y: e.clientY });
        if (touches.size >= 2 && !gestureActive) {
            startGesture();
            e.stopImmediatePropagation();
            e.preventDefault();
        } else if (gestureActive) {
            e.stopImmediatePropagation();
        }
    };
    const onMove = (e) => {
        if (!isTouch(e)) return;
        if (!touches.has(e.pointerId)) return;
        touches.set(e.pointerId, { x: e.clientX, y: e.clientY });
        if (!gestureActive) return;

        e.stopImmediatePropagation();
        e.preventDefault();

        const m = midpoint();
        // Two-finger pan: instant scroll, no server roundtrip.
        wrapEl.scrollLeft -= (m.x - lastMidX);
        wrapEl.scrollTop -= (m.y - lastMidY);
        lastMidX = m.x; lastMidY = m.y;

        // Pinch zoom: throttled to one server call per animation frame.
        const d = distance();
        if (startDist > 0 && d > 0) {
            const ratio = d / startDist;
            const next = Math.max(0.25, Math.min(6, startZoom * ratio));
            if (Math.abs(next - currentZoom) > 0.005) {
                requestZoom(next);
            }
        }
    };
    const onUp = (e) => {
        if (!isTouch(e)) return;
        if (gestureActive) e.stopImmediatePropagation();
        touches.delete(e.pointerId);
        if (touches.size < 2 && gestureActive) {
            endGesture();
        }
    };

    // Capture phase so Blazor's bubble-phase handlers never see these events while gesturing.
    canvasEl.addEventListener('pointerdown', onDown, { capture: true, passive: false });
    canvasEl.addEventListener('pointermove', onMove, { capture: true, passive: false });
    canvasEl.addEventListener('pointerup', onUp, { capture: true, passive: false });
    canvasEl.addEventListener('pointercancel', onUp, { capture: true, passive: false });
    canvasEl.addEventListener('pointerleave', onUp, { capture: true, passive: false });

    return {
        setZoom: (z) => { currentZoom = z; },
        dispose: () => {
            canvasEl.removeEventListener('pointerdown', onDown, { capture: true });
            canvasEl.removeEventListener('pointermove', onMove, { capture: true });
            canvasEl.removeEventListener('pointerup', onUp, { capture: true });
            canvasEl.removeEventListener('pointercancel', onUp, { capture: true });
            canvasEl.removeEventListener('pointerleave', onUp, { capture: true });
        },
    };
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

export async function getImageDimensions(url) {
    if (!url) return { width: 0, height: 0 };
    const img = new Image();
    img.decoding = 'async';
    await new Promise((resolve, reject) => {
        img.onload = resolve;
        img.onerror = reject;
        img.src = url;
    });
    return {
        width: img.naturalWidth || 0,
        height: img.naturalHeight || 0,
    };
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

export function exportContainerSvgPng(containerEl, filename, scale) {
    if (!containerEl) return;
    const svgEl = containerEl.querySelector('svg');
    if (!svgEl) return;
    return exportPng(svgEl, filename, scale);
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

export function confirmAction(message) {
    return window.confirm(message || 'Are you sure?');
}

// ===== PDF export (Issue #6) =====
// jsPDF + jspdf-autotable are bundled in wwwroot/lib/jspdf/ and lazy-loaded
// on first export click to keep first-paint payload small.

let pdfLibsPromise = null;

function loadScriptOnce(src) {
    return new Promise((resolve, reject) => {
        const existing = document.querySelector(`script[data-gp-lib="${src}"]`);
        if (existing) { resolve(); return; }
        const s = document.createElement('script');
        s.src = src;
        s.async = false;
        s.dataset.gpLib = src;
        s.onload = () => resolve();
        s.onerror = () => reject(new Error(`Failed to load ${src}`));
        document.head.appendChild(s);
    });
}

async function ensurePdfLibs() {
    if (window.jspdf && window.jspdf.jsPDF) return;
    if (pdfLibsPromise) return pdfLibsPromise;
    pdfLibsPromise = (async () => {
        await loadScriptOnce('lib/jspdf/jspdf.umd.min.js');
        await loadScriptOnce('lib/jspdf/jspdf.plugin.autotable.min.js');
        if (!window.jspdf || !window.jspdf.jsPDF) {
            pdfLibsPromise = null;
            throw new Error('jsPDF failed to load (window.jspdf.jsPDF missing)');
        }
    })().catch((err) => {
        pdfLibsPromise = null;
        throw err;
    });
    return pdfLibsPromise;
}

// Reads a Blob into a data: URL via FileReader. Used to inline blob: and
// fetched same-origin image references into the serialized SVG so the
// rasterizer's <img> tag can render them from a data: URL document.
function blobToDataUrl(blob) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(String(reader.result));
        reader.onerror = () => reject(new Error('FileReader failed reading blob'));
        reader.readAsDataURL(blob);
    });
}

// Walks every <image> in the cloned SVG and replaces any href / xlink:href
// that points at a blob:, http(s):, or root-relative URL with an inline
// data: URL. This is required because when the SVG is loaded via
// `new Image().src = "data:image/svg+xml;..."` the document context is a
// data: URL with an opaque origin — it cannot resolve blob: URLs (those are
// scoped to the original document) and has no base URL for relative paths.
// Any href that can't be inlined is removed so the SVG parses cleanly
// instead of firing onerror on the outer <img>.
const SVG_NS = 'http://www.w3.org/2000/svg';
const XLINK_NS = 'http://www.w3.org/1999/xlink';
async function inlineEmbeddedImages(svgClone) {
    const images = Array.from(svgClone.getElementsByTagName('image'));
    if (images.length === 0) {
        return;
    }
    await Promise.all(images.map(async (el) => {
        const href = el.getAttribute('href')
            || el.getAttributeNS(XLINK_NS, 'href')
            || el.getAttribute('xlink:href');
        if (!href || href.startsWith('data:')) {
            return;
        }
        try {
            const response = await fetch(href);
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }
            const blob = await response.blob();
            const dataUrl = await blobToDataUrl(blob);
            // Strip every form (literal, prefixed, namespaced) before re-setting
            // so we don't leave a stale blob: URL behind. setAttribute('xlink:href')
            // and setAttributeNS(XLINK_NS, 'xlink:href') create distinct attributes
            // that can coexist, and the literal one is what client-images.js writes.
            el.removeAttribute('href');
            el.removeAttribute('xlink:href');
            el.removeAttributeNS(XLINK_NS, 'href');
            el.setAttribute('href', dataUrl);
            el.setAttributeNS(XLINK_NS, 'xlink:href', dataUrl);
        } catch (e) {
            console.warn('rasterizeSvgToDataUrl: dropping unreachable image href', href, e);
            el.removeAttribute('href');
            el.removeAttribute('xlink:href');
            el.removeAttributeNS(XLINK_NS, 'href');
        }
    }));
}

// Best-effort extraction of a human-readable string from a value that may be
// an Error, a DOM Event (e.g. from img.onerror), or anything else. Plain
// String(event) collapses to "[object Event]" which is useless.
function describeRasterError(err) {
    if (!err) {
        return 'unknown error';
    }
    if (err.message) {
        return err.message;
    }
    if (typeof Event !== 'undefined' && err instanceof Event) {
        const target = err.target || {};
        const src = target.src ? ` (src=${target.src.slice(0, 80)})` : '';
        return `image failed to load${src}`;
    }
    return String(err);
}

// Rasterizes an SVG element to a PNG data URL at a target pixel width,
// preserving aspect ratio. Used for embedding the plot snapshot into the PDF.
//
// When opts.fitToContent is true (default for PDF export), the snapshot uses
// the actual SVG content bounding box (svgEl.getBBox()) instead of the current
// viewport rect — so shapes panned off-screen still appear in the PDF.
// opts.paddingFrac (default 0.04) adds breathing room around the content.
async function rasterizeSvgToDataUrl(svgEl, targetWidthPx, opts) {
    const fitToContent = opts ? opts.fitToContent !== false : true;
    const paddingFrac = opts && typeof opts.paddingFrac === 'number' ? opts.paddingFrac : 0.04;

    let xml;
    let aspect;

    if (fitToContent) {
        // getBBox returns the bounding box of all rendered descendants in the SVG's
        // user-coordinate space, regardless of current pan/zoom or viewport scroll.
        let bbox;
        try {
            bbox = svgEl.getBBox();
        } catch (err) {
            bbox = null;
        }
        if (!bbox || !bbox.width || !bbox.height) {
            // Fall back to the viewport rect if the SVG has no rendered content (or
            // getBBox isn't supported, e.g. in some test environments).
            return rasterizeSvgToDataUrl(svgEl, targetWidthPx, { fitToContent: false });
        }

        // Inflate the content bbox by paddingFrac on each side so shapes don't
        // butt up against the page edge in the PDF.
        const pad = Math.max(bbox.width, bbox.height) * paddingFrac;
        const vbX = bbox.x - pad;
        const vbY = bbox.y - pad;
        const vbW = bbox.width + pad * 2;
        const vbH = bbox.height + pad * 2;
        aspect = vbH / vbW;

        // Clone deep so we can retarget the viewBox without mutating the live SVG.
        // The clone inherits all child elements (shapes, labels, client images) —
        // pan/zoom transforms applied by Blazor on the root <g> are still in there,
        // but the new viewBox now covers the full content extent.
        const clone = svgEl.cloneNode(true);
        clone.setAttribute('viewBox', `${vbX} ${vbY} ${vbW} ${vbH}`);
        // Strip width/height pixel attributes so the SVG scales to whatever size
        // we feed it via the <img> element.
        clone.removeAttribute('width');
        clone.removeAttribute('height');
        // preserveAspectRatio default is xMidYMid meet — letterbox if needed.
        clone.setAttribute('preserveAspectRatio', 'xMidYMid meet');
        // Inline any embedded <image> hrefs (blob: URLs from IndexedDB-backed
        // client images, or same-origin /paths) so the serialized SVG is
        // self-contained when loaded from a data: URL document.
        await inlineEmbeddedImages(clone);
        xml = new XMLSerializer().serializeToString(clone);
    } else {
        const rect = svgEl.getBoundingClientRect();
        if (!rect.width || !rect.height) {
            throw new Error('SVG element has no rendered size');
        }
        aspect = rect.height / rect.width;
        // Clone the live SVG so we can rewrite hrefs without mutating the DOM.
        const clone = svgEl.cloneNode(true);
        await inlineEmbeddedImages(clone);
        xml = new XMLSerializer().serializeToString(clone);
    }

    const w = Math.max(1, Math.round(targetWidthPx));
    const h = Math.max(1, Math.round(targetWidthPx * aspect));
    // Guard against runaway memory: a plot with many large textures or a
    // 30 MB background image becomes a multi-hundred-MB serialized SVG after
    // base64 inlining. Fail with a clear message instead of OOMing the tab.
    const MAX_SERIALIZED_XML_BYTES = 32 * 1024 * 1024;
    if (xml.length > MAX_SERIALIZED_XML_BYTES) {
        throw new Error(`snapshot too large (${Math.round(xml.length / 1024 / 1024)} MB) — try removing large background or texture images`);
    }
    const dataUrl = 'data:image/svg+xml;base64,' + btoa(unescape(encodeURIComponent(xml)));
    const img = new Image();
    // No crossOrigin: the source is a data: URL — CORS doesn't apply, and
    // setting crossOrigin='anonymous' on data: URLs has tripped browsers in
    // the past. The SVG is now self-contained after inlineEmbeddedImages.
    await new Promise((resolve, reject) => {
        img.onload = resolve;
        img.onerror = reject;
        img.src = dataUrl;
    });
    const canvas = document.createElement('canvas');
    canvas.width = w;
    canvas.height = h;
    const ctx = canvas.getContext('2d');
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, w, h);
    ctx.drawImage(img, 0, 0, w, h);
    return { dataUrl: canvas.toDataURL('image/png'), width: w, height: h };
}

// Exports the takeoff (BOM) as a PDF using a structured payload built in C#.
// Schema is versioned so the JS guards against C#-side drift.
//
// Payload shape (schemaVersion 1):
//   { schemaVersion, fileName, firm, project, date, audience,
//     plot: { headerTitle, includeSnapshot },
//     takeoff: {
//       headerTitle,
//       columns: [{ header, dataKey, align }],
//       rows:    [{ type: "row"|"subtotal", values: { [dataKey]: string } }],
//       grandTotal: "string"
//     } }
export async function exportTakeoffPdf(svgEl, payload) {
    if (!payload || payload.schemaVersion !== 1) {
        throw new Error('Unsupported PDF payload schema (expected schemaVersion 1)');
    }
    await ensurePdfLibs();
    const { jsPDF } = window.jspdf;
    const doc = new jsPDF({ orientation: 'portrait', unit: 'pt', format: 'letter' });
    const pageWidth = doc.internal.pageSize.getWidth();
    const pageHeight = doc.internal.pageSize.getHeight();
    const margin = 36; // 0.5 inch
    const contentWidth = pageWidth - margin * 2;

    // Cover header
    let y = margin + 4;
    doc.setFont('helvetica', 'bold').setFontSize(16);
    if (payload.firm) { doc.text(String(payload.firm), margin, y); y += 22; }
    doc.setFont('helvetica', 'normal').setFontSize(12);
    if (payload.project) { doc.text(String(payload.project), margin, y); y += 16; }
    if (payload.date) { doc.text(String(payload.date), margin, y); y += 22; }

    // Plot snapshot
    if (svgEl && (payload.plot?.includeSnapshot !== false)) {
        doc.setFont('helvetica', 'bold').setFontSize(13);
        doc.text(payload.plot?.headerTitle || 'Plot', margin, y);
        y += 12;
        try {
            // Target ~200 DPI at the content width.
            // pt = 72/inch, so contentWidth pt at 200 DPI = contentWidth * 200/72 px.
            const targetPx = Math.round(contentWidth * 200 / 72);
            const cappedPx = Math.min(targetPx, 2400); // memory cap
            const img = await rasterizeSvgToDataUrl(svgEl, cappedPx);
            const drawW = contentWidth;
            const drawH = (img.height / img.width) * drawW;
            const maxH = pageHeight - y - margin;
            const finalH = Math.min(drawH, maxH);
            const finalW = (img.width / img.height) * finalH;
            doc.addImage(img.dataUrl, 'PNG', margin, y, finalW, finalH);
        } catch (err) {
            doc.setFontSize(9).setTextColor('#a00');
            doc.text(`(Plot snapshot unavailable: ${describeRasterError(err)})`, margin, y);
            doc.setTextColor(0, 0, 0);
        }
    }

    // BOM table (new page)
    doc.addPage();
    doc.setFont('helvetica', 'bold').setFontSize(13);
    doc.text(payload.takeoff?.headerTitle || 'Bill of Materials', margin, margin + 4);

    const cols = Array.isArray(payload.takeoff?.columns) ? payload.takeoff.columns : [];
    const rows = Array.isArray(payload.takeoff?.rows) ? payload.takeoff.rows : [];
    const head = [cols.map((c) => c.header)];
    const body = rows.map((r) =>
        cols.map((c) => (r.values && r.values[c.dataKey] != null) ? String(r.values[c.dataKey]) : ''));

    const columnStyles = {};
    cols.forEach((c, idx) => { columnStyles[idx] = { halign: c.align || 'left' }; });

    const foot = payload.takeoff?.grandTotal
        ? [[...cols.slice(0, -1).map(() => ''), `Total: ${payload.takeoff.grandTotal}`]]
        : undefined;

    doc.autoTable({
        startY: margin + 16,
        margin: { left: margin, right: margin },
        head,
        body,
        foot,
        theme: 'striped',
        headStyles: { fillColor: [60, 60, 60], textColor: 255, fontStyle: 'bold' },
        footStyles: { fillColor: [220, 220, 220], textColor: 20, fontStyle: 'bold', halign: 'right' },
        styles: { fontSize: 9, cellPadding: 4, overflow: 'linebreak' },
        columnStyles,
        didParseCell: (data) => {
            if (data.section !== 'body') return;
            const row = rows[data.row.index];
            if (row && row.type === 'subtotal') {
                data.cell.styles.fontStyle = 'bold';
                data.cell.styles.fillColor = [235, 235, 235];
            }
        },
    });

    // Page footer
    const totalPages = doc.internal.getNumberOfPages();
    for (let i = 1; i <= totalPages; i++) {
        doc.setPage(i);
        doc.setFont('helvetica', 'normal').setFontSize(8).setTextColor(110, 110, 110);
        doc.text(
            `Generated by GardenPlot \u00B7 ${payload.date || ''} \u00B7 Page ${i} of ${totalPages}`,
            pageWidth / 2,
            pageHeight - 16,
            { align: 'center' });
    }

    doc.save(payload.fileName || 'takeoff.pdf');
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
