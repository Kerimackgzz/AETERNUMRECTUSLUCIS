# Frontend / Backend Contract — Frozen Foundation

## Layout

Public layout `_Navbar` ve `_PageTransitionOverlay` partiallarını render eder. Bütün layoutlarda isteğe bağlı `Styles` ve `PageScripts` section’ları bulunur. Antiforgery request tokenı:

```html
<meta name="csrf-token" content="...">
```

Fetch/AJAX unsafe istekleri tokenı `RequestVerificationToken` header’ı veya standart form alanıyla göndermelidir.

## Hero

Hero root hook’u `[data-home-hero]` ve şu alanlardır:

- `data-frame-manifest-url`
- `data-poster-url`
- `data-reduced-motion` (`true`/`false`)

Featured container: `[data-featured-products]`; kart listesi: `[data-product-card-list]`, `data-product-count`.

## Product Card Motion

Ajan 1 markup üretirken aşağıdaki sabit hookları sağlar; Ajan 2 yalnız motion enhancement uygular:

- root: `[data-product-card]`, `data-product-id`
- image: `[data-product-card-image]`
- surface/highlight: `[data-product-card-surface]`
- price: `[data-product-card-price]`
- add-to-cart: `[data-add-to-cart-url]`
- favorite: `[data-toggle-favorite-url]`
- detail: `[data-detail-url]`

Endpoint URL’leri JavaScript içinde hardcode edilmez.

## Management AFK

Admin/SuperAdmin layout `<body>` hookları:

- `data-idle-session="admin|superadmin"`
- `data-session-status-url`
- `data-session-keep-alive-url`
- `data-session-logout-url`
- `data-idle-timeout-seconds`
- `data-idle-warning-seconds`

Modal hookları: `[data-idle-warning-dialog]`, `[data-idle-remaining]`, `[data-idle-continue]`.

Status ve keep-alive JSON sözleşmesi:

```json
{
  "isAuthenticated": true,
  "serverTimeUtc": "2026-08-04T12:00:00+00:00",
  "expiresAtUtc": "2026-08-04T12:15:00+00:00",
  "remainingSeconds": 900,
  "warningSeconds": 60
}
```

Status GET aktiviteyi yenilemez. Keep-alive POST antiforgery ister. `401` yeniden login, `403` yetki reddi, `429` rate limit olarak ele alınır. Sekmeler arası senkronizasyon frontend runtime sahibindedir; sunucu session kaydı nihai gerçektir.
