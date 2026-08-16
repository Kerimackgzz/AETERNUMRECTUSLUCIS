const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const vm = require("node:vm");

test("server flash message dismisses after four seconds and removes its empty region", () => {
  const source = fs.readFileSync(
    path.join(__dirname, "../../src/AETKAHVE.Web/wwwroot/js/components/flash-messages.js"),
    "utf8"
  );
  const timers = [];
  const removedClasses = [];
  let messageRemoved = false;
  let regionRemoved = false;
  const region = {
    querySelector: () => null,
    remove: () => { regionRemoved = true; }
  };
  const message = {
    classList: { remove: (value) => removedClasses.push(value) },
    closest: () => region,
    remove: () => { messageRemoved = true; }
  };
  const context = {
    document: { querySelectorAll: () => [message] },
    window: { setTimeout: (callback, delay) => timers.push({ callback, delay }) }
  };

  vm.runInNewContext(source, context);
  assert.equal(timers[0].delay, 4000);

  timers.shift().callback();
  assert.deepEqual(removedClasses, ["is-visible"]);
  assert.equal(timers[0].delay, 300);

  timers.shift().callback();
  assert.equal(messageRemoved, true);
  assert.equal(regionRemoved, true);
});
