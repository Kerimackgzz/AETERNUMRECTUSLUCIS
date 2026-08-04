import { postCommerce } from "/js/core/commerce-api.js";
import { showToast } from "/js/components/toast.js";

async function submit(form, payload) {
  const button = form.querySelector("[type=submit]");
  button.disabled = true;
  const { ok, data } = await postCommerce(form.dataset.submitUrl, payload);
  const message = data?.message || (ok ? "Talebiniz alındı." : "İşlem tamamlanamadı.");
  showToast(message, ok ? "success" : "error");
  if (ok) window.location.assign(form.dataset.successUrl);
  else button.disabled = false;
}

function init() {
  const root = document.querySelector("[data-order-actions]");
  if (!root) return;

  root.querySelectorAll("[data-review-create]").forEach((form) => {
    form.addEventListener("submit", (event) => {
      event.preventDefault();
      if (!form.reportValidity()) return;
      submit(form, {
        orderItemId: form.dataset.orderItemId,
        rating: Number(form.elements.rating.value),
        comment: form.elements.comment.value,
      });
    });
  });

  root.querySelectorAll("[data-return-create]").forEach((form) => {
    form.addEventListener("submit", (event) => {
      event.preventDefault();
      if (!form.reportValidity()) return;
      const reason = form.elements.reason.value;
      submit(form, {
        orderId: form.dataset.orderId,
        reason,
        description: form.elements.description.value || null,
        items: [{
          orderItemId: form.dataset.orderItemId,
          quantity: Number(form.elements.quantity.value),
          reason,
          condition: Number(form.elements.condition.value),
          imageStorageKey: null,
        }],
      });
    });
  });
}

if (typeof document !== "undefined") {
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
  else init();
}
