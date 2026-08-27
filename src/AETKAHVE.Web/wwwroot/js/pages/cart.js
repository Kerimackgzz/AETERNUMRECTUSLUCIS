// AETERNUM RECTUS LUCIS — Cart page
// Miktar/kaldır/kupon mutasyonları; başarılı mutasyondan sonra sayfa yeniden yüklenir
// (sepet toplamlarının sunucudan gelen tek doğru kaynağı yansıtması için).

import { postCommerce } from "/js/core/commerce-api.js";
import { showToast } from "/js/components/toast.js";

const inFlightLines = new WeakSet();
const previousControlStates = new WeakMap();

async function mutate(url, body, button) {
  if (button) button.disabled = true;
  try {
    const { ok, data } = await postCommerce(url, body);
    if (!ok) {
      showToast(data?.message || "İşlem başarısız.", "error");
      if (button) button.disabled = false;
      return false;
    }
    window.location.reload();
    return true;
  } catch (error) {
    if (error?.message !== "unauthenticated") {
      showToast("İşlem sırasında bağlantı hatası oluştu.", "error");
    }
    if (button) button.disabled = false;
    return false;
  }
}

function setLineLocked(line, locked) {
  if (locked) {
    const states = new Map();
    line.querySelectorAll("button, input, select, textarea").forEach((control) => {
      states.set(control, control.disabled);
      control.disabled = true;
    });
    previousControlStates.set(line, states);
    line.setAttribute("aria-busy", "true");
    return;
  }

  previousControlStates.get(line)?.forEach((wasDisabled, control) => {
    control.disabled = wasDisabled;
  });
  previousControlStates.delete(line);
  line.removeAttribute("aria-busy");
}

async function mutateLine(line, url, body) {
  if (inFlightLines.has(line)) return false;

  inFlightLines.add(line);
  setLineLocked(line, true);
  const succeeded = await mutate(url, body);
  if (!succeeded) {
    inFlightLines.delete(line);
    setLineLocked(line, false);
  }
  return succeeded;
}

function initQuantitySteppers(root) {
  root.querySelectorAll("[data-cart-line]").forEach((line) => {
    const itemId = line.getAttribute("data-item-id");
    const input = line.querySelector("[data-quantity-input]");
    const decrease = line.querySelector("[data-quantity-decrease]");
    const increase = line.querySelector("[data-quantity-increase]");
    const remove = line.querySelector("[data-cart-remove]");
    const available = Number(input?.getAttribute("max") || "99");

    const apply = (quantity) => mutateLine(line, `/cart/items/${itemId}/quantity`, { quantity });

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
    remove?.addEventListener("click", () => {
      mutateLine(line, remove.getAttribute("data-cart-remove"));
    });
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
