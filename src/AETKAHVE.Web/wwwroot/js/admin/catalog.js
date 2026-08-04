import { postCommerce } from "/js/core/commerce-api.js";
import { showToast } from "/js/components/toast.js";

function init() {
  const root = document.querySelector("[data-catalog-page]");
  if (!root) return;

  root.querySelectorAll("[data-catalog-form]").forEach((form) => {
    form.addEventListener("submit", async (event) => {
      event.preventDefault();
      const submitBtn = form.querySelector("[type=submit]");
      submitBtn.disabled = true;
      const name = form.querySelector("[name=name]").value;
      const slug = name
        .toLocaleLowerCase("tr-TR")
        .replace(/ğ/g, "g").replace(/ü/g, "u").replace(/ş/g, "s").replace(/ı/g, "i").replace(/ö/g, "o").replace(/ç/g, "c")
        .replace(/[^a-z0-9]+/g, "-").replace(/(^-|-$)/g, "");
      const payload = {
        kind: form.getAttribute("data-kind"),
        name,
        slug,
        isActive: true,
        countryCode: form.querySelector("[name=countryCode]")?.value || null,
      };
      const { ok, data } = await postCommerce("/admin/catalog", payload);
      if (ok) {
        window.location.reload();
      } else {
        showToast(data?.message || "Kaydedilemedi.", "error");
        submitBtn.disabled = false;
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
