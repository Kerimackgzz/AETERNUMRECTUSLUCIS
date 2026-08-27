const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");

function read(relativePath) {
  return fs.readFileSync(path.resolve(__dirname, "../..", relativePath), "utf8");
}

const navbar = read("src/AETKAHVE.Web/Views/Shared/_Navbar.cshtml");
const cartView = read("src/AETKAHVE.Web/Views/Cart/Index.cshtml");
const productDetailView = read("src/AETKAHVE.Web/Views/Products/Detail.cshtml");
const cartScript = read("src/AETKAHVE.Web/wwwroot/js/pages/cart.js");
const productDetailScript = read("src/AETKAHVE.Web/wwwroot/js/pages/product-detail.js");
const ordersView = read("src/AETKAHVE.Web/Views/Orders/Index.cshtml");
const orderDetailView = read("src/AETKAHVE.Web/Views/Orders/Detail.cshtml");
const returnsView = read("src/AETKAHVE.Web/Views/Returns/Index.cshtml");
const orderActionsScript = read("src/AETKAHVE.Web/wwwroot/js/pages/order-actions.js");
const returnsScript = read("src/AETKAHVE.Web/wwwroot/js/pages/returns.js");

test("storefront navbar identifies only the Customer portal and always exposes the cart", () => {
  assert.match(navbar, /User\.IsInRole\(RoleNames\.Customer\)/);
  assert.match(navbar, /SecurityClaimTypes\.Portal/);
  assert.match(navbar, /AuthenticationPortal\.Customer/);
  assert.match(navbar, /asp-controller="Cart"[^>]*>Sepetim</);
  assert.match(navbar, />Hesabım</);
  assert.match(navbar, />Giriş Yap \/ Hesap Oluştur</);
});

test("cart and product detail quantity limits use stock and the configured maximum", () => {
  assert.match(
    cartView,
    /Math\.Min\(line\.AvailableQuantity, CommerceConfiguration\.Value\.MaximumCartItemQuantity\)/
  );
  assert.match(productDetailView, /data-maximum-cart-quantity=/);
  assert.match(
    productDetailView,
    /Math\.Min\(initialAvailableQuantity, CommerceConfiguration\.Value\.MaximumCartItemQuantity\)/
  );
  assert.match(productDetailScript, /Math\.min\(available, configuredMaximum\)/);
  assert.match(productDetailScript, /Number\(input\.max/);
  assert.doesNotMatch(productDetailScript, /Math\.min\(99,/);
});

test("cart serializes each line mutation and locks every line control", () => {
  assert.match(cartScript, /const inFlightLines = new WeakSet\(\)/);
  assert.match(cartScript, /if \(inFlightLines\.has\(line\)\) return false/);
  assert.match(cartScript, /querySelectorAll\("button, input, select, textarea"\)/);
  assert.match(cartScript, /line\.setAttribute\("aria-busy", "true"\)/);
  assert.match(cartScript, /mutateLine\(line, remove\.getAttribute/);
});

test("delivered customer orders expose the return form and preserve its success message", () => {
  assert.match(ordersView, /order\.Status == OrderStatus\.Delivered/);
  assert.match(ordersView, /asp-fragment="return-request"/);
  assert.match(ordersView, /data-order-return-link>İade Et</);
  assert.match(orderDetailView, /id="return-request"[^>]*data-order-actions/);
  assert.match(orderDetailView, /data-return-create/);
  assert.match(orderActionsScript, /sessionStorage\.setItem\(RETURN_TOAST_KEY/);
  assert.match(returnsView, /js\/pages\/returns\.js/);
  assert.match(returnsScript, /sessionStorage\.removeItem\(RETURN_TOAST_KEY\)/);
  assert.match(returnsScript, /showToast\(notification\.message\.trim\(\)/);
});
