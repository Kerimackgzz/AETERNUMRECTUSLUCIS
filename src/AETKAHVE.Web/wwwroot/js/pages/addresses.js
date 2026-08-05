// AETERNUM RECTUS LUCIS — Address book
import { postCommerce } from "/js/core/commerce-api.js";
import { showToast } from "/js/components/toast.js";

function init() {
  const root = document.querySelector("[data-addresses-page]");
  if (!root) return;

  const form = root.querySelector("[data-address-form]");
  form?.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!form.reportValidity()) return;
    const submitBtn = form.querySelector("[type=submit]");
    submitBtn.disabled = true;
    const payload = Object.fromEntries(new FormData(form).entries());
    payload.isDefaultShipping = form.querySelector("[name=isDefaultShipping]").checked;
    payload.isDefaultBilling = form.querySelector("[name=isDefaultBilling]").checked;
    const { ok, data } = await postCommerce("/account/addresses", payload);
    if (ok) {
      window.location.reload();
    } else {
      showToast(data?.message || "Adres kaydedilemedi.", "error");
      submitBtn.disabled = false;
    }
  });

  root.querySelectorAll("[data-address-delete]").forEach((button) => {
    button.addEventListener("click", async () => {
      button.disabled = true;
      const { ok, data } = await postCommerce(button.getAttribute("data-address-delete"));
      if (ok) {
        window.location.reload();
      } else {
        showToast(data?.message || "Adres silinemedi.", "error");
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
