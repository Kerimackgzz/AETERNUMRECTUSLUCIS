import { postCommerce } from "/js/core/commerce-api.js";
import { showToast } from "/js/components/toast.js";

function init() {
  const root = document.querySelector("[data-admin-shipments-page]");
  if (!root) return;

  root.querySelector("[data-shipment-create-form]")?.addEventListener("submit", async (event) => {
    event.preventDefault();
    const form = event.currentTarget;
    const submitBtn = form.querySelector("[type=submit]");
    submitBtn.disabled = true;
    const { ok, data } = await postCommerce("/admin/shipments", {
      orderId: form.querySelector("[name=orderId]").value,
      note: form.querySelector("[name=note]").value || null,
    });
    if (ok) {
      window.location.reload();
    } else {
      showToast(data?.message || "Kargo oluşturulamadı.", "error");
      submitBtn.disabled = false;
    }
  });

  root.querySelectorAll("[data-shipment-track]").forEach((button) => {
    button.addEventListener("click", async () => {
      button.disabled = true;
      const { ok, data } = await postCommerce(`/admin/shipments/${button.getAttribute("data-order-id")}/track`);
      showToast(data?.message || (ok ? "Takip güncellendi." : "İşlem başarısız."), ok ? "success" : "error");
      button.disabled = false;
    });
  });

  root.querySelectorAll("[data-shipment-cancel]").forEach((button) => {
    button.addEventListener("click", async () => {
      button.disabled = true;
      const { ok, data } = await postCommerce(`/admin/shipments/${button.getAttribute("data-order-id")}/cancel`);
      if (ok) {
        window.location.reload();
      } else {
        showToast(data?.message || "İptal edilemedi.", "error");
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
