const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const DISCOUNT_TYPE_JSON_VALUES = Object.freeze({ Percentage: 0, FixedAmount: 1, FreeShipping: 2 });

export function discountTypeJsonValue(value) {
  if (!Object.hasOwn(DISCOUNT_TYPE_JSON_VALUES, value)) throw new Error("Geçerli bir indirim türü seçin.");
  return DISCOUNT_TYPE_JSON_VALUES[value];
}

export function nullableNumber(rawValue, { fieldName, integer = false, minimum = 0, exclusiveMinimum = false } = {}) {
  const value = String(rawValue ?? "").trim();
  if (!value) return null;

  const parsed = Number(value);
  const label = fieldName || "Değer";
  if (!Number.isFinite(parsed) || (integer && !Number.isInteger(parsed))) {
    throw new Error(`${label} geçerli ${integer ? "bir tam sayı" : "bir sayı"} olmalıdır.`);
  }

  const outsideRange = exclusiveMinimum ? parsed <= minimum : parsed < minimum;
  if (outsideRange) {
    throw new Error(`${label} ${exclusiveMinimum ? `${minimum}'dan büyük` : `en az ${minimum}`} olmalıdır.`);
  }

  return parsed;
}

export function requiredNumber(rawValue, options = {}) {
  const parsed = nullableNumber(rawValue, options);
  if (parsed === null) throw new Error(`${options.fieldName || "Değer"} zorunludur.`);
  return parsed;
}

export function guidList(rawValue, fieldName) {
  const values = String(rawValue ?? "")
    .split(/[\s,]+/)
    .map((value) => value.trim())
    .filter(Boolean);

  const invalid = values.find((value) => !GUID_PATTERN.test(value));
  if (invalid) throw new Error(`${fieldName} içinde geçersiz bir ID var: ${invalid}`);
  return values.length ? [...new Set(values.map((value) => value.toLowerCase()))] : null;
}

export function localDateIso(rawValue, fieldName) {
  const date = new Date(rawValue);
  if (!rawValue || Number.isNaN(date.getTime())) throw new Error(`${fieldName} geçerli bir tarih olmalıdır.`);
  return date.toISOString();
}

export function validatePromotion(discountType, discountValue, startDateIso, endDateIso) {
  if (new Date(endDateIso) <= new Date(startDateIso)) {
    throw new Error("Bitiş tarihi başlangıç tarihinden sonra olmalıdır.");
  }

  if (discountType === "FreeShipping") {
    if (discountValue !== 0) throw new Error("Ücretsiz kargo indiriminin değeri 0 olmalıdır.");
    return;
  }

  if (discountValue <= 0) throw new Error("İndirim değeri 0'dan büyük olmalıdır.");
  if (discountType === "Percentage" && discountValue > 100) {
    throw new Error("Yüzde indirimi 100'den büyük olamaz.");
  }
}

export function readField(field, reader) {
  field.setCustomValidity("");
  try {
    return reader(field.value);
  } catch (error) {
    field.setCustomValidity(error instanceof Error ? error.message : "Geçersiz değer.");
    field.reportValidity();
    throw error;
  }
}

export function clearCustomValidityOnInput(form) {
  form.addEventListener("input", (event) => {
    if (typeof event.target?.setCustomValidity === "function") event.target.setCustomValidity("");
  });
}

export function syncDiscountValue(discountTypeField, discountValueField) {
  const freeShipping = discountTypeField.value === "FreeShipping";
  if (freeShipping) discountValueField.value = "0";
  discountValueField.readOnly = freeShipping;
  discountValueField.setAttribute("aria-readonly", String(freeShipping));
}
