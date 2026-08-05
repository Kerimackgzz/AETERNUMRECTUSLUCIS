# Ajan 4 — Commerce ve Ödeme Güvenliği Raporu

## Handoff

- Branch: `agent/codex-commerce`
- Güncel entegrasyon tabanı: `integration` / `b86da0c`
- Commerce çekirdeği: `2673542`
- Development SQLite runtime: `f4af055`
- Merge-sonrası sahiplik ve recovery hardening: `7cabb93` (`18d5d99` ile integration'a alındı)
- Payment provider fail-closed: `e32e23a`
- Authenticated webhook delivery: `74ade39`
- Commerce format borcu kapanışı: `66a147d`
- Disabled-provider persistence testi: `8ff0db8`
- Production mock startup testi: `bac74f8`

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

SQL Server migration sırası, pending-model kontrolü ve idempotent script Coordinator final kapısında yeniden doğrulanacaktır. Integration testleri SQLite in-memory sağlayıcısını kullanır.

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

## Doğrulama

- `dotnet restore`: başarılı.
- Release build: başarılı, 0 uyarı / 0 hata.
- Unit: 54/54 başarılı.
- Integration: 74/74 başarılı.
- Toplam: 128/128 başarılı.
- `dotnet format --verify-no-changes --no-restore`: başarılı.
- `git diff --check`: başarılı.
- EF ve vulnerability kontrolleri Coordinator final kapısında yeniden çalıştırılacaktır.

## Bilinen sınırlamalar

- Production payment/shipping adapter'ları ve gerçek provider secret'ları teslim edilmedi; güvenli fail-closed davranış bilinçlidir.
- SQL Server instance'ı bu ortamda çalıştırılmadı; migration üretimi SQL Server, çalışan testler SQLite ile doğrulanır.
- `Product` global query filter ile required `StockMovement.Product` navigation uyarısı kayıt silmez; soft-delete ürün hareketlerinin navigation join'li tarihsel raporları için `IgnoreQueryFilters()` veya optional/history ilişki tasarımı takip işidir.
