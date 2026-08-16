(() => {
  "use strict";

  const toggle = document.querySelector("[data-admin-nav-toggle]");
  const sidebar = document.querySelector("[data-admin-sidebar]");
  const overlay = document.querySelector("[data-admin-nav-overlay]");
  const content = document.querySelector("[data-admin-nav-content]");
  const topbarActions = document.querySelector(".admin-topbar__actions");
  if (!toggle || !sidebar || !overlay || !content) return;

  const desktop = window.matchMedia("(min-width: 1024px)");
  let open = false;

  const focusableElements = () => Array.from(sidebar.querySelectorAll(
    'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
  ));

  const setBackgroundInert = (value) => {
    if (value) {
      content.setAttribute("inert", "");
      topbarActions?.setAttribute("inert", "");
    } else {
      content.removeAttribute("inert");
      topbarActions?.removeAttribute("inert");
    }
  };

  const applyState = ({ restoreFocus = false } = {}) => {
    if (desktop.matches) {
      open = false;
      sidebar.removeAttribute("data-open");
      sidebar.removeAttribute("aria-hidden");
      sidebar.removeAttribute("inert");
      overlay.hidden = true;
      document.body.classList.remove("admin-nav-open");
      setBackgroundInert(false);
      toggle.setAttribute("aria-expanded", "false");
      toggle.setAttribute("aria-label", "Yönetim menüsünü aç");
      return;
    }

    sidebar.toggleAttribute("data-open", open);
    sidebar.setAttribute("aria-hidden", String(!open));
    sidebar.toggleAttribute("inert", !open);
    overlay.hidden = !open;
    document.body.classList.toggle("admin-nav-open", open);
    setBackgroundInert(open);
    toggle.setAttribute("aria-expanded", String(open));
    toggle.setAttribute("aria-label", open ? "Yönetim menüsünü kapat" : "Yönetim menüsünü aç");

    if (open) {
      (sidebar.querySelector('[aria-current="page"]') || focusableElements()[0])?.focus();
    } else if (restoreFocus) {
      toggle.focus();
    }
  };

  toggle.addEventListener("click", () => {
    open = !open;
    applyState({ restoreFocus: !open });
  });

  overlay.addEventListener("click", () => {
    open = false;
    applyState({ restoreFocus: true });
  });

  sidebar.addEventListener("click", (event) => {
    if (!desktop.matches && event.target.closest("a[href]")) {
      open = false;
      applyState();
    }
  });

  document.addEventListener("keydown", (event) => {
    if (!open || desktop.matches) return;
    if (event.key === "Escape") {
      event.preventDefault();
      open = false;
      applyState({ restoreFocus: true });
      return;
    }
    if (event.key !== "Tab") return;

    const focusable = focusableElements();
    if (focusable.length === 0) return;
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  });

  desktop.addEventListener("change", () => applyState());
  applyState();
})();
