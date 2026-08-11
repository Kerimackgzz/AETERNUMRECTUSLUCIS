// AETERNUM RECTUS LUCIS — Shop filter accordion (smooth aç/kapa)
// navbar-motion.js'deki .is-enhanced + class-toggle + capped max-height desenini
// [data-details] yerine native <details>/<summary> üzerinde uygular. JS hiç
// çalışmazsa <details> kendi native anlık aç/kapa davranışını korur.

function animateClose(details, body) {
  details.classList.remove("is-open");
  const onEnd = (event) => {
    if (event.target !== body || event.propertyName !== "max-height") return;
    body.removeEventListener("transitionend", onEnd);
    details.open = false;
  };
  body.addEventListener("transitionend", onEnd);
}

function animateOpen(details) {
  details.open = true;
  window.requestAnimationFrame(() => details.classList.add("is-open"));
}

function initGroup(details) {
  const summary = details.querySelector("summary");
  const body = details.querySelector(".shop-filters__body");
  if (!summary || !body) return;

  details.classList.toggle("is-open", details.open);

  summary.addEventListener("click", (event) => {
    event.preventDefault();
    if (details.open) {
      animateClose(details, body);
    } else {
      animateOpen(details);
    }
  });
}

function init() {
  const container = document.querySelector(".shop-filters");
  if (!container) return;
  const groups = Array.from(container.querySelectorAll(".shop-filters__group"));
  if (groups.length === 0) return;

  container.classList.add("is-enhanced");
  groups.forEach(initGroup);
}

if (typeof document !== "undefined") {
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
}
