# Ajan 4 — Commerce ve Ödeme Güvenliği Raporu

## Handoff

- Branch: `agent/codex-commerce`
- Güncel entegrasyon tabanı: `integration` / `0960d14`
- Commerce çekirdeği: `2673542`
- Development SQLite runtime: `f4af055`
- Merge-sonrası sahiplik ve recovery hardening: `7cabb93` (`18d5d99` ile integration'a alındı)
- Payment provider fail-closed: `e32e23a`
- Authenticated webhook delivery: `74ade39`
- Commerce format borcu kapanışı: `66a147d`
- Disabled-provider persistence testi: `8ff0db8`
- Production mock startup testi: `bac74f8`
- StockMovement audit/query-filter düzeltmesi: `535bfac`

Bu branch doğrudan `main` veya `integration` üzerine yazılmadı. Final merge ve çözüm-geneli regresyon Coordinator sorumluluğundadır.

## Commerce kapsamı

- Katalog, müşteri, promotion, sepet, favori, checkout, sipariş, ödeme, refund, sevkiyat, fatura, stok, iade, yorum, bildirim ve raporlama modelleri ile servisleri tamamlandı.
- Para alanları `decimal`, zamanlar `DateTimeOffset`, stoklar `int`; kritik aggregate'lerde uygulama tarafından döndürülen `Guid` concurrency token kullanılır.
- Müşteri sahipliği yalnız authenticated `UserId` üzerinden uygulanır; invoice/order/address/return/review IDOR sınırları servis ve controller testleriyle kilitlidir.
- Guest cart cookie'si Data Protection ile korunur. Authenticated merge idempotenttir; geçersiz kuponu temizler, pasif veya stoksuz satırları taşımaz.
- Checkout ve provider transaction idempotency, stok concurrency, başarısız refund görünürlüğü ve stoğun yalnız bir kez iadesi korunur.
- Aynı return isteğinde duplicate order item reddedilir; callback alanları uzunluk ve zorunluluk sınırlarından geçer.
- Notification/Identity e-postaları SQL outbox üzerinden lease, bounded batch, exponential retry ve terminal failure politikasıyla işlenir.

## Migrationlar

1. `20260804155915_InitialIdentity`
2. `20260804170142_AddCommerceCatalogAndCustomer`
3. `20260804170153_AddCommerceCheckoutAndFulfillment`
4. `20260804170203_AddCommerceEngagement`

SQL Server migration sırası ve pending-model kontrolü bu teslimde yeniden doğrulandı. `StockMovement` değişikliği yalnız runtime query-filter metadata'sıdır; kolon, index veya FK şeması değişmediği için yeni migration üretilmedi ve ModelSnapshot değişmedi. Integration testleri SQLite in-memory sağlayıcısını kullanır.

## Provider ve webhook güvenliği

- Base ve örnek ayarlarda `Payment:Provider=Disabled`; yalnız Development ve Testing açıkça `Mock` seçer.
- `PaymentOptionsValidator`, boş veya kayıtsız provider adlarını reddeder. `Mock` yalnız Development/Testing ortamında kabul edilir; `Disabled` güvenli kapalı durumdur.
- `ResolveGateway`, `Disabled` durumunda order/payment kaydı oluşmadan fail eder.
- Webhook route'u `/payments/{provider}/callback` olup antiforgery istisnası yalnız verifier kapısının arkasındadır.
- Callback alanları zorunluluk ve uzunluk sınırlarından, form body ise 64 KiB sınırından geçer.
- Route provider'ı aktif config ile eşleşmelidir; unknown veya disabled provider `401` döner.
- `HmacSha256PaymentWebhookVerifier`, raw body + provider + event ID + Unix timestamp canonical payload'ı üzerinde HMAC-SHA256 kullanır; imzayı constant-time karşılaştırır.
- Event ID karakter/uzunluk sınırı, 30 saniye–15 dakika zaman toleransı, geçmiş ve gelecek timestamp reddi ve atomik replay rezervasyonu uygulanır.
- Mock verifier environment'ı bağımsız olarak tekrar kontrol eder; Production'da kabul etmez.

## Production readiness

- Gerçek payment adapter'ı olmadığı için Production varsayılanı `Disabled` kalır ve mutation/persistence yapmaz.
- Gerçek shipping adapter'ı olmadığı için Production shipping validator startup'ı fail-closed durdurur.
- Production notification mock'u reddedilir; SMTP ve Identity outbox ayarları startup sırasında doğrulanır.
- Gerçek bir payment entegrasyonu hem `IPaymentGateway` hem `IPaymentWebhookVerifier` kaydı, secret yönetimi ve provider contract testleri sağlamadan allow-list'e eklenmemelidir.
- `InMemoryPaymentWebhookReplayStore` tek-process referans implementasyonudur. Çok instance'lı Production için event ID'yi atomik ve süreli rezerve eden distributed/durable store zorunludur. Veritabanındaki provider transaction unique constraint'i state mutation idempotency'sini ayrıca korur.

## Sözleşmeler

- Public: `/products`, `/products/{slug}`, `/search`, `/categories`, `/categories/{slug}`, `/about`, `/contact`, `/campaigns`, `/cart`, `/favorites`, `/checkout`, `/payments/{provider}/callback`.
- Customer: `/account/addresses`, `/account/orders`, `/account/invoices`, `/account/returns`, `/account/reviews`, `/account/notifications`.
- Admin: `/admin/products`, `/admin/catalog`, `/admin/orders`, `/admin/shipments`, `/admin/invoices`, `/admin/returns`, `/admin/campaigns`, `/admin/coupons`, `/admin/reviews`, `/admin/messages`, `/admin/reports`.
- Ayrıntılı route/ViewModel sözleşmesi: `docs/contracts/requests/codex-commerce-20260804-commerce-routes-viewmodels.md`.

## EF 10622 ve stok hareketi audit kararı

- Uyarı ayrıntılı integration test çıktısında yeniden üretildi: filtered `Product`, required `StockMovement.Product` ilişkisinin principal tarafıydı. Normal `dotnet build` model initialize etmediğinden baseline compile çıktısı zaten 0 uyarıydı.
- Production sorgu taramasında `StockMovement.Product` navigation'ını `Include`, join veya projection ile kullanan sorgu bulunmadı. `ReportingService` yalnız `Order` ve `Refund` okur.
- Gerçek StockMovement okumaları `InventoryService` ve `ReturnService` içindeki scalar idempotency kontrolleridir. Her ikisi de soft-delete edilmiş ürünlerin geçmiş hareketlerini görebilmek için açıkça `IgnoreQueryFilters()` kullanır.
- `StockMovementConfiguration`, principal ile eşleşen `Product.DeletedAtUtc == null` filter'ını taşır. Bu EF 10622'yi model seviyesinde kaldırırken required `ProductId`, non-null kolon ve `Restrict` FK bütünlüğünü korur.
- Optional `Guid? ProductId` migration'ı seçilmedi; mevcut zorunlu audit referansını ve unique idempotency index invariantını gereksiz yere zayıflatacaktı.
- Matching filter'ın normal filtreli sorgularda geçmiş satırı gizlemesi bilinçlidir. Tarihsel/audit sorguları `IgnoreQueryFilters()` ile opt-in olur; regresyon testi soft-delete ürün sonrası normal sorgunun satırı gizlediğini ve audit sorgusunun `Include(Product)` ile hareketi ve silinmiş ürün ayrıntılarını yüklediğini doğrular.

## Doğrulama

- `dotnet restore`: başarılı.
- `dotnet build AETKAHVE.sln --no-restore`: başarılı, 0 uyarı / 0 hata.
- Unit: 54/54 başarılı.
- Integration: 77/77 başarılı.
- Frontend contract: 5/5 başarılı.
- Toplam: 136/136 başarılı.
- Commerce hardening sınıfı: 11/11 başarılı.
- Değişen C# dosyalarında `dotnet format --verify-no-changes --no-restore`: başarılı.
- `git diff --check`: başarılı.
- Ayrıntılı EF regresyon koşusunda `10622` bulunmadı.
- `dotnet ef migrations has-pending-model-changes`: model değişikliği yok.
- Idempotent SQL: 54.628 bayt; SHA-256 `DF956CA76B4DED1E5885DB67F90FAE385F1CAF9FDACD32CDAE147E6F1FC705DF`.
- Migration listesi dört kaydı doğru sırada gösterdi. LocalDB runtime bulunmadığı için yalnız applied-state okunamadı; script ve model karşılaştırması bundan etkilenmedi.

## Bilinen sınırlamalar

- Production payment/shipping adapter'ları ve gerçek provider secret'ları teslim edilmedi; güvenli fail-closed davranış bilinçlidir.
- SQL Server instance'ı bu ortamda çalıştırılmadı; migration üretimi SQL Server, çalışan testler SQLite ile doğrulanır.
- Çok-instance Production webhook replay store ve gerçek payment/shipping adapter'ları hâlâ ayrı deployment kapılarıdır; StockMovement EF 10622 takip işi bu teslimle kapanmıştır.

## Önceki Coordinator final integration — 2026-08-05

- Ajan 4 branch'i `84610f2` ile, ardından AFK frontend teslimi `243b9f6` ile `integration` üzerine alındı.
- Release build 0 uyarı / 0 hata; frontend 5/5, unit 54/54 ve integration 77/77 geçti.
- Dört migration doğru sırada; pending model change yok; idempotent SQL 54.628 bayt üretildi.
- NuGet vulnerability audit, 26 JavaScript syntax kontrolü, tam solution format ve `git diff --check` geçti.
