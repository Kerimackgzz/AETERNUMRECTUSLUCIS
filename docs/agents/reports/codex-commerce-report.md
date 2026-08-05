# Ajan 4 — Commerce Çekirdeği Raporu

## Handoff

- Branch: `agent/codex-commerce`
- Worktree: `C:\Users\Kerim Açıkgöz\Desktop\aeternum-codex-commerce`
- Güncel taban: `integration` / `446e9c4` (public/account/admin commerce sayfaları ve son integration handoff'ları dahil)
- Uygulama commit'i: `2673542` (`feat: implement commerce core`)
- Development SQLite runtime commit'i: `f4af055` (`feat: enable sqlite commerce development runtime`)
- Integration teslim merge commit'i: `62e4de5` (`Merge branch 'agent/codex-commerce' into integration`)
- Entegrasyon contract regresyon testi: `9c940fa` (`test: lock hero and navbar integration contract`)
- Frontend JSON uyumluluk düzeltmesi: `099f0b4` (`fix: align commerce mutations with frontend JSON`)
- Payment provider fail-closed commit'i: `e32e23a` (`fix: fail closed for unsafe payment providers`)
- Webhook authentication commit'i: `74ade39` (`feat: authenticate payment webhook deliveries`)
- Commerce format borcu kapanışı: `66a147d` (`style: format commerce services and tests`)
- Disabled-provider persistence testi: `8ff0db8` (`test: prove disabled payment fails before persistence`)
- Merge durumu: Bu sertleştirme commit'leri yalnız `agent/codex-commerce` branch'indedir; integration'a merge/push yapılmadı.
- Kök worktree ve Ajan 1/2'nin Razor/CSS/JS kaynakları Ajan 4 uygulama commit'inde değiştirilmedi.

## Tamamlanan kapsam

- Katalog, müşteri, promotion, sipariş/ödeme/refund/sevkiyat/fatura/stok ve engagement aggregate'leri eklendi.
- Tüm para alanları `decimal(18,2)`, vergi oranları `decimal(5,2)`, zaman alanları `DateTimeOffset`, stoklar `int` olarak yapılandırıldı.
- Product, ProductVariant, Order, Payment, Refund, Shipment, ReturnRequest ve NotificationDelivery için uygulama tarafından döndürülen `Guid` concurrency token kullanıldı.
- Kullanıcı ilişkileri yalnız `Guid UserId` üzerinden kuruldu; Identity tabloları ve authentication şeması değiştirilmedi.
- `AppDbContext` commerce DbSet/configuration'larıyla genişletildi.
- Development seed sabit ID ve zamanlarla, entity bazında idempotent katalog, varyant, kampanya ve kupon üretir; Testing/Production'da çalışmaz. SQLite Development şemasını ilk çalıştırmada güvenli biçimde oluşturur ve seed görselini mevcut hero poster asset'ine bağlar; eski eksik görsel yolunu idempotent olarak onarır.

## Migrationlar

1. `20260804170142_AddCommerceCatalogAndCustomer`
2. `20260804170153_AddCommerceCheckoutAndFulfillment`
3. `20260804170203_AddCommerceEngagement`

EF doğrulamaları:

- Migration listesi doğru sırada ve foundation `InitialIdentity` migration'ı korunuyor.
- `dotnet ef migrations has-pending-model-changes`: model değişikliği yok.
- SQL Server için 54.628 bayt idempotent SQL başarıyla üretildi.
- SQLite in-memory schema kurulumu ve commerce akışları integration testlerinde doğrulandı.

## Servisler ve güvenlik

- `ICatalogQueryService`: server-side filtre, sort, projection ve paging; yalnız sayfadaki favorite ID'lerini ek sorgular.
- `ICartService`: Data Protection korumalı HttpOnly guest cookie, unique cart/item constraint'leri ve idempotent authenticated merge.
- Ürün kartı varyant belirtmeden ekleme yaptığında servis stoklu aktif varyantı ağırlık/ID sırasıyla deterministik seçer; explicit varyant doğrulaması korunur.
- `IDiscountEngine`: toplam faydaya göre en iyi kampanya, kombinasyon kurallı kupon, limit kontrolleri, negatif olmayan toplam, server-side tax/shipping hesabı.
- `ICheckoutService`: PendingPayment snapshot, checkout/provider transaction idempotency, callback transaction'ı, concurrency kontrollü stok, invoice ve notification outbox.
- Callback anında stok yoksa otomatik refund denenir ve sipariş iptal edilir; refund başarısızlığı kalıcı kayıtla görünür bırakılır.
- `IOrderService`: sevkiyat öncesi refund-aware iptal ve stoğun yalnız bir kez iadesi; order/invoice ownership kontrolü.
- `IReturnService`: delivered purchase + configuration tabanlı pencere; miktar sınırı; stok yalnız ProductReceived + admin restock kararında döner.
- `IReviewService`: delivered order item ownership ve sipariş kalemi başına ömür boyu tek yorum.
- `IReportingService`: brüt satış, indirim, vergi, kargo, tamamlanmış refund ve net gelir ayrı; UTF-8 BOM ve spreadsheet-formula korumalı CSV.
- Tüm unsafe MVC action'ları global antiforgery filtresinde; payment webhook istisnası state mutation öncesinde aktif provider eşleşmesi ve `IPaymentWebhookVerifier` authentication kapısından geçer. Customer IDOR filtreleri ve mevcut `AdminArea` policy'si korunuyor.
- Dosya storage uzantı, MIME, magic-byte, boyut ve path kontrolleri uygular; başarısız yüklemede partial dosyayı siler.

## Providerlar

- Veritabanı provider'ı `Database:Provider` ile `SqlServer` veya `Sqlite` seçilebilir. Base/production varsayılanı SQL Server, `appsettings.Development.json` ise yerel ve git-ignored SQLite kullanır; design-time migration factory SQL Server olarak kalır.
- Base/production `Payment:Provider` varsayılanı `Disabled`; Development ve Testing `Mock` değerini açıkça override eder. Options startup validator, `Mock` seçimini Development/Testing dışında ve kayıtlı olmayan provider adlarını tüm ortamlarda fail-fast reddeder.
- `IPaymentGateway`: deterministik `MockPaymentGateway` initialize/verify/refund yalnız Development/Testing için kullanılır. `Disabled` seçiminde gateway resolve işlemi order/payment veritabanına yazılmadan kapanır.
- `IPaymentWebhookVerifier`: route provider'ının aktif config ile eşleşmesini zorunlu kılar; unknown/disabled provider `401` döner. Mock verifier environment'ı ikinci kez kontrol eder.
- `HmacSha256PaymentWebhookVerifier`: en az 32-byte secret, raw form body, provider/event-id/Unix timestamp canonical payload, constant-time HMAC-SHA256 karşılaştırması, yapılandırılabilir 30 saniye–15 dakika zaman penceresi ve atomik replay rezervasyonu sağlar.
- `IShippingProvider`: deterministik mock create/track/cancel.
- `IInvoicePdfGenerator`: PDFsharp `6.2.4`; çok sayfalı PDF desteği.
- `IEmailSender`: mock veya SMTP; `ISmsSender`: mock veya açıkça unconfigured failure provider.
- `IFileStorageService` ve `IInvoiceStorage`: güvenli local storage implementasyonları.
- Notification outbox processor pending/failed/lease'i bitmiş processing kayıtlarını sınırlı batch, exponential retry ve maksimum deneme ile işler. Testing worker'ı yarış oluşturmamak için pasiftir; processor integration testinde doğrudan doğrulanır.

## Route ve ViewModel mapping

- Public: `/products`, `/products/{slug}`, `/search`, `/categories`, `/categories/{slug}`, `/about`, `/contact`, `/campaigns`, `/cart`, `/favorites`, `/checkout`, `/payments/{provider}/callback`.
- Customer: `/account/addresses`, `/account/orders`, `/account/invoices`, `/account/returns`, `/account/reviews`, `/account/notifications`.
- Admin: `/admin/products`, `/admin/catalog`, `/admin/orders`, `/admin/shipments`, `/admin/invoices`, `/admin/returns`, `/admin/campaigns`, `/admin/coupons`, `/admin/reviews`, `/admin/messages`, `/admin/reports`.
- Ayrıntılı method ve ViewModel sözleşmesi: `docs/contracts/requests/codex-commerce-20260804-commerce-routes-viewmodels.md`.
- `HomeController` async featured query'ye bağlandı; frozen Home/ProductCard property şekilleri aynen korundu ve endpoint URL'leri server-side üretildi.
- Cart, checkout, address, contact, return ve review input'ları `application/json` fetch sözleşmesine `[FromBody]` ile bağlandı; antiforgery header doğrulaması aynen korundu.
- Razor, CSS ve JavaScript dosyaları Ajan 4 uygulama commit'lerinde değiştirilmedi. Ajan 1 commerce view'ları integration üzerinden branch'e alındı; anasayfanın hero/navbar markup ve asset bağlantıları integration contract testiyle doğrulandı.

## Test sonucu

- Restore: başarılı.
- Build: 0 uyarı, 0 hata.
- Unit: 34/34 geçti.
- Commerce integration/contract filtresi: 26/26 geçti.
- Tam integration: 43/44 geçti; tek hata integration tabanındaki Ajan 3 AFK cookie deletion düzeltmesinin henüz integration'a alınmamasından kaynaklanan `Idle_timeout_deletes_the_management_authentication_cookie` baseline hatasıdır. Commerce/auth dosyasıyla giderilmedi; coordinator hotfix'i sonrası yeniden koşturulacak.
- `dotnet format --verify-no-changes`: hem commerce-scoped hem tam solution için geçti. `AdminCommerceService`, `CheckoutService`, `CommerceSeedService`, `EngagementServices`, `OrderService`, `CartController`, `CommerceBusinessRulesTests`, `CommerceFlowTests` ve `CommerceContractTests` whitespace borcu kapatıldı.
- NuGet direct/transitive vulnerability taraması: tüm projeler temiz.
- Anasayfa navbar CSS/JS bağlantıları, `is-scrolled` davranış kancaları ve hero reduced-motion değerinin yanlışlıkla zorlanmaması için regresyon testi eklendi.
- Development browser smoke: `/`, `/products`, `/products/eternal-light`, `/categories`, `/categories/nitelikli-kahve`, `/about`, `/contact`, `/campaigns` ve `/cart` 200; korumalı customer/admin route'ları 302 challenge; seeded `Eternal Light` kartı ile görseli yüklendi.
- Gerçek JSON mutation smoke: CSRF header ile varyantsız add `200`, quantity update `200`, uyumsuz campaign+coupon kuralı `409`, remove `200`; kaldırma sonrası item count `0`.

Kapsanan senaryolar: domain price/SKU/slug/stock kuralları, state transition, çok sayfalı PDF, katalog filtre/paging/projection, favorite/cart ownership, guest merge, JSON model binding, varsayılan varyant seçimi, kampanya-kupon matematiği ve limitleri, checkout idempotency, success/fail, tekrar callback, tekrar provider transaction, ödeme sonrası stok tükenmesi/refund, concurrency token ile negatif stok önleme, cancellation restore-once, invoice/order/return IDOR, delivered-purchase review, return restock, rapor matematiği/CSV, outbox mock delivery, route aileleri, antiforgery ve admin policy.

## Bilinen entegrasyon notları

- Public, Account ve Admin commerce view'ları integration'dadır. Contact sayfası ile teslim edilmiş sipariş satırından return/review oluşturma formları `90a7efd` ile tamamlandı; `OrderLineDetails` gerçek `OrderItemId` taşır.
- SQL Server LocalDB/runtime eksikliği Development SQLite override ile giderildi; `dotnet run` ve gerçek tarayıcı smoke artık çalışır. Development SQLite `EnsureCreated` kullandığı için ileride model değiştiğinde yerel `.db` dosyası yeniden oluşturulmalıdır; production migration akışı SQL Server olarak değişmeden kalır.
- Production tax oranı varsayılmadı; ürün verisinden gelir. Production shipping threshold/fee, provider ve SMTP/SMS değerleri deployment configuration ile açıkça verilmelidir.
- Gerçek production payment adapter'ı ve provider secret'ı teslim edilmediği için sistem production'da `Disabled` ile güvenli kapalıdır. Yeni adapter hem `IPaymentGateway` hem `IPaymentWebhookVerifier` kaydı sağlamalı ve options allow-list'i bilinçli olarak genişletmelidir.
- `InMemoryPaymentWebhookReplayStore` tek process/dev-test referans implementasyonudur. Çok instance'lı production için provider event ID'sini atomik ve süreli rezerve eden distributed/durable store gerekir; mevcut `(Provider, TransactionId)` unique veritabanı indeksi state mutation idempotency'sini ayrıca korur.
- Webhook doğrulaması `X-Forwarded-For` veya istemci IP'sini authentication faktörü yapmaz. Ajan 3'teki forwarded-header middleware yalnız açık trusted proxy/network allow-list ile etkinleştirilecektir.
- `Program.cs`, auth/security configuration ve dondurulmuş contract dosyaları değiştirilmedi.
