import { postCommerce } from "/js/core/commerce-api.js";
import { showToast } from "/js/components/toast.js";

function init() {
  const root = document.querySelector("[data-admin-messages-page]");
  if (!root) return;
  root.querySelectorAll("[data-message-status]").forEach((select) => {
    select.addEventListener("change", async () => {
      const id = select.getAttribute("data-message-id");
      select.disabled = true;
      const { ok, data } = await postCommerce(`/admin/messages/${id}/status?status=${select.value}`);
      showToast(data?.message || (ok ? "Durum güncellendi." : "Güncellenemedi."), ok ? "success" : "error");
      select.disabled = false;
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
