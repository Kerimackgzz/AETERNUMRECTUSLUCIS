(function () {
  "use strict";

  document.querySelectorAll("[data-password-field]").forEach(function (field) {
    var input = field.querySelector("[data-password-input]");
    var toggle = field.querySelector("[data-password-toggle]");
    var openEye = field.querySelector("[data-password-eye-open]");
    var closedEye = field.querySelector("[data-password-eye-closed]");

    if (!input || !toggle) {
      return;
    }

    toggle.addEventListener("click", function () {
      var isVisible = input.type === "text";
      input.type = isVisible ? "password" : "text";
      toggle.setAttribute("aria-pressed", String(!isVisible));
      toggle.setAttribute("aria-label", isVisible ? "Şifreyi göster" : "Şifreyi gizle");
      toggle.title = isVisible ? "Şifreyi göster" : "Şifreyi gizle";

      if (openEye) {
        openEye.hidden = !isVisible;
      }

      if (closedEye) {
        closedEye.hidden = isVisible;
      }
    });
  });
})();
