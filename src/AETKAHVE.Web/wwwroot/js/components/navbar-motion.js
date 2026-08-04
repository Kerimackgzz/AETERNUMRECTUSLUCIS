// AETERNUM RECTUS LUCIS — Navbar scroll motion
// Şeffaf -> koyu zemin geçişi. Scroll'a yalnızca okuma amaçlı bağlanır,
// preventDefault/scroll hijack yok.

const SCROLL_THRESHOLD_PX = 24;
const LETTER_STAGGER_MS = 28;

function splitBrandIntoLetterMasks(brand) {
  if (!brand || brand.dataset.letterMaskApplied) return;
  const text = brand.textContent.trim();
  if (!text) return;
  brand.dataset.letterMaskApplied = "true";
  brand.setAttribute("aria-label", text);
  brand.textContent = "";

  const fragment = document.createDocumentFragment();
  Array.from(text).forEach((char, index) => {
    const mask = document.createElement("span");
    mask.className = "navbar-brand__letter-mask";
    mask.setAttribute("aria-hidden", "true");

    const letter = document.createElement("span");
    letter.className = "navbar-brand__letter";
    letter.textContent = char === " " ? " " : char;
    letter.style.setProperty("--letter-delay", `${index * LETTER_STAGGER_MS}ms`);

    mask.appendChild(letter);
    fragment.appendChild(mask);
  });
  brand.appendChild(fragment);
}

function initNavbarMotion(root = document) {
  const nav = root.querySelector("[data-navbar]");
  if (!nav) return null;

  splitBrandIntoLetterMasks(nav.querySelector("[data-navbar-brand]"));

  let ticking = false;

  const apply = () => {
    ticking = false;
    nav.classList.toggle("is-scrolled", window.scrollY > SCROLL_THRESHOLD_PX);
  };

  const onScroll = () => {
    if (ticking) return;
    ticking = true;
    window.requestAnimationFrame(apply);
  };

  window.addEventListener("scroll", onScroll, { passive: true });
  apply();

  return {
    destroy() {
      window.removeEventListener("scroll", onScroll);
    },
  };
}

if (typeof document !== "undefined") {
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", () => initNavbarMotion());
  } else {
    initNavbarMotion();
  }
}

export { initNavbarMotion };
