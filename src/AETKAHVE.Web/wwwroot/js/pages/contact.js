import { postCommerce } from "/js/core/commerce-api.js";
import { showToast } from "/js/components/toast.js";

function init() {
  const form = document.querySelector("[data-contact-form]");
  if (!form) return;

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!form.reportValidity()) return;

    const submitButton = form.querySelector("[type=submit]");
    const status = form.querySelector("[data-contact-status]");
    const values = Object.fromEntries(new FormData(form).entries());
    values.privacyAccepted = form.querySelector("[name=PrivacyAccepted]").checked;
    delete values.PrivacyAccepted;

    submitButton.disabled = true;
    const { ok, data } = await postCommerce(form.action, values);
    const message = data?.message || (ok ? "Mesajınız alındı." : "Mesaj gönderilemedi.");
    status.textContent = message;
    showToast(message, ok ? "success" : "error");
    if (ok) form.reset();
    submitButton.disabled = false;
  });
}

if (typeof document !== "undefined") {
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
  else init();
}
