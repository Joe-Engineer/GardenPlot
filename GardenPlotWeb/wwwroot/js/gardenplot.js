// Garden Plot interop. Loaded as an ES module via JSRuntime.
// Wheel: only preventDefault when Shift or Ctrl is held (otherwise let the page scroll).
export function attachWheel(el, dotnetRef) {
    const handler = (e) => {
        if (e.shiftKey || e.ctrlKey) {
            e.preventDefault();
            dotnetRef.invokeMethodAsync('OnWheelFromJs', e.deltaY, e.shiftKey, e.ctrlKey);
        }
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
