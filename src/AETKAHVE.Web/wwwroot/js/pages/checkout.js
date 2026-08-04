// AETERNUM RECTUS LUCIS — Checkout
// Sipariş başlatma (POST /checkout) + mock ödeme callback'ini takip edip sipariş sayfasına yönlendirme.

import { postCommerce, getJson } from "/js/core/commerce-api.js";
import { showToast } from "/js/components/toast.js";

function init() {
  const root = document.querySelector("[data-checkout-page]");
  if (!root) return;
  const form = root.querySelector("[data-checkout-form]");
  const submitBtn = form?.querySelector("[type=submit]");

  form?.addEventListener("submit", async (event) => {
    event.preventDefault();
    const shippingAddressId = form.querySelector("input[name=shippingAddressId]:checked")?.value;
    const billingAddressId = form.querySelector("input[name=billingAddressId]:checked")?.value;
    if (!shippingAddressId || !billingAddressId) {
      showToast("Lütfen teslimat ve fatura adresi seçin.", "error");
      return;
    }

    submitBtn.disabled = true;
    const { ok, data } = await postCommerce("/checkout", {
      cartId: root.getAttribute("data-cart-id"),
      shippingAddressId,
      billingAddressId,
      idempotencyKey: root.getAttribute("data-idempotency-key"),
      customerNote: form.querySelector("textarea[name=customerNote]")?.value || null,
    });

    if (!ok || !data?.data) {
      showToast(data?.message || "Ödeme başlatılamadı.", "error");
      submitBtn.disabled = false;
      return;
    }

    const initialization = data.data;
    const callbackUrl = `${initialization.callbackUrl}?reference=${encodeURIComponent(initialization.requestReference)}&status=success`;
    const completion = await getJson(callbackUrl);

    // PaymentStatus.Succeeded = 2 (Domain/Commerce/CommerceEnums.cs); backend enum'ları
    // string olarak değil sıra numarasıyla serileştiriyor (JsonStringEnumConverter kayıtlı değil).
    const PAYMENT_STATUS_SUCCEEDED = 2;
    if (completion.ok && completion.data?.paymentStatus === PAYMENT_STATUS_SUCCEEDED) {
      window.location.href = `/account/orders/${initialization.orderId}`;
    } else {
      showToast(completion.data?.message || "Ödeme tamamlanamadı.", "error");
      submitBtn.disabled = false;
    }
  });
}

if (typeof document !== "undefined") {
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
}
