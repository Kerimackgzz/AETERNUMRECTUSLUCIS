import { commerceErrorMessage, postCommerce } from "/js/core/commerce-api.js";
import { nullableNumber, readField } from "/js/admin/admin-form-utils.js";
import {
  captureProductForm,
  clearProductDraft,
  loadProductDraft,
  restoreProductForm,
  saveProductDraft,
} from "/js/admin/admin-product-draft.js";
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

function initDraft(form, panel) {
  const restored = restoreProductForm(form, loadProductDraft(window.sessionStorage));
  if (restored && panel) panel.open = true;

  const persist = () => saveProductDraft(window.sessionStorage, captureProductForm(form));
  form.addEventListener("input", persist);
  form.addEventListener("change", persist);

  const clearButton = form.querySelector("[data-product-draft-clear]");
  clearButton?.addEventListener("click", () => {
    clearProductDraft(window.sessionStorage);
    form.reset();
    if (panel) panel.open = true;
    showToast("Ürün taslağı temizlendi.", "info");
  });
}

function initCreateForm(root) {
  const form = root.querySelector("[data-product-form]");
  if (!form) return;
  initDraft(form, root.querySelector("[data-product-create-panel]"));

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!form.reportValidity()) return;

    const submitBtn = form.querySelector("[type=submit]");
    submitBtn.disabled = true;
    try {
      const name = field(form, "name").value;
      const { ok, data } = await postCommerce("/admin/products", {
        name,
        slug: slugify(name),
        sku: field(form, "sku").value.toUpperCase(),
        shortDescription: field(form, "shortDescription").value,
        description: field(form, "description").value,
        basePrice: Number(field(form, "basePrice").value),
        discountedPrice: readField(
          field(form, "discountedPrice"),
          (value) => nullableNumber(value, { fieldName: "İndirimli fiyat", minimum: 0 })),
        taxRate: Number(field(form, "taxRate").value),
        stockQuantity: Number(field(form, "stockQuantity").value),
        criticalStockLevel: Number(field(form, "criticalStockLevel").value || "5"),
        categoryId: field(form, "categoryId").value,
        brandId: field(form, "brandId").value || null,
        coffeeTypeId: field(form, "coffeeTypeId").value || null,
        beanTypeId: field(form, "beanTypeId").value || null,
        roastLevelId: field(form, "roastLevelId").value || null,
        originId: field(form, "originId").value || null,
        isActive: field(form, "isActive").checked,
        isFeatured: field(form, "isFeatured").checked,
      });
      if (ok) {
        clearProductDraft(window.sessionStorage);
        window.location.reload();
        return;
      }

      showToast(commerceErrorMessage(data, "Ürün kaydedilemedi."), "error");
    } catch (error) {
      showToast(error instanceof Error ? error.message : "Ürün kaydedilemedi.", "error");
    }
    submitBtn.disabled = false;
  });
}

function init() {
  const root = document.querySelector("[data-admin-products-page]");
  if (!root) return;
  initCreateForm(root);
  root.querySelectorAll("[data-stock-adjust]").forEach((button) => {
    button.addEventListener("click", async () => {
      const delta = Number(button.getAttribute("data-stock-adjust"));
      const productId = button.closest("[data-product-row]").getAttribute("data-product-id");
      const actions = button.closest(".stock-actions");
      const actionButtons = actions ? [...actions.querySelectorAll("[data-stock-adjust]")] : [button];
      actionButtons.forEach((actionButton) => { actionButton.disabled = true; });
      actions?.setAttribute("aria-busy", "true");
      const { ok, data } = await postCommerce(`/admin/products/${productId}/stock?delta=${delta}`);
      if (ok) {
        window.location.reload();
      } else {
        showToast(commerceErrorMessage(data, "Stok güncellenemedi."), "error");
        actionButtons.forEach((actionButton) => { actionButton.disabled = false; });
        actions?.removeAttribute("aria-busy");
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
