export const PRODUCT_DRAFT_KEY = "aetkahve.admin.products.create.v1";
export const PRODUCT_DRAFT_VERSION = 1;

const DRAFT_FIELDS = Object.freeze([
  "name", "sku", "shortDescription", "description", "basePrice", "discountedPrice",
  "taxRate", "stockQuantity", "criticalStockLevel", "categoryId", "brandId",
  "coffeeTypeId", "beanTypeId", "roastLevelId", "originId", "isActive", "isFeatured",
]);

export function loadProductDraft(storage) {
  try {
    const raw = storage.getItem(PRODUCT_DRAFT_KEY);
    if (!raw) return null;
    const draft = JSON.parse(raw);
    return draft?.version === PRODUCT_DRAFT_VERSION && draft.values && typeof draft.values === "object"
      ? draft.values
      : null;
  } catch {
    return null;
  }
}

export function saveProductDraft(storage, values) {
  try {
    storage.setItem(PRODUCT_DRAFT_KEY, JSON.stringify({ version: PRODUCT_DRAFT_VERSION, values }));
  } catch {
    // Storage may be disabled by the browser; the form must remain usable.
  }
}

export function clearProductDraft(storage) {
  try {
    storage.removeItem(PRODUCT_DRAFT_KEY);
  } catch {
    // Storage may be disabled by the browser; the form must remain usable.
  }
}

export function captureProductForm(form) {
  return Object.fromEntries(DRAFT_FIELDS.map((name) => {
    const field = form.elements.namedItem(name);
    return [name, field?.type === "checkbox" ? Boolean(field.checked) : String(field?.value ?? "")];
  }));
}

export function restoreProductForm(form, values) {
  if (!values || typeof values !== "object") return false;
  let restored = false;
  DRAFT_FIELDS.forEach((name) => {
    const field = form.elements.namedItem(name);
    if (!field || !Object.hasOwn(values, name)) return;
    if (field.type === "checkbox" && typeof values[name] === "boolean") {
      field.checked = values[name];
      restored = true;
    } else if (field.type !== "checkbox" && typeof values[name] === "string") {
      field.value = values[name];
      restored = true;
    }
  });
  return restored;
}
