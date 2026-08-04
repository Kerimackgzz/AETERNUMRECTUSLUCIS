import { postCommerce } from "/js/core/commerce-api.js";
import { showToast } from "/js/components/toast.js";

function init() {
  const root = document.querySelector("[data-notifications-page]");
  if (!root) return;

  root.querySelectorAll("[data-notification-read]").forEach((button) => {
    button.addEventListener("click", async () => {
      button.disabled = true;
      const { ok, data } = await postCommerce(button.getAttribute("data-notification-read"));
      if (ok) {
        window.location.reload();
      } else {
        showToast(data?.message || "İşlem başarısız.", "error");
        button.disabled = false;
      }
    });
  });

  root.querySelector("[data-notifications-read-all]")?.addEventListener("click", async (event) => {
    event.currentTarget.disabled = true;
    const { ok, data } = await postCommerce("/account/notifications/read-all");
    if (ok) {
      window.location.reload();
    } else {
      showToast(data?.message || "İşlem başarısız.", "error");
      event.currentTarget.disabled = false;
    }
  });
}

if (typeof document !== "undefined") {
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
}
