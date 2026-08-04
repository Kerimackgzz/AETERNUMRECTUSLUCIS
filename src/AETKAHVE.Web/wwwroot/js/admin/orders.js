import { postForm } from "/js/core/commerce-api.js";
import { showToast } from "/js/components/toast.js";

function init() {
  const root = document.querySelector("[data-admin-orders-page]");
  if (!root) return;
  root.querySelectorAll("[data-order-status-form]").forEach((form) => {
    form.addEventListener("submit", async (event) => {
      event.preventDefault();
      const orderId = form.getAttribute("data-order-id");
      const status = form.querySelector("select").value;
      const description = form.querySelector("input[name=description]").value || "Admin durum güncellemesi.";
      const submitBtn = form.querySelector("[type=submit]");
      submitBtn.disabled = true;
      const { ok, data } = await postForm(`/admin/orders/${orderId}/status?status=${status}`, { description });
      if (ok) {
        window.location.reload();
      } else {
        showToast(data?.message || "Durum güncellenemedi.", "error");
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
