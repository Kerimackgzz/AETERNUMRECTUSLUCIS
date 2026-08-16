// AETERNUM RECTUS LUCIS — Koleksiyonlar (/categories) sayfası
// Kart reveal + hover shimmer motoru product-card-motion.js'deki ProductCardMotion sınıfının
// aynısı (About sayfasındaki HomeFrameSequence reuse'unun birebir eşdeğeri) — CollectionCard
// markup'ı ayrıca data-product-card/-surface hook'larını taşıdığı için sıfırdan yazılmıyor.

import { ProductCardMotion } from "/js/components/product-card-motion.js";

function initImageErrorFallback(root) {
  root.querySelectorAll(".collection-card__cover").forEach((cover) => {
    const img = cover.querySelector(".collection-card__image");
    if (!img) return;
    img.addEventListener("error", () => cover.classList.add("has-error"), { once: true });
  });
}

function init() {
  const grid = document.querySelector("[data-collections-grid]");
  if (!grid) return;
  new ProductCardMotion(grid);
  initImageErrorFallback(grid);
}

if (typeof document !== "undefined") {
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
}
