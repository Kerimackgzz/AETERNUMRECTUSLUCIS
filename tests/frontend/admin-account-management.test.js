const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const test = require("node:test");

const root = path.resolve(__dirname, "../..");
const read = (relativePath) => fs.readFileSync(path.join(root, relativePath), "utf8");

test("admin management views keep role selection out and use protected server forms", () => {
  const create = read("src/AETKAHVE.Web/Areas/SuperAdmin/Views/AdminAccounts/Create.cshtml");
  const index = read("src/AETKAHVE.Web/Areas/SuperAdmin/Views/AdminAccounts/Index.cshtml");
  const deletion = read("src/AETKAHVE.Web/Areas/SuperAdmin/Views/AdminAccounts/Delete.cshtml");

  assert.doesNotMatch(create, /<select[^>]*name=["']Role/i);
  assert.doesNotMatch(create, /asp-for=["']Role/i);
  assert.match(create, /Rol: Admin/);
  assert.match(index, /resend-invitation/);
  assert.match(index, /password-reset/);
  assert.match(index, /@Html\.AntiForgeryToken\(\)/);
  assert.match(deletion, /geri alınamaz/i);
  assert.match(deletion, /method="post"/);
  assert.match(deletion, /@Html\.AntiForgeryToken\(\)/);
});

test("anonymous admin credential pages keep tokens hidden and avoid authenticated shell", () => {
  for (const file of ["Invitation.cshtml", "PasswordReset.cshtml", "EmailChange.cshtml"]) {
    const source = read(`src/AETKAHVE.Web/Areas/Admin/Views/AccountAccess/${file}`);
    assert.match(source, /_ManagementAuthLayout/);
    assert.match(source, /asp-for="Token" type="hidden"/);
    assert.match(source, /@Html\.AntiForgeryToken\(\)/);
  }
});
