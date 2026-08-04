import { postForm } from "/js/core/commerce-api.js";
import { showToast } from "/js/components/toast.js";

function init() {
  const root = document.querySelector("[data-admin-reviews-page]");
  if (!root) return;
  root.querySelectorAll("[data-review-status-form]").forEach((form) => {
    form.addEventListener("submit", async (event) => {
      event.preventDefault();
      const id = form.getAttribute("data-review-id");
      const status = form.querySelector("select[name=status]").value;
      const response = form.querySelector("input[name=response]").value || null;
      const submitBtn = form.querySelector("[type=submit]");
      submitBtn.disabled = true;
      const { ok, data } = await postForm(`/admin/reviews/${id}/status?status=${status}`, { response });
      if (ok) {
        window.location.reload();
      } else {
        showToast(data?.message || "Güncellenemedi.", "error");
        submitBtn.disabled = false;
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
