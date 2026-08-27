const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");

const root = path.resolve(__dirname, "../..");
const script = fs.readFileSync(
  path.join(root, "src/AETKAHVE.Web/wwwroot/js/admin/admin-navigation.js"),
  "utf8"
);
const navigation = fs.readFileSync(
  path.join(root, "src/AETKAHVE.Web/Views/Shared/_ManagementNavigation.cshtml"),
  "utf8"
);

test("management navigation covers every commerce module and exposes current page state", () => {
  for (const route of [
    "/admin/products",
    "/admin/catalog",
    "/admin/orders",
    "/admin/shipments",
    "/admin/invoices",
    "/admin/returns",
    "/admin/campaigns",
    "/admin/coupons",
    "/admin/reviews",
    "/admin/messages",
    "/admin/reports"
  ]) {
    assert.match(navigation, new RegExp(`href=\\"${route.replaceAll("/", "\\/")}\\"`));
  }
  assert.match(navigation, /aria-current=/);
  assert.match(navigation, /User\.IsInRole\(RoleNames\.SuperAdmin\)/);
  assert.match(navigation, /href="\/superadmin\/admins"/);
  assert.match(navigation, /Model\.ShowCommerce/);
});

test("mobile management drawer restores focus and isolates background content", () => {
  assert.match(script, /aria-expanded/);
  assert.match(script, /event\.key === "Escape"/);
  assert.match(script, /setAttribute\("inert"/);
  assert.match(script, /toggle\.focus\(\)/);
  assert.match(script, /document\.body\.classList\.toggle\("admin-nav-open"/);
  assert.match(script, /event\.key !== "Tab"/);
});
