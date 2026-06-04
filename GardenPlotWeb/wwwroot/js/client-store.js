// Client-local structured-data storage. Owns the IndexedDB that holds the
// plot library document and any migration state. Mirrors the same minimal
// IDB pattern used by client-images.js but with a separate database so the
// two stores can be versioned and evolved independently.

const DB_NAME = 'gardenplot-structured';
const DB_VERSION = 1;
const STORE = 'kv';

let dbPromise = null;

function openDb() {
    if (dbPromise) {
        return dbPromise;
    }
    dbPromise = new Promise((resolve, reject) => {
        const req = indexedDB.open(DB_NAME, DB_VERSION);
        req.onupgradeneeded = () => {
            const db = req.result;
            if (!db.objectStoreNames.contains(STORE)) {
                db.createObjectStore(STORE, { keyPath: 'key' });
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

export async function getString(key) {
    if (!key) {
        return null;
    }
    try {
        const db = await openDb();
        return await new Promise((resolve, reject) => {
            const r = tx(db, 'readonly').get(key);
            r.onsuccess = () => {
                const v = r.result;
                resolve(v && typeof v.value === 'string' ? v.value : null);
            };
            r.onerror = () => reject(r.error);
        });
    } catch (e) {
        console.warn('client-store: getString failed for', key, e);
        return null;
    }
}

export async function putString(key, value) {
    if (!key) {
        return false;
    }
    try {
        const db = await openDb();
        await new Promise((resolve, reject) => {
            const r = tx(db, 'readwrite').put({ key, value: value ?? '', modifiedUtc: new Date().toISOString() });
            r.onsuccess = () => resolve();
            r.onerror = () => reject(r.error);
        });
        return true;
    } catch (e) {
        console.warn('client-store: putString failed for', key, e);
        return false;
    }
}

export async function remove(key) {
    if (!key) {
        return;
    }
    try {
        const db = await openDb();
        await new Promise((resolve, reject) => {
            const r = tx(db, 'readwrite').delete(key);
            r.onsuccess = () => resolve();
            r.onerror = () => reject(r.error);
        });
    } catch (e) {
        console.warn('client-store: remove failed for', key, e);
    }
}

export async function keys() {
    try {
        const db = await openDb();
        return await new Promise((resolve, reject) => {
            const r = tx(db, 'readonly').getAllKeys();
            r.onsuccess = () => resolve(r.result || []);
            r.onerror = () => reject(r.error);
        });
    } catch (e) {
        console.warn('client-store: keys failed', e);
        return [];
    }
}

// Window-scoped surface so Blazor's IJSRuntime can call us without a module reference
// (parallels GardenPlot.clientImages.* on client-images.js).
if (typeof window !== 'undefined') {
    window.GardenPlot = window.GardenPlot || {};
    window.GardenPlot.clientStore = { getString, putString, remove, keys };
}
