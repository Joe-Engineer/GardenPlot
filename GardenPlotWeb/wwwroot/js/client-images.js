// Client-local image storage for the free tier.
// Stores user-supplied images (custom tiles, custom ground-cover textures)
// in browser IndexedDB keyed by GUID. Plot JSON only holds the GUID;
// rendering resolves it to a cached blob: URL at runtime.

const DB_NAME = 'gardenplot';
const DB_VERSION = 1;
const STORE = 'images';

let dbPromise = null;
const urlCache = new Map(); // guid -> blob: URL

function openDb() {
    if (dbPromise) {
        return dbPromise;
    }
    dbPromise = new Promise((resolve, reject) => {
        const req = indexedDB.open(DB_NAME, DB_VERSION);
        req.onupgradeneeded = () => {
            const db = req.result;
            if (!db.objectStoreNames.contains(STORE)) {
                db.createObjectStore(STORE, { keyPath: 'id' });
            }
        };
        req.onsuccess = () => resolve(req.result);
        req.onerror = () => reject(req.error);
    });
    return dbPromise;
}

function tx(db, mode) {
    return db.transaction(STORE, mode).objectStore(STORE);
}

function newGuid() {
    if (crypto && typeof crypto.randomUUID === 'function') {
        return crypto.randomUUID();
    }
    // Fallback RFC4122 v4
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
        const r = (Math.random() * 16) | 0;
        const v = c === 'x' ? r : (r & 0x3) | 0x8;
        return v.toString(16);
    });
}

export async function putImage(file, explicitId) {
    if (!file) {
        return null;
    }
    const blob = file instanceof Blob ? file : new Blob([file]);
    const id = explicitId || newGuid();
    const record = {
        id,
        blob,
        name: file.name || 'image',
        mime: file.type || blob.type || 'application/octet-stream',
        addedUtc: new Date().toISOString(),
        size: blob.size,
    };
    const db = await openDb();
    await new Promise((resolve, reject) => {
        const r = tx(db, 'readwrite').put(record);
        r.onsuccess = () => resolve();
        r.onerror = () => reject(r.error);
    });
    // Invalidate any cached URL for this id (re-put case).
    const existing = urlCache.get(id);
    if (existing) {
        URL.revokeObjectURL(existing);
        urlCache.delete(id);
    }
    return id;
}

export async function putImageFromDataUrl(dataUrl, explicitId) {
    if (!dataUrl) {
        return null;
    }
    const res = await fetch(dataUrl);
    const blob = await res.blob();
    return putImage(blob, explicitId);
}

export async function getImageUrl(id) {
    if (!id) {
        return null;
    }
    const cached = urlCache.get(id);
    if (cached) {
        return cached;
    }
    try {
        const db = await openDb();
        const record = await new Promise((resolve, reject) => {
            const r = tx(db, 'readonly').get(id);
            r.onsuccess = () => resolve(r.result || null);
            r.onerror = () => reject(r.error);
        });
        if (!record || !record.blob) {
            return null;
        }
        const url = URL.createObjectURL(record.blob);
        urlCache.set(id, url);
        return url;
    } catch (e) {
        console.warn('client-images: getImageUrl failed', e);
        return null;
    }
}

export async function getImageRecord(id) {
    if (!id) {
        return null;
    }
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const r = tx(db, 'readonly').get(id);
        r.onsuccess = () => resolve(r.result || null);
        r.onerror = () => reject(r.error);
    });
}

export async function deleteImage(id) {
    if (!id) {
        return;
    }
    const cached = urlCache.get(id);
    if (cached) {
        URL.revokeObjectURL(cached);
        urlCache.delete(id);
    }
    const db = await openDb();
    await new Promise((resolve, reject) => {
        const r = tx(db, 'readwrite').delete(id);
        r.onsuccess = () => resolve();
        r.onerror = () => reject(r.error);
    });
}

export async function listImages() {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const out = [];
        const cursorReq = tx(db, 'readonly').openCursor();
        cursorReq.onsuccess = () => {
            const c = cursorReq.result;
            if (!c) {
                resolve(out);
                return;
            }
            const { id, name, mime, addedUtc, size } = c.value;
            out.push({ id, name, mime, addedUtc, size });
            c.continue();
        };
        cursorReq.onerror = () => reject(cursorReq.error);
    });
}

export async function exportImage(id) {
    const rec = await getImageRecord(id);
    return rec ? rec.blob : null;
}

// Resolve a list of ids to URL strings (parallel). Unknown ids resolve to null.
export async function resolveMany(ids) {
    if (!ids || ids.length === 0) {
        return {};
    }
    const entries = await Promise.all(ids.map(async id => [id, await getImageUrl(id)]));
    return Object.fromEntries(entries);
}

// Heuristic: GUID-looking strings are client-local; legacy filenames (with
// an image extension) fall back to the legacy /tile-images/<filename> path.
const GUID_RE = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
const LEGACY_EXT_RE = /\.(png|jpe?g|gif|webp|bmp)$/i;

export function isClientImageId(s) {
    return typeof s === 'string' && GUID_RE.test(s);
}

export function isLegacyImageFilename(s) {
    return typeof s === 'string' && LEGACY_EXT_RE.test(s);
}

// Resolve a stored reference (GUID or legacy filename) to a URL usable by <img>/<image>.
export async function resolveImageRef(ref) {
    if (!ref) {
        return null;
    }
    if (isClientImageId(ref)) {
        return await getImageUrl(ref);
    }
    if (isLegacyImageFilename(ref)) {
        return `/tile-images/${ref}`;
    }
    return null;
}

// Preload a batch of refs so they're cached as blob URLs (used before PNG/print export).
export async function preloadRefs(refs) {
    const unique = Array.from(new Set((refs || []).filter(Boolean)));
    const out = {};
    await Promise.all(unique.map(async ref => {
        out[ref] = await resolveImageRef(ref);
    }));
    return out;
}

// Read a File from a <input type=file> element as a Blob, with a size guard.
export async function readFileInput(inputEl, maxBytes) {
    if (!inputEl || !inputEl.files || inputEl.files.length === 0) {
        return null;
    }
    const f = inputEl.files[0];
    if (maxBytes && f.size > maxBytes) {
        throw new Error(`Image is ${(f.size / (1024 * 1024)).toFixed(1)} MB; max is ${(maxBytes / (1024 * 1024)).toFixed(0)} MB.`);
    }
    return f;
}

// Bridge for .NET: accept a base64-encoded data URL coming from a Blazor InputFile
// (which is the easiest interop path) and store it.
export async function putImageFromBase64(base64, mime, suggestedName) {
    if (!base64) {
        return null;
    }
    const bin = atob(base64);
    const bytes = new Uint8Array(bin.length);
    for (let i = 0; i < bin.length; i++) {
        bytes[i] = bin.charCodeAt(i);
    }
    const blob = new Blob([bytes], { type: mime || 'application/octet-stream' });
    blob.name = suggestedName || 'image';
    return putImage(blob);
}

export async function probeImageDimensions(url) {
    if (!url) {
        return null;
    }
    return await new Promise((resolve, reject) => {
        const img = new Image();
        img.onload = () => resolve({ width: img.naturalWidth || img.width, height: img.naturalHeight || img.height });
        img.onerror = () => reject(new Error(`Failed to load image: ${url}`));
        img.src = url;
    });
}

// Revoke all cached blob URLs (called on page unload).
function revokeAll() {
    for (const url of urlCache.values()) {
        try { URL.revokeObjectURL(url); } catch { /* ignore */ }
    }
    urlCache.clear();
}

if (typeof window !== 'undefined') {
    window.addEventListener('pagehide', revokeAll);
    window.GardenPlot = window.GardenPlot || {};
    window.GardenPlot.clientImages = {
        putImage,
        putImageFromBase64,
        putImageFromDataUrl,
        getImageUrl,
        getImageRecord,
        deleteImage,
        listImages,
        exportImage,
        resolveMany,
        resolveImageRef,
        preloadRefs,
        isClientImageId,
        isLegacyImageFilename,
        readFileInput,
        applyClientImages,
        probeImageDimensions,
    };
}

// Scans the document for elements with data-client-image-id and replaces
// their src/href with a resolved blob: URL. Safe to call repeatedly; only
// elements not yet resolved (marked with data-client-image-applied) are touched.
export async function applyClientImages(root) {
    const scope = root || document;
    const els = scope.querySelectorAll('[data-client-image-id]:not([data-client-image-applied])');
    if (els.length === 0) {
        return;
    }
    const ids = new Set();
    els.forEach(el => {
        const id = el.getAttribute('data-client-image-id');
        if (id) {
            ids.add(id);
        }
    });
    const urls = await resolveMany([...ids]);
    els.forEach(el => {
        const id = el.getAttribute('data-client-image-id');
        if (!id) {
            return;
        }
        const url = urls[id];
        if (!url) {
            return;
        }
        const tag = (el.tagName || '').toLowerCase();
        // SVG <image> uses href; HTML <img> uses src. Set both to be safe.
        try {
            if (tag === 'image' || el.namespaceURI === 'http://www.w3.org/2000/svg') {
                el.setAttribute('href', url);
                el.setAttribute('xlink:href', url);
            } else {
                el.setAttribute('src', url);
            }
            el.setAttribute('data-client-image-applied', '1');
        } catch (e) {
            console.warn('client-images: applyClientImages failed for', id, e);
        }
    });
}
