(function () {
  "use strict";

  var body = document.body;
  var sessionKind = body.getAttribute("data-idle-session");
  if (sessionKind !== "admin" && sessionKind !== "superadmin") {
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

  var syncVersion = 1;
  var syncName = "aetkahve-management-session-v1:" + sessionKind;
  var storageKey = "aetkahve.management-session.v1:" + sessionKind;
  var sourceId = String(Date.now()) + ":" + String(Math.random());
  var syncChannel = null;
  var expiresAtMs = null;
  var tickHandle = null;
  var statusPollHandle = null;
  var warningVisible = false;
  var endingSession = false;
  var redirectStarted = false;
  var logoutRequestStarted = false;

  function loginUrl() {
    return "/" + sessionKind + "/login";
  }

  function isLogoutForm(form) {
    if (!logoutUrl || !form || form.tagName !== "FORM") {
      return false;
    }
    var method = String(form.getAttribute("method") || form.method || "get").toLowerCase();
    if (method !== "post") {
      return false;
    }

    try {
      var pageUrl = new window.URL(window.location.href);
      var actionUrl = new window.URL(form.getAttribute("action") || form.action || "", pageUrl);
      var expectedUrl = new window.URL(logoutUrl, pageUrl);
      return actionUrl.origin === expectedUrl.origin &&
        actionUrl.pathname === expectedUrl.pathname &&
        actionUrl.search === expectedUrl.search;
    } catch (_error) {
      return false;
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

  function hideWarning() {
    if (!dialog) {
      return;
    }
    warningVisible = false;
    dialog.hidden = true;
  }

  function redirectToLogin() {
    if (redirectStarted) {
      return;
    }
    redirectStarted = true;
    stopTimers();
    if (typeof window.location.replace === "function") {
      window.location.replace(loginUrl());
      return;
    }
    window.location.href = loginUrl();
  }

  function isFreshSyncMessage(message) {
    return message &&
      message.version === syncVersion &&
      message.sessionKind === sessionKind &&
      message.sourceId !== sourceId &&
      typeof message.sentAt === "number" &&
      isFinite(message.sentAt) &&
      Math.abs(Date.now() - message.sentAt) <= 120000;
  }

  function publishSessionEvent(type, status) {
    var message = {
      version: syncVersion,
      eventId: sourceId + ":" + String(Date.now()) + ":" + String(Math.random()),
      sourceId: sourceId,
      sessionKind: sessionKind,
      type: type,
      sentAt: Date.now()
    };
    if (status) {
      message.status = status;
    }

    if (syncChannel) {
      try {
        syncChannel.postMessage(message);
        return;
      } catch (_error) {
        /* Fall through to the storage transport. */
      }
    }

    try {
      window.localStorage.setItem(storageKey, JSON.stringify(message));
    } catch (_error) {
      /* Cross-tab sync is progressive enhancement; server enforcement remains authoritative. */
    }
  }

  function applyStatus(data) {
    if (endingSession) {
      return false;
    }
    if (data && data.isAuthenticated === false) {
      endSessionWithoutLogout("logout", true);
      return false;
    }
    if (!data || data.isAuthenticated !== true ||
        typeof data.serverTimeUtc !== "string" ||
        typeof data.expiresAtUtc !== "string") {
      return false;
    }

    var now = Date.parse(data.serverTimeUtc);
    var expires = Date.parse(data.expiresAtUtc);
    if (isNaN(expires)) {
      return false;
    }

    var clientNow = Date.now();
    var drift = isNaN(now) ? 0 : clientNow - now;
    expiresAtMs = expires + drift;
    return true;
  }

  function receiveSessionEvent(message) {
    if (!isFreshSyncMessage(message) || endingSession) {
      return;
    }

    if (message.type === "logout" || message.type === "expired") {
      endingSession = true;
      stopTimers();
      hideWarning();
      redirectToLogin();
      return;
    }

    if (message.type === "keep-alive" && applyStatus(message.status)) {
      hideWarning();
    }
  }

  function initializeSessionSync() {
    if (typeof window.BroadcastChannel === "function") {
      try {
        syncChannel = new window.BroadcastChannel(syncName);
        if (typeof syncChannel.addEventListener === "function") {
          syncChannel.addEventListener("message", function (event) {
            receiveSessionEvent(event.data);
          });
        } else {
          syncChannel.onmessage = function (event) {
            receiveSessionEvent(event.data);
          };
        }
      } catch (_error) {
        syncChannel = null;
      }
    }

    window.addEventListener("storage", function (event) {
      if (event.key !== storageKey || !event.newValue) {
        return;
      }
      try {
        receiveSessionEvent(JSON.parse(event.newValue));
      } catch (_error) {
        /* Ignore malformed or untrusted same-origin storage values. */
      }
    });
  }

  function endSessionWithoutLogout(reason, publish) {
    if (endingSession) {
      return;
    }
    endingSession = true;
    stopTimers();
    hideWarning();
    if (publish) {
      publishSessionEvent(reason);
    }
    redirectToLogin();
  }

  function postLogoutAndRedirect() {
    if (logoutRequestStarted) {
      return;
    }
    logoutRequestStarted = true;

    if (!logoutUrl) {
      redirectToLogin();
      return;
    }

    var completed = false;
    var controller = typeof window.AbortController === "function"
      ? new window.AbortController()
      : null;
    var timeoutHandle = window.setTimeout(function () {
      if (controller) {
        controller.abort();
      }
      finish();
    }, 3000);

    function finish() {
      if (completed) {
        return;
      }
      completed = true;
      window.clearTimeout(timeoutHandle);
      redirectToLogin();
    }

    var requestOptions = {
      method: "POST",
      credentials: "same-origin",
      headers: {
        Accept: "application/json",
        RequestVerificationToken: csrfToken
      }
    };
    if (controller) {
      requestOptions.signal = controller.signal;
    }

    try {
      Promise.resolve(fetch(logoutUrl, requestOptions)).then(finish, finish);
    } catch (_error) {
      finish();
    }
  }

  function expireSession() {
    if (endingSession) {
      return;
    }
    endingSession = true;
    stopTimers();
    hideWarning();
    publishSessionEvent("expired");
    postLogoutAndRedirect();
  }

  function fetchStatus() {
    if (!statusUrl || endingSession) {
      return;
    }
    fetch(statusUrl, { credentials: "same-origin", headers: { Accept: "application/json" } })
      .then(function (response) {
        if (response.status === 401) {
          endSessionWithoutLogout("logout", true);
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
        /* Network errors are retried by the next status poll. */
      });
  }

  function keepAlive() {
    if (!keepAliveUrl || endingSession) {
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
          endSessionWithoutLogout("logout", true);
          return null;
        }
        if (response.status === 403 || response.status === 429) {
          return null;
        }
        return response.ok ? response.json() : null;
      })
      .then(function (data) {
        if (data && applyStatus(data)) {
          hideWarning();
          publishSessionEvent("keep-alive", data);
        }
      })
      .catch(function () {
        /* The server-side session remains authoritative after a failed keep-alive. */
      });
  }

  function showWarning(remaining) {
    if (!dialog) {
      return;
    }
    warningVisible = true;
    dialog.hidden = false;
    updateRemaining(remaining);
  }

  function updateRemaining(remainingSeconds) {
    if (remainingEl) {
      remainingEl.textContent = String(Math.max(0, Math.ceil(remainingSeconds)));
    }
  }

  function tick() {
    if (expiresAtMs === null || endingSession) {
      return;
    }
    var remainingMs = expiresAtMs - Date.now();
    var remainingSeconds = remainingMs / 1000;

    if (remainingSeconds <= 0) {
      expireSession();
      return;
    }

    if (remainingSeconds <= warningSeconds) {
      showWarning(remainingSeconds);
    } else if (warningVisible) {
      hideWarning();
    }
  }

  var activityThrottleMs = 15000;
  var lastActivitySent = 0;

  function onUserActivity() {
    if (warningVisible || endingSession) {
      /* A visible warning must be extended only by the explicit continue action. */
      return;
    }
    var now = Date.now();
    if (now - lastActivitySent < activityThrottleMs) {
      return;
    }
    lastActivitySent = now;
    keepAlive();
  }

  ["mousemove", "keydown", "click", "scroll", "touchstart"].forEach(function (eventName) {
    window.addEventListener(eventName, onUserActivity, { passive: true });
  });

  document.addEventListener("submit", function (event) {
    var form = event.target;
    if (!isLogoutForm(form)) {
      return;
    }
    if (endingSession) {
      event.preventDefault();
      return;
    }

    endingSession = true;
    stopTimers();
    hideWarning();
    publishSessionEvent("logout");
    /* The native antiforgery-protected form submission remains the logout authority. */
  }, true);

  if (continueBtn) {
    continueBtn.addEventListener("click", function () {
      keepAlive();
    });
  }

  initializeSessionSync();
  fetchStatus();
  tickHandle = window.setInterval(tick, 1000);
  statusPollHandle = window.setInterval(fetchStatus, 30000);
})();
