// AETERNUM RECTUS LUCIS — Navbar motion and mobile disclosure navigation.
// Scroll yalnızca görsel durum okur; sayfa kaydırmasını ele geçirmez.

const SCROLL_THRESHOLD_PX = 24;
const LETTER_STAGGER_MS = 18;
const MOBILE_NAV_QUERY = "(max-width: 959px)";

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
    letter.textContent = char === " " ? "\u00a0" : char;
    letter.style.setProperty("--letter-delay", `${index * LETTER_STAGGER_MS}ms`);

    mask.appendChild(letter);
    fragment.appendChild(mask);
  });
  brand.appendChild(fragment);
}

function initNavbarMotion(root = document) {
  const nav = root.querySelector("[data-navbar]");
  if (!nav || nav.dataset.navbarInitialized === "true") return null;

  nav.dataset.navbarInitialized = "true";
  nav.classList.add("is-enhanced");
  splitBrandIntoLetterMasks(nav.querySelector("[data-navbar-brand]"));

  const toggle = nav.querySelector("[data-navbar-toggle]");
  const menu = nav.querySelector("[data-navbar-menu]");
  const mobileQuery = window.matchMedia(MOBILE_NAV_QUERY);
  let ticking = false;

  const applyScrollState = () => {
    ticking = false;
    nav.classList.toggle("is-scrolled", window.scrollY > SCROLL_THRESHOLD_PX);
  };

  const onScroll = () => {
    if (ticking) return;
    ticking = true;
    window.requestAnimationFrame(applyScrollState);
  };

  const setMenuOpen = (open, restoreFocus = false) => {
    const shouldOpen = Boolean(open && mobileQuery.matches && toggle && menu);
    nav.classList.toggle("is-menu-open", shouldOpen);
    document.body.classList.toggle("has-open-navigation", shouldOpen);

    if (toggle) {
      toggle.setAttribute("aria-expanded", String(shouldOpen));
      toggle.setAttribute("aria-label", shouldOpen ? "Menüyü kapat" : "Menüyü aç");
    }
    if (menu) {
      menu.setAttribute("aria-hidden", String(mobileQuery.matches && !shouldOpen));
      menu.inert = mobileQuery.matches && !shouldOpen;
    }
    if (restoreFocus && toggle) toggle.focus();
  };

  const onToggle = () => setMenuOpen(!nav.classList.contains("is-menu-open"));
  const onKeyDown = (event) => {
    if (event.key === "Escape" && nav.classList.contains("is-menu-open")) {
      setMenuOpen(false, true);
    }
  };
  const onDocumentClick = (event) => {
    if (nav.classList.contains("is-menu-open") && event.target instanceof Node && !nav.contains(event.target)) {
      setMenuOpen(false);
    }
  };
  const onMenuClick = (event) => {
    if (event.target instanceof Element && event.target.closest("a[href]")) setMenuOpen(false);
  };
  const onViewportChange = () => setMenuOpen(false);

  toggle?.addEventListener("click", onToggle);
  menu?.addEventListener("click", onMenuClick);
  document.addEventListener("keydown", onKeyDown);
  document.addEventListener("click", onDocumentClick);
  window.addEventListener("scroll", onScroll, { passive: true });
  if (typeof mobileQuery.addEventListener === "function") {
    mobileQuery.addEventListener("change", onViewportChange);
  } else {
    mobileQuery.addListener(onViewportChange);
  }

  setMenuOpen(false);
  applyScrollState();

  return {
    destroy() {
      toggle?.removeEventListener("click", onToggle);
      menu?.removeEventListener("click", onMenuClick);
      document.removeEventListener("keydown", onKeyDown);
      document.removeEventListener("click", onDocumentClick);
      window.removeEventListener("scroll", onScroll);
      if (typeof mobileQuery.removeEventListener === "function") {
        mobileQuery.removeEventListener("change", onViewportChange);
      } else {
        mobileQuery.removeListener(onViewportChange);
      }
      document.body.classList.remove("has-open-navigation");
      nav.classList.remove("is-enhanced", "is-menu-open", "is-scrolled");
      nav.removeAttribute("data-navbar-initialized");
      menu?.removeAttribute("aria-hidden");
      if (menu) menu.inert = false;
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
