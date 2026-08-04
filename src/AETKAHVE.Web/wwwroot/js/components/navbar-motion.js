// AETERNUM RECTUS LUCIS — Navbar scroll motion
// Şeffaf -> koyu zemin geçişi. Scroll'a yalnızca okuma amaçlı bağlanır,
// preventDefault/scroll hijack yok.

const SCROLL_THRESHOLD_PX = 24;

function initNavbarMotion(root = document) {
  const nav = root.querySelector("[data-navbar]");
  if (!nav) return null;

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
