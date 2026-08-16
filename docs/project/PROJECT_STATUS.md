# Project Status

Son güncelleme: 2026-08-16

Foundation, tasarım/Razor, hero-motion ve commerce çekirdeği `integration` üzerinde birleşti. Sonraki turlarda atomik Customer üyeliği, gerçek Stripe ödeme adaptörü, gerçek SQL Server doğrulaması, production SMTP/Data Protection deployment runbook'u, **Customer Account Center**, **Admin/SuperAdmin commerce dashboard'u ve ortak modül navigasyonu**, **çift-rollü (Admin+SuperAdmin) yönetim hesabı ve paylaşılan güvenlik merkezi**, **ürünler sayfası filtre sidebar'ının premium yeniden tasarımı** ve **admin ürün formu taslak/stok iyileştirmesi** de kapatıldı. Güncel doğrulama kapısı 17 frontend + 82 unit + 121 integration testidir.

## Tamamlandı

- .NET 10 katmanlı solution, SQL Server EF Core/Identity modeli ve altı sıralı migration.
- Customer/Admin/SuperAdmin ayrık cookie scheme, route ve policy’leri; kayıt, doğrulama, login/logout, forgot/reset password.
- Customer kaydı e-posta onayına kadar ayrı pending tabloda tutulur; gerçek hesap yalnız antiforgery POST ile atomik oluşturulur. Reset GET salt okunur, parola yalnız geçerli POST'ta değişir.
- Yönetim lockout, rate limit, audit, AFK session/keep-alive/revoke ve uygulama `TimeProvider`'ıyla uyumlu cookie süreleri.
- Global exception handling, correlation ID, security headers, management `no-store` ve health skeleton.
- Design token sistemi, Mellos fontu ve Public/Account/Admin/SuperAdmin Razor sayfaları.
- Home hero frame-sequence, product-card motion ve erişilebilir page-transition davranışı.
- Desktop'ta üstte gerçekten şeffaf, scroll'da animasyonla koyulaşan navbar; mobilde disclosure menü, focus restore ve body scroll lock.
- Commerce backend: katalog, sepet, favori, kampanya/kupon, checkout, sipariş, ödeme, refund, iade, yorum, fatura, stok, shipment, notification ve raporlama.
- Guest-cart merge, callback/checkout idempotency, stok concurrency, IDOR kontrolleri, duplicate-return sınırı ve geçersiz kupondan güvenli kurtarma.
- Development SQLite ve deterministik/idempotent kahve kataloğu seed'i.
- Absolute ve kalıcı Data Protection key-ring, sertifikayla key-at-rest şifreleme, trusted proxy allow-list ve Production SMTP/Identity SQL outbox.
- Production'da Mock payment reddi, güvenli `Disabled` ödeme varsayılanı, gerçek shipping adapter eksikliğinde startup fail-closed davranışı.
- HMAC-SHA256, timestamp toleransı, constant-time imza kontrolü ve replay rezervasyonlu payment webhook kapısı.
- AFK expiry sırasında tek antiforgery logout POST'u, üç saniyelik fallback ve portal-scope cross-tab senkronizasyonu.
- Gerçek Stripe Checkout Session tabanlı `IPaymentGateway`/`IPaymentWebhookVerifier`: gerçek API çağrıları, `VerifyAsync`'in Stripe'tan tekrar doğrulaması (müşteri query string'ine güvenilmiyor), Stripe'ın gerçek HMAC webhook imza şeması.
- **Customer Account Center**: `/account` gerçek server-side projected dashboard (profil, sayaçlar, fiyatlandırılmış sepet özeti, son sipariş, quick-link'ler); owner-only private profil fotoğrafı (magic-byte/MIME/uzantı/2 MiB kontrolü); doğrulamalı e-posta değişikliği ve Identity policy tabanlı parola değişikliği, ikisi de security-stamp yenileyip mevcut oturumlar dahil tüm customer cookie'lerini geçersiz kılıyor.
- **Admin/SuperAdmin commerce dashboard'u**: `/admin` ve `/superadmin` placeholder'ları gerçek commerce projection'ına bağlandı (son 30 gün net gelir, ödenen sipariş, ortalama sipariş, kritik stok, canlı sipariş/sevkiyat/iade/yorum/mesaj kuyrukları, son 5 sipariş).
- **Ortak yönetim navigasyon kabuğu**: ürün/katalog/sipariş/sevkiyat/fatura/iade/kampanya/kupon/yorum/mesaj/rapor modülleri Admin ve SuperAdmin'de aynı sidebar/mobil-drawer'a bağlandı (`aria-expanded`/`aria-current`/Escape/backdrop/focus-restore/focus-trap/`inert`/body-scroll-lock/reduced-motion).
- **Çift-rollü yönetim hesabı ve güvenlik merkezi**: aynı normalize e-posta için Admin+SuperAdmin rollerini birleştiren idempotent seed akışı; eksik çift/parola uyuşmazlığı/Production onayı eksikliği/Customer hesabının sessiz yükseltilmesi startup'ta reddediliyor; eski hesaplar için açık onaylı tek seferlik retirement akışı; `/admin/security` ve `/superadmin/security` altında paylaşılan e-posta/parola merkezi; kritik credential değişikliğinde Admin+SuperAdmin oturumları birlikte iptal ediliyor; ortak tek kullanımlık flash bildirimleri.
- **Ürünler/koleksiyon filtre sidebar'ının premium yeniden tasarımı**: altın filtre ikonu + başlık, ikon+döner-chevron'lu accordion grupları, `appearance:none` restyle edilmiş select/checkbox'lar, çift-tutamaçlı fiyat aralığı slider'ı (Min/Maks input'larıyla çift yönlü senkron), kullanıcı tarafından sağlanan arka plan görseliyle (`shop-banner.webp`) sayfa başlığı banner'ı.
- **Admin ürün formu iyileştirmesi**: yeni ürün formu girişleri `sessionStorage`'a taslak olarak kaydedip sayfa yenilendiğinde geri yüklüyor; stok +/- kontrolleri etiketli, `aria-busy` durumlu erişilebilir butonlara dönüştürüldü.
- `StockMovement`→`Product` navigation'ındaki EF query-filter uyarısı giderildi; soft-delete edilmiş ürünlere ait geçmiş stok hareketleri audit/raporlama sorgularında artık kayboluyor değil.
- Production SMTP ve Data Protection key-ring için adım adım deployment runbook'u (`docs/deployment/PRODUCTION_SETUP.md`).
- Restore ve build: 0 uyarı, 0 hata. Testler: frontend 17/17, unit 82/82, integration 121/121.
- Altı migration doğru sırada ve pending model change yok. Gerçek `.\SQLEXPRESS` smoke veritabanına uygulandı; idempotent script art arda iki kez hatasız çalıştı ve `DBCC CHECKDB` temiz geçti. Smoke veritabanı doğrulama sonunda kaldırıldı.
- NuGet direct/transitive vulnerability taraması temiz; first-party JavaScript dosyaları syntax kontrolünden geçti.
- Localhost HTTP taraması ve gerçek Chrome masaüstü/mobil smoke tamamlandı; console/network hatası yok.

## Teslim ve doğrulama

- [x] Foundation ile Ajan 1, Ajan 2 ve Ajan 4 teslimleri `integration` üzerine alındı.
- [x] Commerce hardening, production-readiness, auth/test-clock düzeltmesi, frontend/UX hardening ve production proxy/Data Protection/security hardening `integration`'a alındı.
- [x] Payment provider/webhook hardening ve AFK expiry/cross-tab frontend hardening `integration`'a alındı.
- [x] Gerçek Stripe ödeme adaptörü, StockMovement audit fix, SQL Server smoke ve deployment runbook `integration`'a alındı.
- [x] Customer Account Center commit'lendi ve merge edildi (`3b4db03`).
- [x] Admin commerce dashboard/navigasyon ve çift-rollü yönetim hesabı + güvenlik merkezi, önceki oturumdan commit'siz kalan haliyle bulunup doğrulanarak commit'lendi (`0d4d8e9`).
- [x] Ürünler filtre sidebar'ının premium yeniden tasarımı + shop banner `integration`'a merge edildi (`7eff985`).
- [x] Admin ürün formu taslak/stok iyileştirmesi commit'lendi (`70a1390`).
- [x] Public route'lar 200; müşteri/admin/superadmin korumalı route'ları doğru login URL'lerine 302.
- [x] Desktop navbar 0 px'te şeffaf, scroll'da `is-scrolled`; mobil menü aç/kapat/Escape/focus/overflow davranışları gerçek Chrome ile doğrulandı.

## Açık Production kapıları

- Durable/distributed webhook replay store (şu an in-memory, tek instance için yeterli) ve gerçek `IShippingProvider` adaptörü hâlâ yok — kargo entegrasyonu kullanıcı tercihiyle kasıtlı olarak kapsam dışı bırakıldı (prototip/staj teslimi); shipping startup guard'ı bu yüzden kaldırılmamalıdır.
- Gerçek Stripe test-mode anahtarı bu ortamda mevcut değil; `Payment:Provider=Stripe` + `Stripe:SecretKey` koduyla hazır ama uçtan uca tarayıcı smoke'u henüz yapılmadı (kullanıcı kendi Stripe test hesabından anahtar sağlarsa doğrulanabilir).
- Production SMTP değerleri, absolute shared Data Protection key-ring yolu ve private key erişimli sertifika deployment ortamında açıkça sağlanmalıdır. Development/Testing dış gönderim yapmayan deterministik mock'larda kalır. Bu kalıcı operasyonel gereksinim; hangi anahtarın hangi ortam değişkeninden/secret store'dan geleceği ve key-ring'in neden replica'lar arasında paylaşılması gerektiği [`docs/deployment/PRODUCTION_SETUP.md`](../deployment/PRODUCTION_SETUP.md) runbook'unda adım adım belgelenmiştir; eksik config ile fail-closed davranış testlerle kanıtlanmıştır.
- Gerçek bir `.\SQLEXPRESS` instance'ına karşı migration/idempotent-script doğrulaması bu ortamda yapıldı; gerçek production topolojisinde (uzak SQL Server, farklı yetkiler) ayrıca doğrulanmalı.
- Admin ürün oluşturma/kampanya hedefleme formlarındaki Koleksiyon/Marka/vb. seçimleri hâlâ gerçek `<select>` değil, Katalog sayfasından kopyala-yapıştır ID girişi — Admin Products/Campaigns action'ları view'a `CatalogLookupSet` döndürmeye başlarsa gerçek dropdown'a çevrilebilir.
- Return/Review "yeni talep oluştur" formları `OrderDetails.Items`'ta artık `OrderItemId` mevcut olduğu için kuruldu (bkz. commit `90a7efd`); bu madde kapandı.

## Repo / dağıtım durumu

- Bu depoda **hiç git remote yok** (`git remote -v` boş) — proje şu ana kadar hiçbir yere (GitHub dahil) push edilmedi, tamamen yerel.
- `main` branch'i `integration`'ın halen çok gerisinde; orkestrasyon planındaki "temiz `integration`'ı `main`'e alma" adımı henüz yapılmadı.
