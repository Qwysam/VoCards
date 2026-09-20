/* ============================================================================
   VoCards browser interop.
   ----------------------------------------------------------------------------
   Everything the WebAssembly app cannot do on its own: persistent storage,
   speech synthesis, global keyboard handling, file download and pick, and the
   theme stamp that has to run before first paint.
   ========================================================================= */

const DB_NAME = "vocards";
const DB_VERSION = 1;
const STORE = "library";
const KEY = "current";

/* ------------------------------------------------------------- storage --- */
/* IndexedDB is the primary store: a serious library outgrows the ~5 MB that
   localStorage allows. localStorage stays as a fallback for private windows
   and anywhere IndexedDB is blocked. */

let dbPromise = null;

function openDb() {
    if (dbPromise) {
        return dbPromise;
    }

    dbPromise = new Promise((resolve, reject) => {
        if (!("indexedDB" in window)) {
            reject(new Error("IndexedDB unavailable"));
            return;
        }

        const request = indexedDB.open(DB_NAME, DB_VERSION);

        request.onupgradeneeded = () => {
            const db = request.result;
            if (!db.objectStoreNames.contains(STORE)) {
                db.createObjectStore(STORE);
            }
        };

        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error ?? new Error("IndexedDB open failed"));
        request.onblocked = () => reject(new Error("IndexedDB blocked"));
    }).catch((err) => {
        dbPromise = null;
        throw err;
    });

    return dbPromise;
}

export async function save(json) {
    try {
        const db = await openDb();
        await new Promise((resolve, reject) => {
            const tx = db.transaction(STORE, "readwrite");
            tx.objectStore(STORE).put(json, KEY);
            tx.oncomplete = resolve;
            tx.onerror = () => reject(tx.error);
            tx.onabort = () => reject(tx.error);
        });
        return true;
    } catch {
        // Fall back to localStorage; it may also throw when storage is blocked.
        try {
            localStorage.setItem("vocards:library", json);
            return true;
        } catch {
            return false;
        }
    }
}

export async function load() {
    try {
        const db = await openDb();
        const value = await new Promise((resolve, reject) => {
            const tx = db.transaction(STORE, "readonly");
            const req = tx.objectStore(STORE).get(KEY);
            req.onsuccess = () => resolve(req.result ?? null);
            req.onerror = () => reject(req.error);
        });

        if (value) {
            return value;
        }
    } catch {
        /* fall through to localStorage */
    }

    try {
        return localStorage.getItem("vocards:library");
    } catch {
        return null;
    }
}

export async function clear() {
    try {
        const db = await openDb();
        await new Promise((resolve, reject) => {
            const tx = db.transaction(STORE, "readwrite");
            tx.objectStore(STORE).delete(KEY);
            tx.oncomplete = resolve;
            tx.onerror = () => reject(tx.error);
        });
    } catch {
        /* ignore */
    }

    try {
        localStorage.removeItem("vocards:library");
    } catch {
        /* ignore */
    }
}

/* Rough usage report for the settings page. */
export async function storageEstimate() {
    if (!navigator.storage?.estimate) {
        return null;
    }

    try {
        const { usage, quota } = await navigator.storage.estimate();
        return { usage: usage ?? 0, quota: quota ?? 0 };
    } catch {
        return null;
    }
}

/* --------------------------------------------------------------- theme --- */

export function applyTheme(theme, accent, reduceMotion) {
    const root = document.documentElement;

    if (theme === "light" || theme === "dark") {
        root.setAttribute("data-theme", theme);
    } else {
        root.removeAttribute("data-theme");
    }

    root.setAttribute("data-accent", accent || "violet");

    if (reduceMotion) {
        root.setAttribute("data-motion", "reduced");
    } else {
        root.removeAttribute("data-motion");
    }

    // Keep the browser UI (address bar, notches) in step with the surface.
    const surface = getComputedStyle(root).getPropertyValue("--surface-canvas").trim();
    let meta = document.querySelector('meta[name="theme-color"]');
    if (!meta) {
        meta = document.createElement("meta");
        meta.name = "theme-color";
        document.head.appendChild(meta);
    }
    meta.content = surface || "#f6f6f4";

    try {
        localStorage.setItem("vocards:theme", theme || "system");
        localStorage.setItem("vocards:accent", accent || "violet");
    } catch {
        /* ignore */
    }
}

/** True on macOS and iPadOS, where the palette chord is rendered as Cmd. */
export function isApplePlatform() {
    const platform = navigator.userAgentData?.platform ?? navigator.platform ?? "";
    return /mac|iphone|ipad|ipod/i.test(platform);
}

export function prefersDark() {
    return window.matchMedia?.("(prefers-color-scheme: dark)").matches ?? false;
}

export function prefersReducedMotion() {
    return window.matchMedia?.("(prefers-reduced-motion: reduce)").matches ?? false;
}

/* --------------------------------------------------------------- speech --- */

let voices = [];

function refreshVoices() {
    voices = window.speechSynthesis?.getVoices() ?? [];
}

if ("speechSynthesis" in window) {
    refreshVoices();
    window.speechSynthesis.addEventListener("voiceschanged", refreshVoices);
}

export function speechAvailable() {
    return "speechSynthesis" in window;
}

export function listVoices() {
    refreshVoices();
    return voices.map((v) => ({
        name: v.name,
        lang: v.lang,
        uri: v.voiceURI,
        isDefault: v.default === true,
    }));
}

/**
 * Speaks text, preferring a voice matching the card's language tag.
 * Falls back through exact tag -> language prefix -> browser default.
 */
export function speak(text, lang, voiceUri, rate, pitch) {
    if (!text || !("speechSynthesis" in window)) {
        return false;
    }

    try {
        window.speechSynthesis.cancel();
        refreshVoices();

        const utterance = new SpeechSynthesisUtterance(text);
        utterance.lang = lang || "en-US";
        utterance.rate = Math.min(Math.max(rate || 1, 0.5), 2);
        utterance.pitch = Math.min(Math.max(pitch ?? 1, 0), 2);

        const chosen =
            (voiceUri && voices.find((v) => v.voiceURI === voiceUri)) ||
            voices.find((v) => v.lang === lang) ||
            voices.find((v) => lang && v.lang?.startsWith(lang.split("-")[0]));

        if (chosen) {
            utterance.voice = chosen;
        }

        window.speechSynthesis.speak(utterance);
        return true;
    } catch {
        return false;
    }
}

export function stopSpeaking() {
    try {
        window.speechSynthesis?.cancel();
    } catch {
        /* ignore */
    }
}

/* ---------------------------------------------------------------- sound --- */
/* Short synthesised blips rather than audio files: no assets to ship, and no
   licence to worry about. */

let audioCtx = null;

export function blip(kind) {
    try {
        audioCtx ??= new (window.AudioContext || window.webkitAudioContext)();

        if (audioCtx.state === "suspended") {
            audioCtx.resume();
        }

        const tones = {
            correct: [660, 880],
            wrong: [220, 165],
            level: [523, 659, 784],
            done: [587, 740, 880],
        };

        const notes = tones[kind] ?? tones.correct;
        const now = audioCtx.currentTime;

        notes.forEach((freq, index) => {
            const osc = audioCtx.createOscillator();
            const gain = audioCtx.createGain();

            osc.type = "sine";
            osc.frequency.value = freq;

            const start = now + index * 0.08;
            gain.gain.setValueAtTime(0, start);
            gain.gain.linearRampToValueAtTime(0.06, start + 0.012);
            gain.gain.exponentialRampToValueAtTime(0.0001, start + 0.16);

            osc.connect(gain).connect(audioCtx.destination);
            osc.start(start);
            osc.stop(start + 0.18);
        });
    } catch {
        /* audio is a nicety; never let it break a study session */
    }
}

/* ------------------------------------------------------------- keyboard --- */

let keyHandler = null;

/**
 * Routes global shortcuts to .NET. Keys pressed inside a text field are
 * ignored, except Escape and the palette chord, so typing never triggers a
 * navigation.
 */
export function registerKeys(dotNetRef) {
    unregisterKeys();

    keyHandler = (event) => {
        const target = event.target;
        const typing =
            target instanceof HTMLElement &&
            (target.tagName === "INPUT" ||
                target.tagName === "TEXTAREA" ||
                target.tagName === "SELECT" ||
                target.isContentEditable);

        const paletteChord = (event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k";

        if (paletteChord) {
            event.preventDefault();
        } else if (typing && event.key !== "Escape") {
            return;
        } else if (event.ctrlKey || event.metaKey || event.altKey) {
            return;
        }

        dotNetRef.invokeMethodAsync("OnKey", {
            key: event.key,
            ctrl: event.ctrlKey || event.metaKey,
            shift: event.shiftKey,
            fromField: typing,
        });
    };

    window.addEventListener("keydown", keyHandler);
}

export function unregisterKeys() {
    if (keyHandler) {
        window.removeEventListener("keydown", keyHandler);
        keyHandler = null;
    }
}

/* ----------------------------------------------------------------- dom --- */

export function focus(selector) {
    requestAnimationFrame(() => {
        const el = document.querySelector(selector);
        if (el instanceof HTMLElement) {
            el.focus();
            if (el instanceof HTMLInputElement) {
                el.select();
            }
        }
    });
}

export function scrollToTop() {
    window.scrollTo({ top: 0, behavior: "instant" });
}

/* ---------------------------------------------------------------- files --- */

export function download(filename, content, mime) {
    const blob = new Blob([content], { type: mime || "application/json" });
    const url = URL.createObjectURL(blob);

    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = filename;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();

    // Give the browser a beat to start the download before revoking.
    setTimeout(() => URL.revokeObjectURL(url), 2000);
}

export async function copyText(text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch {
        return false;
    }
}

/* -------------------------------------------------------------- startup --- */
/* Stamps the stored theme before Blazor boots, so there is no flash of the
   wrong colour scheme while WebAssembly downloads. */

export function bootTheme() {
    try {
        const theme = localStorage.getItem("vocards:theme");
        const accent = localStorage.getItem("vocards:accent");

        if (theme === "light" || theme === "dark") {
            document.documentElement.setAttribute("data-theme", theme);
        }

        document.documentElement.setAttribute("data-accent", accent || "violet");
    } catch {
        /* ignore */
    }
}
