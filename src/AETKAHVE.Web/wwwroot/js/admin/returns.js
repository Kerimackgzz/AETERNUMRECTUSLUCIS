import { postForm } from "/js/core/commerce-api.js";
import { showToast } from "/js/components/toast.js";

function init() {
  const root = document.querySelector("[data-admin-returns-page]");
  if (!root) return;
  root.querySelectorAll("[data-return-status-form]").forEach((form) => {
    form.addEventListener("submit", async (event) => {
      event.preventDefault();
      const id = form.getAttribute("data-return-id");
      const status = form.querySelector("select[name=status]").value;
      const restock = form.querySelector("input[name=restock]").checked;
      const response = form.querySelector("input[name=response]").value || "İnceleme tamamlandı.";
      const submitBtn = form.querySelector("[type=submit]");
      submitBtn.disabled = true;
      const { ok, data } = await postForm(`/admin/returns/${id}/status?status=${status}&restock=${restock}`, { response });
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
