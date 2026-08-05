(function () {
  "use strict";

  var body = document.body;
  var sessionKind = body.getAttribute("data-idle-session");
  if (!sessionKind) {
    return;
  }

  var statusUrl = body.getAttribute("data-session-status-url");
  var keepAliveUrl = body.getAttribute("data-session-keep-alive-url");
  var logoutUrl = body.getAttribute("data-session-logout-url");
  var warningSeconds = parseInt(body.getAttribute("data-idle-warning-seconds"), 10) || 60;

  var dialog = document.querySelector("[data-idle-warning-dialog]");
  var remainingEl = dialog ? dialog.querySelector("[data-idle-remaining]") : null;
  var continueBtn = dialog ? dialog.querySelector("[data-idle-continue]") : null;

  var csrfToken = (function () {
    var meta = document.querySelector('meta[name="csrf-token"]');
    return meta ? meta.getAttribute("content") : "";
  })();

  var expiresAtMs = null;
  var tickHandle = null;
  var statusPollHandle = null;
  var warningVisible = false;
  var focusBeforeWarning = null;

  function loginUrl() {
    return "/" + sessionKind + "/login";
  }

  function applyStatus(data) {
    if (!data || data.isAuthenticated === false) {
      redirectToLogin();
      return;
    }
    var now = Date.parse(data.serverTimeUtc);
    var expires = Date.parse(data.expiresAtUtc);
    var clientNow = Date.now();
    var drift = isNaN(now) ? 0 : clientNow - now;
    expiresAtMs = isNaN(expires) ? null : expires + drift;
  }

  function redirectToLogin() {
    stopTimers();
    window.location.href = loginUrl();
  }

  function fetchStatus(countsAsPoll) {
    if (!statusUrl) {
      return;
    }
    fetch(statusUrl, { credentials: "same-origin", headers: { Accept: "application/json" } })
      .then(function (response) {
        if (response.status === 401) {
          redirectToLogin();
          return null;
        }
        if (!response.ok) {
          return null;
        }
        return response.json();
      })
      .then(function (data) {
        if (data) {
          applyStatus(data);
        }
      })
      .catch(function () {
        /* Ağ hatasında sessizce yeniden dener; sunucu oturumu nihai gerçektir. */
      });
  }

  function keepAlive() {
    if (!keepAliveUrl) {
      return;
    }
    fetch(keepAliveUrl, {
      method: "POST",
      credentials: "same-origin",
      headers: {
        Accept: "application/json",
        RequestVerificationToken: csrfToken
      }
    })
      .then(function (response) {
        if (response.status === 401) {
          redirectToLogin();
          return null;
        }
        if (response.status === 403 || response.status === 429) {
          return null;
        }
        return response.ok ? response.json() : null;
      })
      .then(function (data) {
        if (data) {
          applyStatus(data);
          hideWarning();
        }
      })
      .catch(function () {
        /* sessiz yeniden deneme */
      });
  }

  function showWarning(remaining) {
    if (!dialog) {
      return;
    }
    var wasVisible = warningVisible;
    warningVisible = true;
    dialog.hidden = false;
    updateRemaining(remaining);
    if (!wasVisible) {
      focusBeforeWarning = document.activeElement;
      if (continueBtn) {
        continueBtn.focus();
      }
    }
  }

  function hideWarning() {
    if (!dialog) {
      return;
    }
    var shouldRestoreFocus = warningVisible;
    warningVisible = false;
    dialog.hidden = true;
    if (shouldRestoreFocus && focusBeforeWarning && typeof focusBeforeWarning.focus === "function" && focusBeforeWarning.isConnected) {
      focusBeforeWarning.focus();
    }
    focusBeforeWarning = null;
  }

  function updateRemaining(remainingSeconds) {
    if (remainingEl) {
      remainingEl.textContent = String(Math.max(0, Math.ceil(remainingSeconds)));
    }
  }

  function tick() {
    if (expiresAtMs === null) {
      return;
    }
    var remainingMs = expiresAtMs - Date.now();
    var remainingSeconds = remainingMs / 1000;

    if (remainingSeconds <= 0) {
      stopTimers();
      redirectToLogin();
      return;
    }

    if (remainingSeconds <= warningSeconds) {
      showWarning(remainingSeconds);
    } else if (warningVisible) {
      hideWarning();
    }
  }

  function stopTimers() {
    if (tickHandle) {
      window.clearInterval(tickHandle);
      tickHandle = null;
    }
    if (statusPollHandle) {
      window.clearInterval(statusPollHandle);
      statusPollHandle = null;
    }
  }

  var activityThrottleMs = 15000;
  var lastActivitySent = 0;

  function onUserActivity() {
    if (warningVisible) {
      /* Uyarı görünürken aktivite otomatik oturumu uzatmaz; kullanıcı bilinçli olarak devam etmelidir. */
      return;
    }
    var now = Date.now();
    if (now - lastActivitySent < activityThrottleMs) {
      return;
    }
    lastActivitySent = now;
    keepAlive();
  }

  ["mousemove", "keydown", "click", "scroll", "touchstart"].forEach(function (evt) {
    window.addEventListener(evt, onUserActivity, { passive: true });
  });

  if (continueBtn) {
    continueBtn.addEventListener("click", function () {
      keepAlive();
    });
  }

  if (dialog) {
    dialog.addEventListener("keydown", function (event) {
      if (warningVisible && event.key === "Tab" && continueBtn) {
        event.preventDefault();
        continueBtn.focus();
      }
    });
  }

  fetchStatus(false);
  tickHandle = window.setInterval(tick, 1000);
  statusPollHandle = window.setInterval(function () {
    fetchStatus(true);
  }, 30000);
})();
