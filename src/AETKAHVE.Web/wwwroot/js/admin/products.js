import { postCommerce } from "/js/core/commerce-api.js";
import { showToast } from "/js/components/toast.js";

function slugify(name) {
  return name
    .toLocaleLowerCase("tr-TR")
    .replace(/ğ/g, "g").replace(/ü/g, "u").replace(/ş/g, "s").replace(/ı/g, "i").replace(/ö/g, "o").replace(/ç/g, "c")
    .replace(/[^a-z0-9]+/g, "-").replace(/(^-|-$)/g, "");
}

function initCreateForm(root) {
  const form = root.querySelector("[data-product-form]");
  if (!form) return;
  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    const submitBtn = form.querySelector("[type=submit]");
    submitBtn.disabled = true;
    const field = (name) => form.querySelector(`[name=${name}]`);
    const name = field("name").value;
    const { ok, data } = await postCommerce("/admin/products", {
      name,
      slug: slugify(name),
      sku: field("sku").value.toUpperCase(),
      shortDescription: field("shortDescription").value,
      description: field("description").value,
      basePrice: Number(field("basePrice").value),
      discountedPrice: field("discountedPrice").value || null,
      taxRate: Number(field("taxRate").value),
      stockQuantity: Number(field("stockQuantity").value),
      criticalStockLevel: Number(field("criticalStockLevel").value || "5"),
      categoryId: field("categoryId").value,
      brandId: field("brandId").value || null,
      coffeeTypeId: field("coffeeTypeId").value || null,
      beanTypeId: field("beanTypeId").value || null,
      roastLevelId: field("roastLevelId").value || null,
      originId: field("originId").value || null,
      isActive: field("isActive").checked,
      isFeatured: field("isFeatured").checked,
    });
    if (ok) {
      window.location.reload();
    } else {
      showToast(data?.message || "Ürün kaydedilemedi.", "error");
      submitBtn.disabled = false;
    }
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
      button.disabled = true;
      const { ok, data } = await postCommerce(`/admin/products/${productId}/stock?delta=${delta}`);
      if (ok) {
        window.location.reload();
      } else {
        showToast(data?.message || "Stok güncellenemedi.", "error");
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
