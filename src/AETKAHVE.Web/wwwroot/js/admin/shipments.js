import { commerceErrorMessage, postCommerce } from "/js/core/commerce-api.js";
import { showToast } from "/js/components/toast.js";

const RELOAD_TOAST_KEY = "aetkahve.admin.shipments.reload-toast";

function restoreReloadToast() {
  try {
    const stored = window.sessionStorage.getItem(RELOAD_TOAST_KEY);
    if (!stored) return;
    window.sessionStorage.removeItem(RELOAD_TOAST_KEY);
    const notification = JSON.parse(stored);
    if (typeof notification?.message === "string" && notification.message.trim()) {
      showToast(notification.message.trim(), notification.kind || "success");
    }
  } catch {
    try {
      window.sessionStorage.removeItem(RELOAD_TOAST_KEY);
    } catch {
      // sessionStorage kullanılamıyorsa bildirimi sessizce atla.
    }
  }
}

function reloadWithToast(message) {
  try {
    window.sessionStorage.setItem(RELOAD_TOAST_KEY, JSON.stringify({ message, kind: "success" }));
    window.location.reload();
  } catch {
    showToast(message, "success");
    window.setTimeout(() => window.location.reload(), 1000);
  }
}

function setRowBusy(row, busy) {
  row.querySelectorAll("button, input").forEach((control) => {
    control.disabled = busy;
  });
}

async function runRowAction(button, action, successMessage) {
  const row = button.closest("[data-shipment-row]");
  if (!row || row.dataset.busy === "true") return;

  let reloadRequested = false;
  row.dataset.busy = "true";
  setRowBusy(row, true);
  try {
    const { ok, data } = await action(row);
    if (ok) {
      reloadRequested = true;
      reloadWithToast(data?.message || successMessage);
      return;
    }
    showToast(commerceErrorMessage(data, "Kargo işlemi tamamlanamadı."), "error");
  } catch (error) {
    showToast(error instanceof Error ? error.message : "Kargo işlemi tamamlanamadı.", "error");
  } finally {
    if (!reloadRequested) {
      row.dataset.busy = "false";
      setRowBusy(row, false);
    }
  }
}

function init() {
  const root = document.querySelector("[data-admin-shipments-page]");
  if (!root) return;
  restoreReloadToast();

  root.querySelectorAll("[data-shipment-create]").forEach((button) => {
    button.addEventListener("click", () => runRowAction(button, (row) => postCommerce("/admin/shipments", {
      orderId: row.getAttribute("data-order-id"),
      note: row.querySelector("[data-shipment-note]")?.value || null,
      estimatedDeliveryDateUtc: null,
    }), "Kargo oluşturuldu."));
  });

  root.querySelectorAll("[data-shipment-track]").forEach((button) => {
    button.addEventListener("click", () => runRowAction(button, (row) =>
      postCommerce(`/admin/shipments/${row.getAttribute("data-order-id")}/track`), "Takip güncellendi."));
  });

  root.querySelectorAll("[data-shipment-cancel]").forEach((button) => {
    button.addEventListener("click", () => runRowAction(button, (row) =>
      postCommerce(`/admin/shipments/${row.getAttribute("data-order-id")}/cancel`), "Kargo iptal edildi."));
  });
}

if (typeof document !== "undefined") {
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
}
