// AETERNUM RECTUS LUCIS — Hikâyemiz (About) scroll-scrub sahne + parça parça metin reveal
// Kare motoru home-frame-sequence.js'deki HomeFrameSequence sınıfının aynısı (tamamen
// jenerik, [data-hero-pin]/[data-hero-canvas] arıyor) — burada yalnız farklı bir kökte
// ([data-about-hero]) başlatılır ve herosequence:progress olayına göre [data-reveal-at]
// elemanları kademeli olarak açılır.

import { HomeFrameSequence } from "/js/pages/home-frame-sequence.js";

function initRevealSequence(root) {
  const blocks = Array.from(root.querySelectorAll("[data-reveal-at]")).map((el) => ({
    el,
    threshold: Number(el.dataset.revealAt) || 0,
  }));
  if (blocks.length === 0) return;

  const revealAll = () => blocks.forEach(({ el }) => el.classList.add("is-revealed"));

  if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
    revealAll();
    return;
  }

  root.addEventListener("herosequence:progress", (event) => {
    const { progress } = event.detail;
    blocks.forEach(({ el, threshold }) => {
      if (progress >= threshold) el.classList.add("is-revealed");
    });
  });

  root.addEventListener("herosequence:complete", (event) => {
    if (event.detail?.reducedMotion) revealAll();
  });

  // Manifest/kare yüklemesi tamamen başarısız olursa HomeFrameSequence hiçbir event
  // dispatch etmeden --fallback class'ı ekler (zamanlaması garanti değil, async init
  // içinde); MutationObserver ile bunu ne zaman eklenirse eklensin yakalayıp metni
  // yine de görünür kılıyoruz.
  if (root.classList.contains("hero-frame-sequence--fallback")) {
    revealAll();
    return;
  }
  const fallbackObserver = new MutationObserver(() => {
    if (root.classList.contains("hero-frame-sequence--fallback")) {
      revealAll();
      fallbackObserver.disconnect();
    }
  });
  fallbackObserver.observe(root, { attributes: true, attributeFilter: ["class"] });
}

function init() {
  const root = document.querySelector("[data-about-hero]");
  if (!root) return;
  new HomeFrameSequence(root);
  initRevealSequence(root);
}

if (typeof document !== "undefined") {
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
}
