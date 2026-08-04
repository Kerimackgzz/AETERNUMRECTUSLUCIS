import { postCommerce } from "/js/core/commerce-api.js";
import { showToast } from "/js/components/toast.js";

function init() {
  const root = document.querySelector("[data-reviews-page]");
  if (!root) return;
  root.querySelectorAll("[data-review-delete]").forEach((button) => {
    button.addEventListener("click", async () => {
      button.disabled = true;
      const { ok, data } = await postCommerce(button.getAttribute("data-review-delete"));
      if (ok) {
        window.location.reload();
      } else {
        showToast(data?.message || "Silinemedi.", "error");
        button.disabled = false;
      }
    });
  });
}

if (typeof document !== "undefined") {
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
}
