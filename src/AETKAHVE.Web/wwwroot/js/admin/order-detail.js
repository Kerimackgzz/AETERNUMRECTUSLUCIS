import { commerceErrorMessage, postForm } from "/js/core/commerce-api.js";
import { showToast } from "/js/components/toast.js";

function init() {
  const root = document.querySelector("[data-admin-order-detail-page]");
  if (!root) return;
  const form = root.querySelector("[data-order-force-status-form]");
  if (!form) return;

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!form.reportValidity()) return;
    if (!window.confirm("Bu, normal durum akışını atlayarak siparişi doğrudan seçtiğin duruma geçirecek. Emin misin?")) return;

    const orderId = form.getAttribute("data-order-id");
    const status = form.querySelector("select[name=status]").value;
    const reason = form.querySelector("input[name=reason]").value;
    const submitBtn = form.querySelector("[type=submit]");
    submitBtn.disabled = true;
    try {
      const { ok, data } = await postForm(`/admin/orders/${orderId}/force-status?status=${status}`, { reason });
      if (ok) {
        window.location.reload();
        return;
      }
      showToast(commerceErrorMessage(data, "Durum güncellenemedi."), "error");
    } catch (error) {
      showToast(error instanceof Error ? error.message : "Durum güncellenemedi.", "error");
    }
    submitBtn.disabled = false;
  });

  const deleteButton = root.querySelector("[data-order-delete-button]");
  if (deleteButton) {
    deleteButton.addEventListener("click", async () => {
      const orderId = deleteButton.getAttribute("data-order-id");
      if (!window.confirm("Bu siparişi ve ona bağlı ödeme/kargo/iade/yorum kayıtlarının hepsini KALICI OLARAK sileceksin. Bu işlem geri alınamaz. Devam etmek istediğine emin misin?")) {
        return;
      }
      deleteButton.disabled = true;
      try {
        const { ok, data } = await postForm(`/admin/orders/${orderId}/delete`, {});
        if (ok) {
          window.location.href = "/admin/orders";
          return;
        }
        showToast(commerceErrorMessage(data, "Sipariş silinemedi."), "error");
      } catch (error) {
        showToast(error instanceof Error ? error.message : "Sipariş silinemedi.", "error");
      }
      deleteButton.disabled = false;
    });
  }
}

if (typeof document !== "undefined") {
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
}
