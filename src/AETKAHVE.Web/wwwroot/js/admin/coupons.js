import { postCommerce } from "/js/core/commerce-api.js";
import { showToast } from "/js/components/toast.js";

function init() {
  const root = document.querySelector("[data-admin-coupons-page]");
  if (!root) return;
  const form = root.querySelector("[data-coupon-form]");
  form?.addEventListener("submit", async (event) => {
    event.preventDefault();
    const submitBtn = form.querySelector("[type=submit]");
    submitBtn.disabled = true;
    const { ok, data } = await postCommerce("/admin/coupons", {
      name: form.querySelector("[name=name]").value,
      code: form.querySelector("[name=code]").value.toUpperCase(),
      discountType: form.querySelector("[name=discountType]").value,
      discountValue: Number(form.querySelector("[name=discountValue]").value),
      minimumCartAmount: form.querySelector("[name=minimumCartAmount]").value || null,
      maximumDiscountAmount: form.querySelector("[name=maximumDiscountAmount]").value || null,
      startDateUtc: new Date(form.querySelector("[name=startDate]").value).toISOString(),
      endDateUtc: new Date(form.querySelector("[name=endDate]").value).toISOString(),
      totalUsageLimit: form.querySelector("[name=totalUsageLimit]").value || null,
      perUserUsageLimit: form.querySelector("[name=perUserUsageLimit]").value || null,
      isFirstOrderOnly: form.querySelector("[name=isFirstOrderOnly]").checked,
      isActive: true,
      canCombineWithOtherDiscounts: form.querySelector("[name=canCombine]").checked,
    });
    if (ok) {
      window.location.reload();
    } else {
      showToast(data?.message || "Kupon kaydedilemedi.", "error");
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
