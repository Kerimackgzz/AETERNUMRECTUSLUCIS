// AETERNUM RECTUS LUCIS — Commerce fetch helper
// Frozen contract: RequestVerificationToken header, CommerceMutationResponse JSON shape.

const LOGIN_PATHS = new Set(["/account/login", "/admin/login", "/superadmin/login"]);

function csrfToken() {
  const meta = document.querySelector('meta[name="csrf-token"]');
  return meta ? meta.getAttribute("content") : "";
}

function normalizedPath(url) {
  try {
    const parsed = new URL(url, window.location.href);
    return parsed.origin === window.location.origin ? parsed.pathname.toLowerCase().replace(/\/$/, "") || "/" : null;
  } catch {
    return null;
  }
}

function loginPathForRequest(requestUrl) {
  const path = normalizedPath(requestUrl) || "";
  if (path === "/superadmin" || path.startsWith("/superadmin/")) return "/superadmin/login";
  if (path === "/admin" || path.startsWith("/admin/")) return "/admin/login";
  return "/account/login";
}

function redirectedLoginPath(response) {
  if (!response.redirected || !response.url) return null;
  const path = normalizedPath(response.url);
  return path && LOGIN_PATHS.has(path) ? path : null;
}

function currentReturnUrl() {
  return `${window.location.pathname}${window.location.search}${window.location.hash}`;
}

function redirectToLogin(loginPath) {
  const destination = `${loginPath}?returnUrl=${encodeURIComponent(currentReturnUrl())}`;
  window.location.assign(destination);
  throw new Error("unauthenticated");
}

function enforceAuthentication(response, requestUrl) {
  const followedLogin = redirectedLoginPath(response);
  if (followedLogin) redirectToLogin(followedLogin);
  if (response.status === 401) redirectToLogin(loginPathForRequest(requestUrl));
}

async function readJson(response) {
  const contentType = response.headers.get("content-type") || "";
  if (!contentType.toLowerCase().includes("json")) return null;
  try {
    return await response.json();
  } catch {
    return null;
  }
}

function jsonResult(response, payload) {
  return {
    // Commerce endpoint'leri JSON sözleşmesi taşır. Login HTML'i veya başka
    // beklenmeyen bir 2xx gövde hiçbir zaman başarılı mutation sayılmaz.
    ok: response.ok && payload !== null,
    status: response.status,
    data: payload,
  };
}

export function commerceErrorMessage(payload, fallback) {
  if (typeof payload?.message === "string" && payload.message.trim()) return payload.message.trim();

  if (payload?.errors && typeof payload.errors === "object") {
    const messages = Object.values(payload.errors)
      .flatMap((value) => Array.isArray(value) ? value : [value])
      .filter((value) => typeof value === "string" && value.trim())
      .map((value) => value.trim());
    if (messages.length) return [...new Set(messages)].join(" ");
  }

  if (typeof payload?.detail === "string" && payload.detail.trim()) return payload.detail.trim();
  if (typeof payload?.title === "string" && payload.title.trim() && payload.title !== "One or more validation errors occurred.") {
    return payload.title.trim();
  }
  return fallback;
}

export async function postCommerce(url, body) {
  const response = await fetch(url, {
    method: "POST",
    credentials: "same-origin",
    headers: {
      "Content-Type": "application/json",
      Accept: "application/json",
      RequestVerificationToken: csrfToken(),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  enforceAuthentication(response, url);
  return jsonResult(response, await readJson(response));
}

// [FromForm] parametreli admin action'lar için (ör. Orders/Returns/Reviews Status) — JSON gövde kabul etmezler.
export async function postForm(url, fields) {
  const body = new URLSearchParams();
  Object.entries(fields || {}).forEach(([key, value]) => {
    if (value !== undefined && value !== null) body.append(key, value);
  });

  const response = await fetch(url, {
    method: "POST",
    credentials: "same-origin",
    headers: {
      "Content-Type": "application/x-www-form-urlencoded",
      Accept: "application/json",
      RequestVerificationToken: csrfToken(),
    },
    body: body.toString(),
  });

  enforceAuthentication(response, url);
  return jsonResult(response, await readJson(response));
}

export async function getJson(url) {
  const response = await fetch(url, {
    credentials: "same-origin",
    headers: { Accept: "application/json" },
  });
  enforceAuthentication(response, url);
  return jsonResult(response, await readJson(response));
}

export {
  currentReturnUrl,
  loginPathForRequest,
  redirectedLoginPath,
};
