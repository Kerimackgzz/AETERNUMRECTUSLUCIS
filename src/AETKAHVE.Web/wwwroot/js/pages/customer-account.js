const menu = document.querySelector("[data-account-menu]");
const toggle = document.querySelector("[data-account-menu-toggle]");
const closeButton = document.querySelector("[data-account-menu-close]");
const backdrop = document.querySelector("[data-account-menu-backdrop]");
const mainContent = document.querySelector(".customer-account__content");
const mobileBar = document.querySelector(".customer-account__mobile-bar");
const siteNavbar = document.querySelector("[data-navbar]");

if (menu && toggle && backdrop && mainContent) {
  let lastFocused = null;

  const isOpen = () => toggle.getAttribute("aria-expanded") === "true";
  const setOpen = (open) => {
    toggle.setAttribute("aria-expanded", String(open));
    menu.classList.toggle("is-open", open);
    backdrop.hidden = !open;
    mainContent.inert = open;
    if (mobileBar) mobileBar.inert = open;
    if (siteNavbar) siteNavbar.inert = open;
    document.body.classList.toggle("account-menu-open", open);
    if (open) {
      lastFocused = document.activeElement;
      closeButton?.focus();
    } else if (lastFocused instanceof HTMLElement) {
      lastFocused.focus();
    }
  };

  toggle.addEventListener("click", () => setOpen(!isOpen()));
  closeButton?.addEventListener("click", () => setOpen(false));
  backdrop.addEventListener("click", () => setOpen(false));
  menu.querySelectorAll("a").forEach((link) => link.addEventListener("click", () => setOpen(false)));
  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && isOpen()) setOpen(false);
  });
  window.matchMedia("(min-width: 960px)").addEventListener("change", (event) => {
    if (event.matches && isOpen()) setOpen(false);
  });
}
