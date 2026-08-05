# Project Status

Son güncelleme: 2026-08-05

Foundation, tasarım/Razor, hero-motion ve commerce çekirdeği `integration` üzerinde birleşti. Son ortak hardening turunda auth saatleri, commerce sahiplik/idempotency sınırları, responsive navbar/fetch davranışı ve production notification güvenliği kapatıldı. Güncel doğrulama kapısı 33 unit + 60 integration = 93/93.

## Tamamlandı

- .NET 10 katmanlı solution, SQL Server EF Core/Identity modeli ve dört sıralı migration.
- Customer/Admin/SuperAdmin ayrık cookie scheme, route ve policy’leri; kayıt, doğrulama, login/logout, forgot/reset password.
- Yönetim lockout, rate limit, audit, AFK session/keep-alive/revoke ve uygulama `TimeProvider`'ıyla uyumlu cookie süreleri.
- Global exception handling, correlation ID, security headers, management `no-store` ve health skeleton.
- Design token sistemi, Mellos fontu ve Public/Account/Admin/SuperAdmin Razor sayfaları.
- Home hero frame-sequence, product-card motion ve erişilebilir page-transition davranışı.
- Desktop'ta üstte gerçekten şeffaf, scroll'da animasyonla koyulaşan navbar; mobilde 69 px disclosure menü, focus restore ve body scroll lock.
- Commerce backend: katalog, sepet, favori, kampanya/kupon, checkout, sipariş, ödeme, refund, iade, yorum, fatura, stok, shipment, notification ve raporlama.
- Guest-cart merge, callback/checkout idempotency, stok concurrency, IDOR kontrolleri, duplicate-return sınırı ve geçersiz kupondan güvenli kurtarma.
- Development SQLite ve deterministik/idempotent kahve kataloğu seed'i.
- Kalıcı Data Protection key-ring, Production SMTP/Identity SQL outbox, lease/concurrency kontrollü bounded retry ve terminal failure politikası.
- Production mock notification, payment ve shipping yapılandırmalarında startup'ın bilinçli fail-closed davranması.
- Restore ve build: 0 uyarı, 0 hata. Testler: 33/33 unit + 60/60 integration = 93/93.
- Dört migration doğru sırada; pending model change yok; SQL Server idempotent script 54.628 bayt.
- NuGet direct/transitive vulnerability taraması temiz; 26 first-party JavaScript dosyası syntax kontrolünden geçti.
- Localhost HTTP taraması ve gerçek Chrome masaüstü/mobil smoke tamamlandı; console/network hatası yok.

## Teslim ve doğrulama

- [x] Foundation ile Ajan 1, Ajan 2 ve Ajan 4 teslimleri `integration` üzerine alındı.
- [x] Commerce hardening `7cabb93` (`18d5d99` merge) ile alındı.
- [x] Production-readiness `8432baa` + `91b614a` (`49d0fc8` merge) ile alındı.
- [x] Auth/test-clock düzeltmesi `60cddc2` (`211127a` merge) ile alındı.
- [x] Frontend/UX hardening `98c17ef` (`ec20497` merge) ile alındı.
- [x] Public route'lar 200; müşteri/admin/superadmin korumalı route'ları doğru login URL'lerine 302.
- [x] Desktop navbar 0 px'te şeffaf, 1000 px'te `is-scrolled`; mobil menü aç/kapat/Escape/focus/overflow davranışları gerçek Chrome ile doğrulandı.

## Açık Production kapıları

- Gerçek `IPaymentGateway` ve `IShippingProvider` adapter'ları henüz yoktur. Production, mock adapter'larla başlamayı reddeder; bu guard gerçek adapter, secret yönetimi ve provider contract testleri tamamlanmadan kaldırılmamalıdır.
- Production SMTP değerleri ve kalıcı/shared Data Protection key-ring yolu deployment ortamında açıkça sağlanmalıdır. Development/Testing dış gönderim yapmayan deterministik mock'larda kalır.
- Bu ortamda SQL Server instance'ı çalıştırılmadı. Migration/model/idempotent-script kapıları SQL Server sağlayıcısıyla, runtime ve integration testleri SQLite ile doğrulandı.
- `Product` global query filter'ı ile required `StockMovement.Product` navigation'ı için kalan EF uyarısı veri veya FK silmez; ancak soft-delete ürünlere ait tarihsel hareketleri navigation join'li raporlarda gizleyebilir. Audit sorgularında `IgnoreQueryFilters()` veya ayrı optional/history ilişki migration'ı takip işidir.
