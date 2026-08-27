import { commerceErrorMessage, postCommerce } from "/js/core/commerce-api.js";
import {
  clearCustomValidityOnInput,
  discountTypeJsonValue,
  localDateIso,
  nullableNumber,
  readField,
  requiredNumber,
  syncDiscountValue,
  validatePromotion,
} from "/js/admin/admin-form-utils.js";
import { showToast } from "/js/components/toast.js";

function field(form, name) {
  return form.elements.namedItem(name);
}

function buildPayload(form) {
  const startField = field(form, "startDate");
  const endField = field(form, "endDate");
  const discountTypeField = field(form, "discountType");
  const discountValueField = field(form, "discountValue");
  const startDateUtc = readField(startField, (value) => localDateIso(value, "Başlangıç tarihi"));
  const endDateUtc = readField(endField, (value) => {
    const end = localDateIso(value, "Bitiş tarihi");
    if (new Date(end) <= new Date(startDateUtc)) throw new Error("Bitiş tarihi başlangıç tarihinden sonra olmalıdır.");
    return end;
  });
  const discountValue = readField(discountValueField, (value) => {
    const parsed = requiredNumber(value, { fieldName: "İndirim değeri", minimum: 0 });
    validatePromotion(discountTypeField.value, parsed, startDateUtc, endDateUtc);
    return parsed;
  });

  return {
    name: field(form, "name").value,
    code: field(form, "code").value.toUpperCase(),
    discountType: discountTypeJsonValue(discountTypeField.value),
    discountValue,
    minimumCartAmount: readField(
      field(form, "minimumCartAmount"),
      (value) => nullableNumber(value, { fieldName: "Minimum sepet tutarı", minimum: 0 })),
    maximumDiscountAmount: readField(
      field(form, "maximumDiscountAmount"),
      (value) => nullableNumber(value, { fieldName: "Maksimum indirim", minimum: 0 })),
    startDateUtc,
    endDateUtc,
    totalUsageLimit: readField(
      field(form, "totalUsageLimit"),
      (value) => nullableNumber(value, { fieldName: "Toplam kullanım limiti", integer: true, minimum: 0, exclusiveMinimum: true })),
    perUserUsageLimit: readField(
      field(form, "perUserUsageLimit"),
      (value) => nullableNumber(value, { fieldName: "Kullanıcı başı limit", integer: true, minimum: 0, exclusiveMinimum: true })),
    isFirstOrderOnly: field(form, "isFirstOrderOnly").checked,
    isActive: true,
    canCombineWithOtherDiscounts: field(form, "canCombine").checked,
  };
}

function initActiveToggle(root) {
  root.querySelectorAll("[data-toggle-coupon-active]").forEach((button) => {
    button.addEventListener("click", async () => {
      const id = button.getAttribute("data-toggle-coupon-active");
      const nextActive = button.getAttribute("data-active") !== "true";
      button.disabled = true;
      const { ok, data } = await postCommerce(`/admin/coupons/${id}/active?isActive=${nextActive}`);
      if (ok) {
        window.location.reload();
        return;
      }
      showToast(commerceErrorMessage(data, "Kupon durumu güncellenemedi."), "error");
      button.disabled = false;
    });
  });
}

function init() {
  const root = document.querySelector("[data-admin-coupons-page]");
  if (!root) return;
  initActiveToggle(root);
  const form = root.querySelector("[data-coupon-form]");
  if (!form) return;

  const discountType = field(form, "discountType");
  const discountValue = field(form, "discountValue");
  syncDiscountValue(discountType, discountValue);
  discountType.addEventListener("change", () => syncDiscountValue(discountType, discountValue));
  clearCustomValidityOnInput(form);

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!form.reportValidity()) return;
    const submitBtn = form.querySelector("[type=submit]");
    submitBtn.disabled = true;
    try {
      const { ok, data } = await postCommerce("/admin/coupons", buildPayload(form));
      if (ok) {
        window.location.reload();
        return;
      }
      showToast(commerceErrorMessage(data, "Kupon kaydedilemedi."), "error");
    } catch (error) {
      showToast(error instanceof Error ? error.message : "Kupon kaydedilemedi.", "error");
    }
    submitBtn.disabled = false;
  });
}

if (typeof document !== "undefined") {
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
  else init();
}
