import { postCommerce } from "/js/core/commerce-api.js";
import { showToast } from "/js/components/toast.js";

function slugify(name) {
  return name
    .toLocaleLowerCase("tr-TR")
    .replace(/ğ/g, "g").replace(/ü/g, "u").replace(/ş/g, "s").replace(/ı/g, "i").replace(/ö/g, "o").replace(/ç/g, "c")
    .replace(/[^a-z0-9]+/g, "-").replace(/(^-|-$)/g, "");
}

function init() {
  const root = document.querySelector("[data-admin-campaigns-page]");
  if (!root) return;
  const form = root.querySelector("[data-campaign-form]");
  form?.addEventListener("submit", async (event) => {
    event.preventDefault();
    const submitBtn = form.querySelector("[type=submit]");
    submitBtn.disabled = true;
    const name = form.querySelector("[name=name]").value;
    const { ok, data } = await postCommerce("/admin/campaigns", {
      name,
      slug: slugify(name),
      discountType: form.querySelector("[name=discountType]").value,
      discountValue: Number(form.querySelector("[name=discountValue]").value),
      minimumCartAmount: form.querySelector("[name=minimumCartAmount]").value || null,
      maximumDiscountAmount: form.querySelector("[name=maximumDiscountAmount]").value || null,
      startDateUtc: new Date(form.querySelector("[name=startDate]").value).toISOString(),
      endDateUtc: new Date(form.querySelector("[name=endDate]").value).toISOString(),
      isActive: true,
      canCombineWithOtherDiscounts: form.querySelector("[name=canCombine]").checked,
    });
    if (ok) {
      window.location.reload();
    } else {
      showToast(data?.message || "Kampanya kaydedilemedi.", "error");
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
