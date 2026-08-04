// AETERNUM RECTUS LUCIS — Product Card Motion Enhancement
// Yalnizca motion katmani: ProductCard markup/base stili Ajan 1'e aittir, burada degistirilmez.
// Hook'lar (docs/contracts/FRONTEND_BACKEND_CONTRACT.md — frozen):
// [data-featured-products] > [data-product-card-list] > [data-product-card] (+ [data-product-card-surface]).
// Hero kendi pininde biter (bkz. home-frame-sequence.js); bu section normal scroll akisinda
// viewport'a girince IntersectionObserver ile kartlar asagidan yukari kayarak belirir.

const STAGGER_STEP_MS = 90;
const INTERSECTION_THRESHOLD = 0.2;

export class ProductCardMotion {
  constructor(sectionEl) {
    this.sectionEl = sectionEl;
    this.listEl = sectionEl.querySelector("[data-product-card-list]");
    this.cards = this.listEl ? Array.from(this.listEl.querySelectorAll("[data-product-card]")) : [];
    this.revealed = false;
    this.observer = null;

    this._bindActivation();
    this._observe();
  }

  _observe() {
    if (this.cards.length === 0) return;
    if (!("IntersectionObserver" in window) || window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      this._reveal(true);
      return;
    }
    this.observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) {
          this._reveal(false);
          this.observer.disconnect();
        }
      },
      { threshold: INTERSECTION_THRESHOLD }
    );
    this.observer.observe(this.sectionEl);
  }

  _reveal(immediate) {
    if (this.revealed) return;
    this.revealed = true;
    this.cards.forEach((card, index) => {
      if (immediate) {
        card.classList.add("is-revealed");
        return;
      }
      window.setTimeout(() => card.classList.add("is-revealed"), index * STAGGER_STEP_MS);
    });
  }

  _bindActivation() {
    this.cards.forEach((card) => {
      const trigger = () => {
        card.classList.remove("is-activated");
        void card.offsetWidth; // reflow: animasyonu yeniden tetiklemek icin
        card.classList.add("is-activated");
      };
      card.addEventListener("pointerenter", trigger);
      card.addEventListener("focus", trigger, true);
    });
  }

  destroy() {
    if (this.observer) this.observer.disconnect();
  }
}

export function initProductCardMotion(root = document) {
  return Array.from(root.querySelectorAll("[data-featured-products]")).map(
    (sectionEl) => new ProductCardMotion(sectionEl)
  );
}

if (typeof document !== "undefined") {
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", () => initProductCardMotion());
  } else {
    initProductCardMotion();
  }
}
