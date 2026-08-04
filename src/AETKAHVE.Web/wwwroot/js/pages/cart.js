// AETERNUM RECTUS LUCIS — Cart page
// Miktar/kaldır/kupon mutasyonları; başarılı mutasyondan sonra sayfa yeniden yüklenir
// (sepet toplamlarının sunucudan gelen tek doğru kaynağı yansıtması için).

import { postCommerce } from "/js/core/commerce-api.js";
import { showToast } from "/js/components/toast.js";

async function mutate(url, body, button) {
  if (button) button.disabled = true;
  const { ok, data } = await postCommerce(url, body);
  if (!ok) {
    showToast(data?.message || "İşlem başarısız.", "error");
    if (button) button.disabled = false;
    return false;
  }
  window.location.reload();
  return true;
}

function initQuantitySteppers(root) {
  root.querySelectorAll("[data-cart-line]").forEach((line) => {
    const itemId = line.getAttribute("data-item-id");
    const input = line.querySelector("[data-quantity-input]");
    const decrease = line.querySelector("[data-quantity-decrease]");
    const increase = line.querySelector("[data-quantity-increase]");
    const available = Number(input?.getAttribute("max") || "99");

    const apply = (quantity) => mutate(`/cart/items/${itemId}/quantity`, { quantity }, increase);

    decrease?.addEventListener("click", () => {
      const next = Math.max(0, Number(input.value || "1") - 1);
      apply(next);
    });
    increase?.addEventListener("click", () => {
      const next = Math.min(available, Number(input.value || "1") + 1);
      apply(next);
    });
    input?.addEventListener("change", () => {
      const next = Math.max(0, Math.min(available, Number(input.value || "1")));
      apply(next);
    });
  });

  root.querySelectorAll("[data-cart-remove]").forEach((button) => {
    button.addEventListener("click", () => mutate(button.getAttribute("data-cart-remove"), undefined, button));
  });
}

function initCoupon(root) {
  const form = root.querySelector("[data-coupon-form]");
  form?.addEventListener("submit", (event) => {
    event.preventDefault();
    const input = form.querySelector("input[name=code]");
    if (!input?.value) return;
    mutate("/cart/coupon", { code: input.value }, form.querySelector("button"));
  });

  root.querySelector("[data-coupon-remove]")?.addEventListener("click", (event) => {
    mutate("/cart/coupon/remove", undefined, event.currentTarget);
  });
}

function init() {
  const root = document.querySelector("[data-cart-page]");
  if (!root) return;
  initQuantitySteppers(root);
  initCoupon(root);
}

if (typeof document !== "undefined") {
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
}
