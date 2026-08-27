import { showToast } from "/js/components/toast.js";

const RETURN_TOAST_KEY = "aetkahve.returns.toast";

function restoreReturnToast() {
  let serialized;
  try {
    serialized = window.sessionStorage.getItem(RETURN_TOAST_KEY);
    window.sessionStorage.removeItem(RETURN_TOAST_KEY);
  } catch {
    return;
  }

  if (!serialized) return;
  try {
    const notification = JSON.parse(serialized);
    if (typeof notification?.message === "string" && notification.message.trim()) {
      showToast(notification.message.trim(), notification.kind === "error" ? "error" : "success");
    }
  } catch {
    // Ignore malformed or stale browser state.
  }
}

if (typeof document !== "undefined") {
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", restoreReturnToast);
  else restoreReturnToast();
}
