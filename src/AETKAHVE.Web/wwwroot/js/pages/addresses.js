// AETERNUM RECTUS LUCIS — Address book
import { postCommerce } from "/js/core/commerce-api.js";
import { showToast } from "/js/components/toast.js";
import { turkeyProvinces } from "/js/data/turkey-locations.js";

const sortedProvinces = [...turkeyProvinces].sort((a, b) => a.il.localeCompare(b.il, "tr"));

function populateProvinceSelect(select) {
  select.innerHTML = "";
  select.append(new Option("İl seçin", ""));
  for (const province of sortedProvinces) {
    select.append(new Option(province.il, province.il));
  }
}

function populateDistrictSelect(select, provinceName) {
  const province = sortedProvinces.find((item) => item.il === provinceName);
  select.innerHTML = "";
  if (!province) {
    select.append(new Option("Önce il seçin", ""));
    select.disabled = true;
    return;
  }
  select.append(new Option("İlçe seçin", ""));
  for (const district of [...province.ilceler].sort((a, b) => a.localeCompare(b, "tr"))) {
    select.append(new Option(district, district));
  }
  select.disabled = false;
}

function initProvinceDistrictCascade(root) {
  const provinceSelect = root.querySelector("[data-province-select]");
  const districtSelect = root.querySelector("[data-district-select]");
  if (!provinceSelect || !districtSelect) return;

  populateProvinceSelect(provinceSelect);
  populateDistrictSelect(districtSelect, "");
  provinceSelect.addEventListener("change", () => {
    populateDistrictSelect(districtSelect, provinceSelect.value);
  });
}

function init() {
  const root = document.querySelector("[data-addresses-page]");
  if (!root) return;

  initProvinceDistrictCascade(root);

  const form = root.querySelector("[data-address-form]");
  form?.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!form.reportValidity()) return;
    const submitBtn = form.querySelector("[type=submit]");
    submitBtn.disabled = true;
    const payload = Object.fromEntries(new FormData(form).entries());
    payload.isDefaultShipping = form.querySelector("[name=isDefaultShipping]").checked;
    payload.isDefaultBilling = form.querySelector("[name=isDefaultBilling]").checked;
    const { ok, data } = await postCommerce("/account/addresses", payload);
    if (ok) {
      window.location.reload();
    } else {
      showToast(data?.message || "Adres kaydedilemedi.", "error");
      submitBtn.disabled = false;
    }
  });

  root.querySelectorAll("[data-address-delete]").forEach((button) => {
    button.addEventListener("click", async () => {
      button.disabled = true;
      const { ok, data } = await postCommerce(button.getAttribute("data-address-delete"));
      if (ok) {
        window.location.reload();
      } else {
        showToast(data?.message || "Adres silinemedi.", "error");
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
