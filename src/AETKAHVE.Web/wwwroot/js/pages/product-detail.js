// AETERNUM RECTUS LUCIS — Product detail
// Galeri/varyant seçimi + sepete ekleme/favori (product-card-actions.js üzerinden).

import { addToCart, toggleFavorite } from "/js/components/product-card-actions.js";

function updateQuantityLimit(root, availableQuantity) {
  const input = root.querySelector("[data-quantity-stepper] input");
  if (!input) return 0;

  const configuredMaximum = Number(root.getAttribute("data-maximum-cart-quantity") || "1");
  const available = Number.isFinite(availableQuantity) ? Math.max(0, availableQuantity) : 0;
  const maximum = Math.max(0, Math.min(available, configuredMaximum));
  input.max = String(maximum);
  input.disabled = maximum <= 0;
  input.value = maximum <= 0
    ? "0"
    : String(Math.max(1, Math.min(maximum, Number(input.value || "1"))));
  return maximum;
}

function initGallery(root) {
  const main = root.querySelector("[data-detail-main-image]");
  const thumbs = root.querySelectorAll("[data-detail-thumb]");
  thumbs.forEach((thumb) => {
    thumb.addEventListener("click", () => {
      if (!main) return;
      main.src = thumb.getAttribute("data-image-url");
      main.alt = thumb.getAttribute("data-image-alt") || main.alt;
      thumbs.forEach((t) => t.setAttribute("aria-current", "false"));
      thumb.setAttribute("aria-current", "true");
    });
  });
}

function initVariants(root) {
  const chips = root.querySelectorAll("[data-variant-chip]");
  const variantInput = root.querySelector("[data-selected-variant]");
  const priceEl = root.querySelector("[data-detail-price]");
  const originalPriceEl = root.querySelector("[data-detail-original-price]");
  const addBtn = root.querySelector("[data-detail-add-to-cart]");

  chips.forEach((chip) => {
    chip.addEventListener("click", () => {
      if (chip.disabled) return;
      chips.forEach((c) => c.setAttribute("aria-pressed", "false"));
      chip.setAttribute("aria-pressed", "true");
      if (variantInput) variantInput.value = chip.getAttribute("data-variant-id") || "";
      if (priceEl) priceEl.textContent = chip.getAttribute("data-display-price") || priceEl.textContent;
      const original = chip.getAttribute("data-original-price");
      if (originalPriceEl) {
        if (original) {
          originalPriceEl.textContent = original;
          originalPriceEl.hidden = false;
        } else {
          originalPriceEl.hidden = true;
        }
      }
      const available = Number(chip.getAttribute("data-available-quantity") || "0");
      const maximum = updateQuantityLimit(root, available);
      if (addBtn) addBtn.disabled = maximum <= 0;
    });
  });
}

function initQuantity(root) {
  const stepper = root.querySelector("[data-quantity-stepper]");
  if (!stepper) return;
  const input = stepper.querySelector("input");
  if (!input) return;
  stepper.querySelector("[data-quantity-decrease]")?.addEventListener("click", () => {
    input.value = String(Math.max(1, Number(input.value || "1") - 1));
  });
  stepper.querySelector("[data-quantity-increase]")?.addEventListener("click", () => {
    const maximum = Number(input.max || "1");
    input.value = String(Math.min(maximum, Number(input.value || "1") + 1));
  });
  input.addEventListener("change", () => {
    const maximum = Number(input.max || "1");
    input.value = String(Math.max(1, Math.min(maximum, Number(input.value || "1"))));
  });
}

function initAddToCart(root) {
  const addBtn = root.querySelector("[data-detail-add-to-cart]");
  if (!addBtn) return;
  addBtn.addEventListener("click", async () => {
    const productId = root.getAttribute("data-product-id");
    const variantInput = root.querySelector("[data-selected-variant]");
    const quantityInput = root.querySelector("[data-quantity-stepper] input");
    addBtn.disabled = true;
    await addToCart(addBtn.getAttribute("data-add-to-cart-url"), productId, variantInput?.value || null, Number(quantityInput?.value || "1"));
    addBtn.disabled = false;
  });
}

function initFavorite(root) {
  const favBtn = root.querySelector("[data-detail-favorite]");
  if (!favBtn) return;
  favBtn.addEventListener("click", async () => {
    favBtn.disabled = true;
    const result = await toggleFavorite(favBtn.getAttribute("data-toggle-favorite-url"));
    favBtn.disabled = false;
    if (result.ok && result.data?.data) {
      const isFavorite = Boolean(result.data.data.isFavorite);
      favBtn.setAttribute("aria-pressed", String(isFavorite));
      favBtn.setAttribute("aria-label", isFavorite ? "Favorilerden çıkar" : "Favorilere ekle");
    }
  });
}

function init() {
  const root = document.querySelector("[data-product-detail]");
  if (!root) return;
  initGallery(root);
  initVariants(root);
  initQuantity(root);
  initAddToCart(root);
  initFavorite(root);
}

if (typeof document !== "undefined") {
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
}
