import { postCommerce } from "/js/core/commerce-api.js";
import { showToast } from "/js/components/toast.js";

function init() {
  const root = document.querySelector("[data-admin-products-page]");
  if (!root) return;
  root.querySelectorAll("[data-stock-adjust]").forEach((button) => {
    button.addEventListener("click", async () => {
      const delta = Number(button.getAttribute("data-stock-adjust"));
      const productId = button.closest("[data-product-row]").getAttribute("data-product-id");
      button.disabled = true;
      const { ok, data } = await postCommerce(`/admin/products/${productId}/stock?delta=${delta}`);
      if (ok) {
        window.location.reload();
      } else {
        showToast(data?.message || "Stok güncellenemedi.", "error");
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
