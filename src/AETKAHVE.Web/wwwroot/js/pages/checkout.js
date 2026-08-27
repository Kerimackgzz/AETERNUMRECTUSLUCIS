// AETERNUM RECTUS LUCIS — Checkout
// Sipariş başlatma (POST /checkout) + mock ödeme callback'ini takip edip sipariş sayfasına yönlendirme.

import { postCommerce, getJson } from "/js/core/commerce-api.js";
import { showToast } from "/js/components/toast.js";

function init() {
  const root = document.querySelector("[data-checkout-page]");
  if (!root) return;
  // data-checkout-form ve data-checkout-page aynı <form> elemanında olabilir —
  // querySelector yalnız alt elemanlara bakar, kendi kendini bulamaz.
  const form = root.matches("[data-checkout-form]") ? root : root.querySelector("[data-checkout-form]");
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
      if (data?.data?.cartReviewRequired && data.data.redirectUrl) {
        window.location.assign(data.data.redirectUrl);
        return;
      }
      submitBtn.disabled = false;
      return;
    }

    const initialization = data.data;

    // Gerçek (harici) bir ödeme sağlayıcısı (ör. Stripe) döndüyse RedirectUrl bizim
    // origin'imizde değildir — kullanıcıyı sağlayıcının barındırdığı gerçek ödeme
    // sayfasına gönderiyoruz. Mock gibi eşzamanlı/same-origin sağlayıcılarda RedirectUrl
    // zaten kendi callback URL'imize işaret eder; bu durumda mevcut anlık-tamamlama akışı
    // (aşağıda) değişmeden çalışmaya devam eder.
    if (initialization.redirectUrl) {
      try {
        const target = new URL(initialization.redirectUrl, window.location.href);
        if (target.origin !== window.location.origin) {
          window.location.href = target.href;
          return;
        }
      } catch {
        // Geçersiz bir URL gelirse mock akışına düş.
      }
    }

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
