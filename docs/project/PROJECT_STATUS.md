# Project Status

Son güncelleme: 2026-08-05

Foundation, tasarım/Razor, hero-motion ve commerce çekirdeği `integration` üzerinde birleşti. Son ortak hardening turunda production security, authenticated payment webhook ve cross-tab AFK logout akışları da kapatıldı. Güncel doğrulama kapısı 5 frontend + 54 unit + 77 integration testidir.

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
- Absolute ve kalıcı Data Protection key-ring, sertifikayla key-at-rest şifreleme, trusted proxy allow-list ve Production SMTP/Identity SQL outbox.
- Production'da Mock payment reddi, güvenli `Disabled` ödeme varsayılanı, gerçek shipping adapter eksikliğinde startup fail-closed davranışı.
- HMAC-SHA256, timestamp toleransı, constant-time imza kontrolü ve replay rezervasyonlu payment webhook kapısı.
- AFK expiry sırasında tek antiforgery logout POST'u, üç saniyelik fallback ve portal-scope cross-tab senkronizasyonu.
- Restore ve Release build: 0 uyarı, 0 hata. Testler: frontend 5/5, unit 54/54, integration 77/77.
- Dört migration doğru sırada ve pending model change yok. Gerçek `.\SQLEXPRESS` smoke veritabanına uygulandı; 54.628 baytlık idempotent script art arda iki kez hatasız çalıştı ve `DBCC CHECKDB` temiz geçti. Smoke veritabanı doğrulama sonunda kaldırıldı.
- NuGet direct/transitive vulnerability taraması temiz; 26 first-party JavaScript dosyası syntax kontrolünden geçti.
- Localhost HTTP taraması ve gerçek Chrome masaüstü/mobil smoke tamamlandı; console/network hatası yok.

## Teslim ve doğrulama

- [x] Foundation ile Ajan 1, Ajan 2 ve Ajan 4 teslimleri `integration` üzerine alındı.
- [x] Commerce hardening `7cabb93` (`18d5d99` merge) ile alındı.
- [x] Production-readiness `8432baa` + `91b614a` (`49d0fc8` merge) ile alındı.
- [x] Auth/test-clock düzeltmesi `60cddc2` (`211127a` merge) ile alındı.
- [x] Frontend/UX hardening `98c17ef` (`ec20497` merge) ile alındı.
- [x] Production proxy/Data Protection/security hardening `b86da0c` ile alındı.
- [x] Payment provider/webhook hardening `84610f2` ile alındı.
- [x] AFK expiry/cross-tab frontend hardening `243b9f6` ile alındı.
- [x] Public route'lar 200; müşteri/admin/superadmin korumalı route'ları doğru login URL'lerine 302.
- [x] Desktop navbar 0 px'te şeffaf, 1000 px'te `is-scrolled`; mobil menü aç/kapat/Escape/focus/overflow davranışları gerçek Chrome ile doğrulandı.

## Açık Production kapıları

- Gerçek `IPaymentGateway` artık var: Stripe Checkout Session tabanlı gerçek ödeme adaptörü (`agent/claude-stripe-payment`) — gerçek API çağrıları, webhook imza doğrulaması dahil. Durable/distributed webhook replay store ve gerçek `IShippingProvider` adaptörü hâlâ yok (kullanıcı tercihiyle kasıtlı — prototip teslimi için gerçek kargo entegrasyonu istenmedi); shipping startup guard'ı bu yüzden kaldırılmamalıdır.
- Production SMTP değerleri, absolute shared Data Protection key-ring yolu ve private key erişimli sertifika deployment ortamında açıkça sağlanmalıdır. Development/Testing dış gönderim yapmayan deterministik mock'larda kalır. Bu kalıcı operasyonel gereksinim; hangi anahtarın hangi ortam değişkeninden/secret store'dan geleceği ve key-ring'in neden replica'lar arasında paylaşılması gerektiği [`docs/deployment/PRODUCTION_SETUP.md`](../deployment/PRODUCTION_SETUP.md) runbook'unda adım adım belgelenmiştir; eksik config ile fail-closed davranış `Production_host_fails_start_when_smtp_configuration_is_missing` ve `Missing_production_data_protection_configuration_fails_fast` testleriyle kanıtlanmıştır.
- Bu ortamda gerçek bir `.\SQLEXPRESS` instance'ına karşı migration/idempotent-script doğrulaması yapıldı (yukarıya bkz.); production benzeri bir SQL Server üzerinde ayrıca doğrulanmalıdır.
