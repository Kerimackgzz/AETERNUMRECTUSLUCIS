// AETERNUM RECTUS LUCIS — Fiyat aralığı çift-tutamaçlı slider
// İki üst üste binen input[type=range]'i Min/Maks number input'larıyla
// çift yönlü senkronize eder; gerçek submit değeri her zaman number input'lardan gider,
// slider yalnızca görsel/etkileşimli bir kısayoldur (form JS'siz de çalışır).

function clampToStep(value, min, max, step) {
  const snapped = Math.round((value - min) / step) * step + min;
  return Math.min(max, Math.max(min, snapped));
}

function initSlider(root) {
  const max = Number(root.getAttribute("data-price-max") || "2000");
  const minRange = root.querySelector("[data-price-slider-min]");
  const maxRange = root.querySelector("[data-price-slider-max]");
  const fill = root.querySelector("[data-price-range]");
  const form = root.closest("form");
  const minNumber = form?.querySelector('[data-price-input="min"]');
  const maxNumber = form?.querySelector('[data-price-input="max"]');
  if (!minRange || !maxRange || !fill) return;

  const step = Number(minRange.step || "10");

  function updateFill() {
    const minVal = Number(minRange.value);
    const maxVal = Number(maxRange.value);
    fill.style.left = `${(minVal / max) * 100}%`;
    fill.style.right = `${100 - (maxVal / max) * 100}%`;
  }

  minRange.addEventListener("input", () => {
    if (Number(minRange.value) > Number(maxRange.value)) minRange.value = maxRange.value;
    if (minNumber) minNumber.value = minRange.value;
    updateFill();
  });

  maxRange.addEventListener("input", () => {
    if (Number(maxRange.value) < Number(minRange.value)) maxRange.value = minRange.value;
    if (maxNumber) maxNumber.value = maxRange.value;
    updateFill();
  });

  minNumber?.addEventListener("input", () => {
    const raw = minNumber.value === "" ? 0 : Number(minNumber.value);
    const snapped = clampToStep(raw, 0, Number(maxRange.value), step);
    minRange.value = String(snapped);
    updateFill();
  });

  maxNumber?.addEventListener("input", () => {
    const raw = maxNumber.value === "" ? max : Number(maxNumber.value);
    const snapped = clampToStep(raw, Number(minRange.value), max, step);
    maxRange.value = String(snapped);
    updateFill();
  });

  updateFill();
}

function init() {
  document.querySelectorAll("[data-price-slider]").forEach(initSlider);
}

if (typeof document !== "undefined") {
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
}
