// AETERNUM RECTUS LUCIS — Home Hero Frame Sequence
// Manifest-driven, scroll-position-linked canvas frame sequence. No scroll hijacking.
// Root hook: [data-home-hero] data-frame-manifest-url / data-poster-url / data-reduced-motion
// (docs/contracts/FRONTEND_BACKEND_CONTRACT.md — frozen).

const PRIORITY_WINDOW = 12;
const BATCH_SIZE = 6;
const BATCH_DELAY_MS = 120;
const RESIZE_DEBOUNCE_MS = 150;
const BREAKPOINTS = [
  { key: "mobile", maxWidth: 767 },
  { key: "tablet", maxWidth: 1279 },
  { key: "desktop", maxWidth: Infinity },
];

function pickBreakpoint(width) {
  const bp = BREAKPOINTS.find((b) => width <= b.maxWidth);
  return bp ? bp.key : "desktop";
}

function clamp(value, min, max) {
  return Math.min(max, Math.max(min, value));
}

function framePath(pattern, index, padLength) {
  return pattern.replace("{index}", String(index).padStart(padLength, "0"));
}

export class HomeFrameSequence {
  constructor(root, options = {}) {
    this.root = root;
    this.pin = root.querySelector("[data-hero-pin]") || root;
    this.canvas = root.querySelector("[data-hero-canvas]");
    this.ctx = this.canvas ? this.canvas.getContext("2d") : null;
    this.manifestUrl = options.manifestUrl || root.dataset.frameManifestUrl;
    this.rootPosterUrl = options.posterUrl || root.dataset.posterUrl || null;
    this.pinViewportUnits = Number(root.dataset.pinVh || options.pinVh || 400);

    this.manifest = null;
    this.activeBreakpointKey = null;
    this.activeSet = null;
    this.frameCache = new Map();
    this.loadedIndexes = new Set();
    this.currentIndex = -1;
    this.posterImage = null;
    this.rafId = null;
    this.resizeTimer = null;
    this.destroyed = false;
    this.wasComplete = false;
    const serverReducedMotion = root.dataset.reducedMotion === "true";
    this.reducedMotion = serverReducedMotion || window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    this._onScroll = this._onScroll.bind(this);
    this._onResize = this._onResize.bind(this);
    this._tick = this._tick.bind(this);

    this._init();
  }

  async _init() {
    this._applyPinHeight();
    this._resizeCanvas();

    if (this.rootPosterUrl) {
      this._loadImage(this.rootPosterUrl)
        .then((img) => {
          if (!this.posterImage) {
            this.posterImage = img;
            this._drawImage(img);
          }
        })
        .catch(() => {});
    }

    if (!this.manifestUrl) {
      this._fallbackToStatic();
      return;
    }

    try {
      const res = await fetch(this.manifestUrl, { credentials: "same-origin" });
      if (!res.ok) throw new Error(`Manifest yuklenemedi: ${res.status}`);
      this.manifest = await res.json();
    } catch (err) {
      this._fallbackToStatic();
      return;
    }
    if (this.destroyed) return;

    this._selectBreakpoint();
    if (!this.activeSet) {
      this._fallbackToStatic();
      return;
    }

    await this._loadPoster();
    if (this.destroyed) return;

    if (this.reducedMotion) {
      this._renderReducedMotion();
      return;
    }

    this._preloadPriorityFrames();
    this._schedulePreloadRemaining();

    window.addEventListener("scroll", this._onScroll, { passive: true });
    window.addEventListener("resize", this._onResize, { passive: true });
    this._onScroll();
  }

  _selectBreakpoint() {
    const key = pickBreakpoint(window.innerWidth);
    if (key === this.activeBreakpointKey) return;
    this.activeBreakpointKey = key;
    this.activeSet = (this.manifest && this.manifest[key]) || null;
    this.frameCache.clear();
    this.loadedIndexes.clear();
    this.currentIndex = -1;
  }

  _applyPinHeight() {
    this.root.style.setProperty("--hero-pin-vh", String(this.pinViewportUnits));
  }

  async _loadPoster() {
    if (!this.activeSet || !this.activeSet.poster) return;
    try {
      const img = await this._loadImage(this.activeSet.poster);
      this.posterImage = img;
      this._drawImage(img);
    } catch (err) {
      // breakpoint posteri yuklenemedi; kok data-poster-url veya icerik katmani hala okunabilir
    }
  }

  _loadImage(src) {
    return new Promise((resolve, reject) => {
      const img = new Image();
      img.decoding = "async";
      img.onload = () => resolve(img);
      img.onerror = () => reject(new Error(`Kare yuklenemedi: ${src}`));
      img.src = src;
    });
  }

  async _loadFrame(index) {
    if (this.frameCache.has(index)) return this.frameCache.get(index);
    const { pattern, padLength } = this.activeSet;
    try {
      const img = await this._loadImage(framePath(pattern, index, padLength));
      this.frameCache.set(index, img);
      this.loadedIndexes.add(index);
      return img;
    } catch (err) {
      return null;
    }
  }

  _preloadPriorityFrames() {
    const upper = Math.min(this.activeSet.count, PRIORITY_WINDOW);
    for (let i = 0; i < upper; i += 1) this._loadFrame(i);
  }

  _schedulePreloadRemaining() {
    const count = this.activeSet.count;
    let index = PRIORITY_WINDOW;
    const scheduleNext = (fn) => {
      if ("requestIdleCallback" in window) {
        window.requestIdleCallback(fn, { timeout: BATCH_DELAY_MS * 2 });
      } else {
        window.setTimeout(fn, BATCH_DELAY_MS);
      }
    };
    const loadBatch = () => {
      if (this.destroyed || !this.activeSet || index >= count) return;
      const end = Math.min(index + BATCH_SIZE, count);
      for (let i = index; i < end; i += 1) this._loadFrame(i);
      index = end;
      if (index < count) scheduleNext(loadBatch);
    };
    scheduleNext(loadBatch);
  }

  _nearestLoadedFrame(index) {
    if (this.loadedIndexes.has(index)) return this.frameCache.get(index);
    const count = this.activeSet.count;
    for (let offset = 1; index - offset >= 0 || index + offset < count; offset += 1) {
      if (index - offset >= 0 && this.loadedIndexes.has(index - offset)) {
        return this.frameCache.get(index - offset);
      }
      if (index + offset < count && this.loadedIndexes.has(index + offset)) {
        return this.frameCache.get(index + offset);
      }
    }
    return this.posterImage || null;
  }

  _onScroll() {
    if (this.rafId != null) return;
    this.rafId = window.requestAnimationFrame(this._tick);
  }

  _onResize() {
    window.clearTimeout(this.resizeTimer);
    this.resizeTimer = window.setTimeout(() => {
      const previousKey = this.activeBreakpointKey;
      this._selectBreakpoint();
      this._applyPinHeight();
      this._resizeCanvas();
      if (this.activeBreakpointKey !== previousKey && this.activeSet) {
        this._loadPoster().then(() => {
          this._preloadPriorityFrames();
          this._schedulePreloadRemaining();
          this._onScroll();
        });
      } else {
        this._onScroll();
      }
    }, RESIZE_DEBOUNCE_MS);
  }

  _resizeCanvas() {
    if (!this.canvas) return;
    const dpr = window.devicePixelRatio || 1;
    const rect = this.canvas.getBoundingClientRect();
    this.canvas.width = Math.max(1, Math.round(rect.width * dpr));
    this.canvas.height = Math.max(1, Math.round(rect.height * dpr));
  }

  _progress() {
    const rect = this.root.getBoundingClientRect();
    const wrapperTop = rect.top + window.scrollY;
    const scrollable = this.root.offsetHeight - window.innerHeight;
    if (scrollable <= 0) return 1;
    return clamp((window.scrollY - wrapperTop) / scrollable, 0, 1);
  }

  _tick() {
    this.rafId = null;
    if (this.destroyed || !this.activeSet) return;

    const progress = this._progress();
    const count = this.activeSet.count;
    const frameIndex = Math.round(progress * (count - 1));
    this.root.style.setProperty("--hero-progress", progress.toFixed(4));

    if (frameIndex !== this.currentIndex) {
      this.currentIndex = frameIndex;
      const cached = this.frameCache.get(frameIndex);
      if (cached) {
        this._drawImage(cached);
      } else {
        const nearest = this._nearestLoadedFrame(frameIndex);
        if (nearest) this._drawImage(nearest);
        this._loadFrame(frameIndex).then((img) => {
          if (img && this.currentIndex === frameIndex) this._drawImage(img);
        });
      }
    }

    const isComplete = progress >= 1;
    if (isComplete !== this.wasComplete) {
      this.wasComplete = isComplete;
      // Hero kendi pininde biter biter (featured-products ayrı, dogal scroll akisinda gelen bir
      // section); asil "urunler yukari gelsin" hareketi product-card-motion.js'de
      // IntersectionObserver ile featured-products viewport'a girince tetiklenir.
      this.root.dispatchEvent(new CustomEvent("herosequence:complete", { detail: { complete: isComplete } }));
    }
    this.root.dispatchEvent(new CustomEvent("herosequence:progress", { detail: { progress, frameIndex, count } }));
  }

  _drawImage(img) {
    if (!this.ctx || !this.canvas || !this.canvas.width || !this.canvas.height) return;
    const { width, height } = this.canvas;
    this.ctx.clearRect(0, 0, width, height);
    const canvasRatio = width / height;
    const imgRatio = img.width / img.height;
    const drawWidth = imgRatio > canvasRatio ? height * imgRatio : width;
    const drawHeight = imgRatio > canvasRatio ? height : width / imgRatio;
    this.ctx.drawImage(img, (width - drawWidth) / 2, (height - drawHeight) / 2, drawWidth, drawHeight);
  }

  _renderReducedMotion() {
    this.root.classList.add("hero-frame-sequence--static");
    if (this.posterImage) this._drawImage(this.posterImage);
    this.root.dispatchEvent(
      new CustomEvent("herosequence:complete", { detail: { complete: true, reducedMotion: true } })
    );
  }

  _fallbackToStatic() {
    this.root.classList.add("hero-frame-sequence--fallback");
  }

  destroy() {
    this.destroyed = true;
    window.removeEventListener("scroll", this._onScroll);
    window.removeEventListener("resize", this._onResize);
    if (this.rafId != null) window.cancelAnimationFrame(this.rafId);
    window.clearTimeout(this.resizeTimer);
    this.frameCache.clear();
    this.loadedIndexes.clear();
  }
}

export function initHomeFrameSequences(root = document) {
  return Array.from(root.querySelectorAll("[data-home-hero]")).map((node) => new HomeFrameSequence(node));
}

if (typeof document !== "undefined") {
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", () => initHomeFrameSequences());
  } else {
    initHomeFrameSequences();
  }
}
