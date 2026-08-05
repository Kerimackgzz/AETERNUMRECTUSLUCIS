const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const assert = require("node:assert/strict");
const vm = require("node:vm");

test("password toggle updates visibility, icon and accessible label", () => {
  const source = fs.readFileSync(
    path.join(__dirname, "../../src/AETKAHVE.Web/wwwroot/js/auth/password-visibility.js"),
    "utf8"
  );
  const input = { type: "password" };
  const openEye = { hidden: false };
  const closedEye = { hidden: true };
  const attributes = {};
  let click;
  const toggle = {
    title: "Şifreyi göster",
    addEventListener: (event, handler) => { if (event === "click") click = handler; },
    setAttribute: (name, value) => { attributes[name] = value; }
  };
  const elements = {
    "[data-password-input]": input,
    "[data-password-toggle]": toggle,
    "[data-password-eye-open]": openEye,
    "[data-password-eye-closed]": closedEye
  };
  const field = { querySelector: (selector) => elements[selector] };
  const context = { document: { querySelectorAll: () => [field] } };

  vm.runInNewContext(source, context);
  click();

  assert.equal(input.type, "text");
  assert.equal(attributes["aria-pressed"], "true");
  assert.equal(attributes["aria-label"], "Şifreyi gizle");
  assert.equal(openEye.hidden, true);
  assert.equal(closedEye.hidden, false);

  click();
  assert.equal(input.type, "password");
  assert.equal(attributes["aria-pressed"], "false");
  assert.equal(attributes["aria-label"], "Şifreyi göster");
  assert.equal(openEye.hidden, false);
  assert.equal(closedEye.hidden, true);
});
