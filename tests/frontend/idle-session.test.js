const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const vm = require("node:vm");

const scriptPath = path.resolve(
  __dirname,
  "../../src/AETKAHVE.Web/wwwroot/js/admin/idle-session.js"
);
const scriptSource = fs.readFileSync(scriptPath, "utf8");

class BroadcastHub {
  constructor() {
    this.channels = new Map();
    this.messages = [];
  }

  createConstructor() {
    const hub = this;
    return class FakeBroadcastChannel {
      constructor(name) {
        this.name = name;
        this.listeners = [];
        const channels = hub.channels.get(name) || [];
        channels.push(this);
        hub.channels.set(name, channels);
      }

      addEventListener(type, listener) {
        if (type === "message") {
          this.listeners.push(listener);
        }
      }

      postMessage(message) {
        const cloned = JSON.parse(JSON.stringify(message));
        hub.messages.push({ name: this.name, message: cloned });
        for (const channel of hub.channels.get(this.name) || []) {
          if (channel === this) {
            continue;
          }
          for (const listener of channel.listeners) {
            listener({ data: JSON.parse(JSON.stringify(cloned)) });
          }
        }
      }
    };
  }
}

class StorageHub {
  constructor() {
    this.clients = [];
    this.values = new Map();
  }

  register(client) {
    this.clients.push(client);
  }

  setItem(source, key, value) {
    const oldValue = this.values.get(key) || null;
    this.values.set(key, value);
    for (const client of this.clients) {
      if (client === source) {
        continue;
      }
      for (const listener of client.listeners.get("storage") || []) {
        listener({ key, oldValue, newValue: value });
      }
    }
  }
}

function response(status, data) {
  return {
    status,
    ok: status >= 200 && status < 300,
    json: async () => data
  };
}

function createRuntime({
  hub = new BroadcastHub(),
  storageHub = new StorageHub(),
  broadcastEnabled = true,
  kind = "admin",
  now = Date.parse("2026-08-05T10:00:00Z"),
  initialStatus,
  keepAliveStatus,
  logoutResponse = Promise.resolve(response(200))
} = {}) {
  const clock = { now };
  const fetchCalls = [];
  const intervalCallbacks = new Map();
  const timeoutCallbacks = new Map();
  const windowListeners = new Map();
  const documentListeners = new Map();
  const continueListeners = [];
  const locationReplacements = [];
  let nextTimerId = 1;

  const status = initialStatus || {
    isAuthenticated: true,
    serverTimeUtc: new Date(now).toISOString(),
    expiresAtUtc: new Date(now + 60000).toISOString(),
    remainingSeconds: 60,
    warningSeconds: 60
  };
  const refreshedStatus = keepAliveStatus || {
    ...status,
    expiresAtUtc: new Date(now + 900000).toISOString(),
    remainingSeconds: 900
  };

  const bodyAttributes = {
    "data-idle-session": kind,
    "data-session-status-url": `/${kind}/session/status`,
    "data-session-keep-alive-url": `/${kind}/session/keep-alive`,
    "data-session-logout-url": `/${kind}/logout`,
    "data-idle-warning-seconds": "60"
  };

  const remainingElement = { textContent: "" };
  const continueButton = {
    addEventListener(type, listener) {
      if (type === "click") {
        continueListeners.push(listener);
      }
    }
  };
  const dialog = {
    hidden: true,
    querySelector(selector) {
      if (selector === "[data-idle-remaining]") {
        return remainingElement;
      }
      if (selector === "[data-idle-continue]") {
        return continueButton;
      }
      return null;
    }
  };
  const document = {
    body: {
      getAttribute(name) {
        return bodyAttributes[name] || null;
      }
    },
    querySelector(selector) {
      if (selector === "[data-idle-warning-dialog]") {
        return dialog;
      }
      if (selector === 'meta[name="csrf-token"]') {
        return { getAttribute: () => "csrf-test-token" };
      }
      return null;
    },
    addEventListener(type, listener) {
      const listeners = documentListeners.get(type) || [];
      listeners.push(listener);
      documentListeners.set(type, listeners);
    }
  };

  let storageClient;
  const window = {
    AbortController,
    BroadcastChannel: broadcastEnabled ? hub.createConstructor() : undefined,
    URL,
    location: {
      href: `https://example.test/${kind}`,
      replace(url) {
        locationReplacements.push(url);
        this.href = url;
      }
    },
    localStorage: {
      setItem(key, value) {
        storageHub.setItem(storageClient, key, value);
      }
    },
    addEventListener(type, listener) {
      const listeners = windowListeners.get(type) || [];
      listeners.push(listener);
      windowListeners.set(type, listeners);
    },
    setInterval(callback, milliseconds) {
      const id = nextTimerId++;
      intervalCallbacks.set(id, { callback, milliseconds });
      return id;
    },
    clearInterval(id) {
      intervalCallbacks.delete(id);
    },
    setTimeout(callback, milliseconds) {
      const id = nextTimerId++;
      timeoutCallbacks.set(id, { callback, milliseconds });
      return id;
    },
    clearTimeout(id) {
      timeoutCallbacks.delete(id);
    }
  };
  storageClient = { listeners: windowListeners };
  storageHub.register(storageClient);

  async function fetch(url, options = {}) {
    fetchCalls.push({ url, options });
    if (url === bodyAttributes["data-session-status-url"]) {
      return response(200, status);
    }
    if (url === bodyAttributes["data-session-keep-alive-url"]) {
      return response(200, refreshedStatus);
    }
    if (url === bodyAttributes["data-session-logout-url"]) {
      return logoutResponse;
    }
    throw new Error(`Unexpected fetch URL: ${url}`);
  }

  class RuntimeDate extends Date {
    static now() {
      return clock.now;
    }
  }

  const context = vm.createContext({
    Date: RuntimeDate,
    Math,
    Promise,
    console,
    document,
    fetch,
    isFinite,
    window
  });
  vm.runInContext(scriptSource, context, { filename: scriptPath });

  return {
    clock,
    dialog,
    fetchCalls,
    hub,
    locationReplacements,
    async flush() {
      await Promise.resolve();
      await Promise.resolve();
      await new Promise((resolve) => setImmediate(resolve));
    },
    runIntervals(milliseconds) {
      const callbacks = [...intervalCallbacks.values()]
        .filter((timer) => timer.milliseconds === milliseconds)
        .map((timer) => timer.callback);
      for (const callback of callbacks) {
        callback();
      }
    },
    runTimeouts(milliseconds) {
      const callbacks = [...timeoutCallbacks.entries()]
        .filter(([, timer]) => timer.milliseconds === milliseconds);
      for (const [id, timer] of callbacks) {
        timeoutCallbacks.delete(id);
        timer.callback();
      }
    },
    triggerDocument(type, target) {
      let defaultPrevented = false;
      for (const listener of documentListeners.get(type) || []) {
        listener({
          target,
          preventDefault() {
            defaultPrevented = true;
          }
        });
      }
      return defaultPrevented;
    },
    triggerWindow(type) {
      for (const listener of windowListeners.get(type) || []) {
        listener({ type });
      }
    }
  };
}

test("local expiry posts one antiforgery logout request before redirecting", async () => {
  const runtime = createRuntime({ logoutResponse: Promise.resolve(response(401)) });
  await runtime.flush();

  runtime.clock.now += 61000;
  runtime.runIntervals(1000);
  runtime.runIntervals(1000);
  await runtime.flush();

  const logoutCalls = runtime.fetchCalls.filter((call) => call.url === "/admin/logout");
  assert.equal(logoutCalls.length, 1);
  assert.equal(logoutCalls[0].options.method, "POST");
  assert.equal(logoutCalls[0].options.credentials, "same-origin");
  assert.equal(logoutCalls[0].options.headers.RequestVerificationToken, "csrf-test-token");
  assert.deepEqual(runtime.locationReplacements, ["/admin/login?reason=expired"]);
  assert.equal(runtime.hub.messages.at(-1).message.type, "expired");
});

test("logout network timeout cannot delay the login redirect indefinitely", async () => {
  const runtime = createRuntime({ logoutResponse: new Promise(() => {}) });
  await runtime.flush();

  runtime.clock.now += 61000;
  runtime.runIntervals(1000);
  assert.deepEqual(runtime.locationReplacements, []);

  runtime.runTimeouts(3000);
  assert.deepEqual(runtime.locationReplacements, ["/admin/login?reason=expired"]);
});

test("expiry and explicit logout synchronize only matching management portals", async () => {
  const hub = new BroadcastHub();
  const firstAdmin = createRuntime({ hub });
  const secondAdmin = createRuntime({ hub });
  const superAdmin = createRuntime({ hub, kind: "superadmin" });
  await Promise.all([firstAdmin.flush(), secondAdmin.flush(), superAdmin.flush()]);

  firstAdmin.clock.now += 61000;
  firstAdmin.runIntervals(1000);
  await firstAdmin.flush();

  assert.deepEqual(secondAdmin.locationReplacements, ["/admin/login?reason=expired"]);
  assert.deepEqual(superAdmin.locationReplacements, []);
  assert.equal(
    secondAdmin.fetchCalls.filter((call) => call.url === "/admin/logout").length,
    0,
    "a receiving tab must not duplicate the logout POST"
  );

  const isolatedHub = new BroadcastHub();
  const logoutSource = createRuntime({ hub: isolatedHub });
  const logoutPeer = createRuntime({ hub: isolatedHub });
  await Promise.all([logoutSource.flush(), logoutPeer.flush()]);
  const logoutForm = {
    tagName: "FORM",
    method: "post",
    action: "https://example.test/admin/logout",
    getAttribute(name) {
      return name === "method" ? "post" : "https://example.test/admin/logout";
    }
  };
  assert.equal(logoutSource.triggerDocument("submit", logoutForm), false);
  assert.equal(logoutSource.triggerDocument("submit", logoutForm), true);

  assert.deepEqual(logoutPeer.locationReplacements, ["/admin/login?reason=session-ended"]);
  assert.equal(isolatedHub.messages.at(-1).message.type, "logout");
});

test("successful keep-alive extends the peer tab deadline", async () => {
  const hub = new BroadcastHub();
  const first = createRuntime({ hub });
  const second = createRuntime({ hub });
  await Promise.all([first.flush(), second.flush()]);

  first.triggerWindow("keydown");
  await first.flush();
  const keepAliveMessage = hub.messages.find((entry) => entry.message.type === "keep-alive");
  assert.ok(keepAliveMessage);

  second.clock.now += 61000;
  second.runIntervals(1000);
  assert.deepEqual(second.locationReplacements, []);
  assert.equal(second.fetchCalls.filter((call) => call.url === "/admin/logout").length, 0);
});

test("storage events preserve cross-tab expiry when BroadcastChannel is unavailable", async () => {
  const storageHub = new StorageHub();
  const first = createRuntime({ broadcastEnabled: false, storageHub });
  const second = createRuntime({ broadcastEnabled: false, storageHub });
  await Promise.all([first.flush(), second.flush()]);

  first.clock.now += 61000;
  first.runIntervals(1000);
  await first.flush();

  assert.deepEqual(second.locationReplacements, ["/admin/login?reason=expired"]);
  assert.equal(second.fetchCalls.filter((call) => call.url === "/admin/logout").length, 0);
});
