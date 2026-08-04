// AETERNUM RECTUS LUCIS — Commerce fetch helper
// Frozen contract: RequestVerificationToken header, CommerceMutationResponse JSON shape.

function csrfToken() {
  const meta = document.querySelector('meta[name="csrf-token"]');
  return meta ? meta.getAttribute("content") : "";
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

  let payload = null;
  try {
    payload = await response.json();
  } catch {
    payload = null;
  }

  if (response.status === 401) {
    window.location.href = "/account/login?returnUrl=" + encodeURIComponent(window.location.pathname);
    throw new Error("unauthenticated");
  }

  return {
    ok: response.ok,
    status: response.status,
    data: payload,
  };
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

  let payload = null;
  try {
    payload = await response.json();
  } catch {
    payload = null;
  }

  if (response.status === 401) {
    window.location.href = "/admin/login?returnUrl=" + encodeURIComponent(window.location.pathname);
    throw new Error("unauthenticated");
  }

  return { ok: response.ok, status: response.status, data: payload };
}

export async function getJson(url) {
  const response = await fetch(url, { credentials: "same-origin", headers: { Accept: "application/json" } });
  let payload = null;
  try {
    payload = await response.json();
  } catch {
    payload = null;
  }
  return { ok: response.ok, status: response.status, data: payload };
}
