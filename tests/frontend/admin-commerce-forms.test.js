const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");
const vm = require("node:vm");

const root = path.resolve(__dirname, "../..");

function loadExports(relativePath, names) {
  const source = fs.readFileSync(path.join(root, relativePath), "utf8").replace(/^export /gm, "");
  const context = { module: { exports: {} }, console };
  vm.runInNewContext(`${source}\nmodule.exports = { ${names.join(", ")} };`, context);
  return context.module.exports;
}

test("admin promotion helpers produce JSON numbers, nullable values and valid GUID lists", () => {
  const helpers = loadExports(
    "src/AETKAHVE.Web/wwwroot/js/admin/admin-form-utils.js",
    ["nullableNumber", "discountTypeJsonValue", "guidList", "validatePromotion"]
  );

  assert.equal(helpers.nullableNumber(""), null);
  assert.equal(helpers.nullableNumber("12.50"), 12.5);
  assert.equal(helpers.nullableNumber("8", { integer: true, minimum: 0, exclusiveMinimum: true }), 8);
  assert.throws(() => helpers.nullableNumber("3.5", { integer: true }), /tam sayı/);
  assert.throws(() => helpers.nullableNumber("0", { integer: true, minimum: 0, exclusiveMinimum: true }), /büyük/);
  assert.equal(helpers.discountTypeJsonValue("Percentage"), 0);
  assert.equal(helpers.discountTypeJsonValue("FixedAmount"), 1);
  assert.equal(helpers.discountTypeJsonValue("FreeShipping"), 2);

  const id = "8B711A7C-B8F2-4EBB-BB55-CFF9A909A2B6";
  assert.deepEqual(Array.from(helpers.guidList(` ${id},${id} `, "Ürünler")), [id.toLowerCase()]);
  assert.equal(helpers.guidList("", "Ürünler"), null);
  assert.throws(() => helpers.guidList("not-a-guid", "Ürünler"), /geçersiz/);

  assert.doesNotThrow(() => helpers.validatePromotion("Percentage", 100, "2026-01-01", "2026-01-02"));
  assert.doesNotThrow(() => helpers.validatePromotion("FreeShipping", 0, "2026-01-01", "2026-01-02"));
  assert.throws(() => helpers.validatePromotion("Percentage", 101, "2026-01-01", "2026-01-02"), /100/);
  assert.throws(() => helpers.validatePromotion("FreeShipping", 1, "2026-01-01", "2026-01-02"), /0/);
  assert.throws(() => helpers.validatePromotion("FixedAmount", 10, "2026-01-02", "2026-01-01"), /Bitiş/);
});

test("product draft is versioned, restorable and explicitly clearable", () => {
  const draft = loadExports(
    "src/AETKAHVE.Web/wwwroot/js/admin/admin-product-draft.js",
    ["PRODUCT_DRAFT_KEY", "loadProductDraft", "saveProductDraft", "clearProductDraft", "captureProductForm", "restoreProductForm"]
  );
  const values = new Map();
  const storage = {
    getItem: (key) => values.get(key) ?? null,
    setItem: (key, value) => values.set(key, value),
    removeItem: (key) => values.delete(key)
  };
  const fields = new Map([
    ["name", { type: "text", value: "Gece Ritüeli" }],
    ["taxRate", { type: "number", value: "10" }],
    ["isActive", { type: "checkbox", checked: true }]
  ]);
  const form = { elements: { namedItem: (name) => fields.get(name) ?? null } };
  const captured = draft.captureProductForm(form);

  draft.saveProductDraft(storage, captured);
  const loaded = draft.loadProductDraft(storage);
  assert.equal(loaded.name, "Gece Ritüeli");
  assert.equal(loaded.taxRate, "10");
  assert.equal(loaded.isActive, true);

  fields.get("name").value = "";
  fields.get("isActive").checked = false;
  assert.equal(draft.restoreProductForm(form, loaded), true);
  assert.equal(fields.get("name").value, "Gece Ritüeli");
  assert.equal(fields.get("isActive").checked, true);

  draft.clearProductDraft(storage);
  assert.equal(draft.loadProductDraft(storage), null);
  storage.setItem(draft.PRODUCT_DRAFT_KEY, "{broken");
  assert.equal(draft.loadProductDraft(storage), null);
});

test("admin form sources preserve drafts, surface API errors and remove empty warning icons", () => {
  const products = fs.readFileSync(path.join(root, "src/AETKAHVE.Web/wwwroot/js/admin/products.js"), "utf8");
  const campaigns = fs.readFileSync(path.join(root, "src/AETKAHVE.Web/wwwroot/js/admin/campaigns.js"), "utf8");
  const coupons = fs.readFileSync(path.join(root, "src/AETKAHVE.Web/wwwroot/js/admin/coupons.js"), "utf8");
  const productView = fs.readFileSync(path.join(root, "src/AETKAHVE.Web/Areas/Admin/Views/Products/Index.cshtml"), "utf8");
  const accountCss = fs.readFileSync(path.join(root, "src/AETKAHVE.Web/wwwroot/css/pages/account.css"), "utf8");

  assert.match(products, /window\.sessionStorage/);
  assert.match(products, /clearProductDraft\(window\.sessionStorage\)/);
  assert.match(productView, /data-product-draft-clear/);
  assert.match(productView, /target="_blank"/);
  assert.match(campaigns, /discountTypeJsonValue/);
  assert.match(coupons, /discountTypeJsonValue/);
  assert.match(campaigns, /commerceErrorMessage/);
  assert.match(coupons, /commerceErrorMessage/);
  assert.doesNotMatch(campaigns, /\.value \|\| null/);
  assert.doesNotMatch(coupons, /\.value \|\| null/);
  assert.match(accountCss, /field-validation-error:empty/);
  assert.doesNotMatch(accountCss, /content:\s*["']⚠/);
});
