// AETERNUM RECTUS LUCIS — ProductCard actions (add to cart / favorite)
// Ajan 1: gerçek sepet/favori davranışı. Motion katmanı product-card-motion.js'dedir, burada değiştirilmez.

import { postCommerce } from "/js/core/commerce-api.js";
import { showToast } from "/js/components/toast.js";

export async function addToCart(url, productId, variantId, quantity) {
  const { ok, data } = await postCommerce(url, { productId, variantId: variantId || null, quantity: quantity || 1 });
  showToast(data?.message || (ok ? "Sepete eklendi." : "Sepete eklenemedi."), ok ? "success" : "error");
  return { ok, data };
}

export async function toggleFavorite(url) {
  const { ok, data } = await postCommerce(url);
  showToast(data?.message || (ok ? "Güncellendi." : "İşlem başarısız."), ok ? "success" : "error");
  return { ok, data };
}

function initDelegatedListeners() {
  document.addEventListener("click", async (event) => {
    const addBtn = event.target.closest("[data-add-to-cart-url]");
    if (addBtn && !addBtn.disabled) {
      const card = addBtn.closest("[data-product-card]");
      const productId = card ? card.getAttribute("data-product-id") : null;
      if (productId) {
        addBtn.disabled = true;
        await addToCart(addBtn.getAttribute("data-add-to-cart-url"), productId, null, 1);
        addBtn.disabled = false;
      }
      return;
    }

    const favBtn = event.target.closest("[data-toggle-favorite-url]");
    if (favBtn) {
      favBtn.disabled = true;
      const result = await toggleFavorite(favBtn.getAttribute("data-toggle-favorite-url"));
      favBtn.disabled = false;
      if (result.ok && result.data?.data) {
        const isFavorite = Boolean(result.data.data.isFavorite);
        favBtn.setAttribute("aria-pressed", String(isFavorite));
        favBtn.setAttribute("aria-label", isFavorite ? "Favorilerden çıkar" : "Favorilere ekle");
      }
    }
  });
}

if (typeof document !== "undefined") {
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initDelegatedListeners);
  } else {
    initDelegatedListeners();
  }
}
