// AETERNUM RECTUS LUCIS — Toast
// Ortak, ekranı bloklamayan bildirim şeridi.

const AUTO_DISMISS_MS = 4000;

function region() {
  let el = document.querySelector("[data-toast-region]");
  if (!el) {
    el = document.createElement("div");
    el.setAttribute("data-toast-region", "");
    document.body.appendChild(el);
  }
  return el;
}

export function showToast(message, kind = "info") {
  const container = region();
  const toast = document.createElement("div");
  toast.className = "toast toast--" + kind;
  if (kind === "error") toast.setAttribute("role", "alert");
  else toast.setAttribute("role", "status");
  toast.textContent = message;
  container.appendChild(toast);
  window.requestAnimationFrame(() => toast.classList.add("is-visible"));
  window.setTimeout(() => {
    toast.classList.remove("is-visible");
    window.setTimeout(() => toast.remove(), 300);
  }, AUTO_DISMISS_MS);
}
