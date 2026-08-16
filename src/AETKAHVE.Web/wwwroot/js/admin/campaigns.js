import { commerceErrorMessage, postCommerce } from "/js/core/commerce-api.js";
import {
  clearCustomValidityOnInput,
  discountTypeJsonValue,
  guidList,
  localDateIso,
  nullableNumber,
  readField,
  requiredNumber,
  syncDiscountValue,
  validatePromotion,
} from "/js/admin/admin-form-utils.js";
import { showToast } from "/js/components/toast.js";

function slugify(name) {
  return name
    .toLocaleLowerCase("tr-TR")
    .replace(/ğ/g, "g").replace(/ü/g, "u").replace(/ş/g, "s").replace(/ı/g, "i").replace(/ö/g, "o").replace(/ç/g, "c")
    .replace(/[^a-z0-9]+/g, "-").replace(/(^-|-$)/g, "");
}

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
  const name = field(form, "name").value;

  return {
    name,
    slug: slugify(name),
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
    isActive: true,
    canCombineWithOtherDiscounts: field(form, "canCombine").checked,
    productIds: readField(field(form, "productIds"), (value) => guidList(value, "Hedef ürünler")),
    categoryIds: readField(field(form, "categoryIds"), (value) => guidList(value, "Hedef kategoriler")),
  };
}

function init() {
  const root = document.querySelector("[data-admin-campaigns-page]");
  if (!root) return;
  const form = root.querySelector("[data-campaign-form]");
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
      const { ok, data } = await postCommerce("/admin/campaigns", buildPayload(form));
      if (ok) {
        window.location.reload();
        return;
      }
      showToast(commerceErrorMessage(data, "Kampanya kaydedilemedi."), "error");
    } catch (error) {
      showToast(error instanceof Error ? error.message : "Kampanya kaydedilemedi.", "error");
    }
    submitBtn.disabled = false;
  });
}

if (typeof document !== "undefined") {
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
  else init();
}
