const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");

function read(relativePath) {
  return fs.readFileSync(path.resolve(__dirname, "../..", relativePath), "utf8");
}

const confirmationView = read("src/AETKAHVE.Web/Views/Account/ConfirmEmail.cshtml");
const registerView = read("src/AETKAHVE.Web/Views/Account/Register.cshtml");
const resendView = read("src/AETKAHVE.Web/Views/Account/ResendConfirmation.cshtml");

test("confirmation view keeps account creation behind an antiforgery POST", () => {
  assert.match(confirmationView, /@model ConfirmEmailViewModel/);
  assert.match(confirmationView, /data-confirm-email-state=/);
  assert.match(confirmationView, /data-confirm-email-form/);
  assert.match(confirmationView, /asp-action="ConfirmEmail(?:Post)?"/);
  assert.match(confirmationView, /asp-antiforgery="true"/);
  assert.match(confirmationView, /method="post"/);
  assert.match(confirmationView, /asp-for="RegistrationId" type="hidden"/);
  assert.match(confirmationView, /asp-for="Token" type="hidden"/);
  assert.match(confirmationView, />Üyeliği tamamla</);
});

test("invalid confirmation state offers recovery without a completion form", () => {
  const invalidBranch = confirmationView.split("else")[1];
  assert.match(invalidBranch, /Bağlantı geçersiz veya süresi dolmuş/);
  assert.match(invalidBranch, /asp-action="ResendConfirmation"/);
  assert.doesNotMatch(invalidBranch, /data-confirm-email-form/);
});

test("registration copy explains the explicit completion step", () => {
  assert.match(registerView, /Doğrulama bağlantısı gönder/);
  assert.match(registerView, /hesabınızı otomatik olarak oluşturmaz/);
  assert.match(registerView, /Üyeliği tamamla/);
  assert.match(resendView, /üyeliği otomatik olarak etkinleştirmez/);
  assert.match(resendView, /Yeni doğrulama bağlantısı gönder/);
});
