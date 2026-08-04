# Contract Request — page-transition.js/css için layout bağlantısı

Durum: Uygulandı (`c4a669d`) — CSS/JS `_PublicLayout.cshtml` içine bağlandı ve integration contract testiyle kilitlendi.

**Ajan:** Ajan 2 — Claude Home Hero ve Motion
**Tarih:** 2026-08-04
**Konu:** `_PublicLayout.cshtml`'e page-transition asset'lerinin bağlanması

## İstek

`Views/Shared/_PageTransitionOverlay.cshtml` hook'u zaten `_PublicLayout.cshtml` içinde render ediliyor, ama onu kullanan runtime (`page-transition.js`) hiç yüklenmiyordu. Bu oturumda ekledim:

- `wwwroot/css/core/page-transition.css`
- `wwwroot/js/core/page-transition.js`

`navbar.css`/`navbar-motion.js` için daha önce yapıldığı gibi (`7c0568d`), `_PublicLayout.cshtml`'e şu iki satırın eklenmesini rica ediyorum (Layoutu değiştirme kuralı gereği bunu ben yapamıyorum):

```html
<link rel="stylesheet" href="~/css/core/page-transition.css" asp-append-version="true" />
```
(`Styles` bloğundan önce, diğer stylesheet linkleriyle birlikte)

```html
<script type="module" src="~/js/core/page-transition.js" asp-append-version="true"></script>
```
(`navbar-motion.js` script tag'inin yanına, `PageScripts` section'ından önce)

## Neden

`page-transition.js` yalnızca aynı-origin `<a>` tıklamalarını yakalar (form submit, yeni sekme, modifier-key, download, hash-only ve farklı-origin linklere hiç dokunmaz); back/forward akışına da kasıtlı olarak müdahale etmez. Layout'a bağlanmadan bu dosya hiçbir sayfada çalışmaz.
