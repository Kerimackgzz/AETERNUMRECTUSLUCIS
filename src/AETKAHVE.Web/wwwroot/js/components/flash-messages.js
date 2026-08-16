const AUTO_DISMISS_MS = 4000;
const EXIT_DURATION_MS = 300;

document.querySelectorAll("[data-server-flash-message]").forEach((message) => {
  window.setTimeout(() => {
    message.classList.remove("is-visible");
    window.setTimeout(() => {
      const region = message.closest("[data-server-flash-region]");
      message.remove();
      if (region && !region.querySelector("[data-server-flash-message]")) region.remove();
    }, EXIT_DURATION_MS);
  }, AUTO_DISMISS_MS);
});
