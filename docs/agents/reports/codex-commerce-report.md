# Ajan 4 — Commerce Çekirdeği Raporu

## Handoff

- Branch: `agent/codex-commerce`
- Worktree: `C:\Users\Kerim Açıkgöz\Desktop\aeternum-codex-commerce`
- Güncel taban: `integration` / `f008db0` (commerce çekirdeği, Ajan 1 commerce Razor view'ları ve Ajan 3 invalid-cookie sertleştirmesi dahil)
- Uygulama commit'i: `2673542` (`feat: implement commerce core`)
- Development SQLite runtime commit'i: `f4af055` (`feat: enable sqlite commerce development runtime`)
- Integration teslim merge commit'i: `62e4de5` (`Merge branch 'agent/codex-commerce' into integration`)
- Commerce hardening commit'i: `7cabb93` (`fix: harden commerce ownership and recovery flows`)
- Commerce hardening merge commit'i: `18d5d99` (`Merge branch 'agent/commerce-hardening' into integration`)
- Entegrasyon contract regresyon testi: `9c940fa` (`test: lock hero and navbar integration contract`)
- Frontend JSON uyumluluk düzeltmesi: `099f0b4` (`fix: align commerce mutations with frontend JSON`)
- Merge durumu: Commerce çekirdeği `62e4de5`, merge-sonrası sahiplik/idempotency hardening'i `18d5d99` üzerinden integration'a alındı. Coordinator post-purchase/public navigation boşluklarını `90a7efd` ile kapattı; final doğrulama tabanı frontend merge'i sonrası `ec20497` oldu.
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
- Guest merge artık geçersiz kuponu idempotent temizler; inactive veya stokta olmayan satırları authenticated sepete taşımaz. Campaign sayfasındaki ilk authenticated commerce isteği de aynı merge tabanını kullanır.
- Ürün kartı varyant belirtmeden ekleme yaptığında servis stoklu aktif varyantı ağırlık/ID sırasıyla deterministik seçer; explicit varyant doğrulaması korunur.
- `IDiscountEngine`: toplam faydaya göre en iyi kampanya, kombinasyon kurallı kupon, limit kontrolleri, negatif olmayan toplam, server-side tax/shipping hesabı.
- `ICheckoutService`: PendingPayment snapshot, checkout/provider transaction idempotency, callback transaction'ı, concurrency kontrollü stok, invoice ve notification outbox.
- Callback anında stok yoksa otomatik refund denenir ve sipariş iptal edilir; refund başarısızlığı kalıcı kayıtla görünür bırakılır.
- `IOrderService`: sevkiyat öncesi refund-aware iptal ve stoğun yalnız bir kez iadesi; order/invoice ownership kontrolü.
- `IReturnService`: delivered purchase + configuration tabanlı pencere; miktar sınırı; stok yalnız ProductReceived + admin restock kararında döner.
- Aynı return isteğine aynı order item'ın iki kez eklenmesi reddedilir; böylece satır bazlı iade/refund üst sınırı duplicate payload ile aşılamaz.
- `IReviewService`: delivered order item ownership ve sipariş kalemi başına ömür boyu tek yorum.
- `IReportingService`: brüt satış, indirim, vergi, kargo, tamamlanmış refund ve net gelir ayrı; UTF-8 BOM ve spreadsheet-formula korumalı CSV.
- Tüm unsafe MVC action'ları global antiforgery filtresinde; doğrulanmış provider webhook'u istisna. Customer IDOR filtreleri ve mevcut `AdminArea` policy'si korunuyor.
- Payment callback'in eksik veya aşırı uzun provider alanları 400 döner; yabancı address update'i sahiplik sızıntısı olmadan conflict sonucuna çevrilir.
- Dosya storage uzantı, MIME, magic-byte, boyut ve path kontrolleri uygular; başarısız yüklemede partial dosyayı siler.

## Providerlar

- Veritabanı provider'ı `Database:Provider` ile `SqlServer` veya `Sqlite` seçilebilir. Base/production varsayılanı SQL Server, `appsettings.Development.json` ise yerel ve git-ignored SQLite kullanır; design-time migration factory SQL Server olarak kalır.
- `IPaymentGateway`: deterministik `MockPaymentGateway` initialize/verify/refund.
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
- Unit: 33/33 geçti.
- Integration/contract: 60/60 geçti.
- Toplam: 93/93 geçti.
- NuGet direct/transitive vulnerability taraması: tüm projeler temiz.
- Anasayfa navbar CSS/JS bağlantıları, `is-scrolled` davranış kancaları ve hero reduced-motion değerinin yanlışlıkla zorlanmaması için regresyon testi eklendi.
- Development browser smoke: `/`, `/products`, `/products/eternal-light`, `/categories`, `/categories/nitelikli-kahve`, `/about`, `/contact`, `/campaigns` ve `/cart` 200; korumalı customer/admin route'ları 302 challenge; seeded `Eternal Light` kartı ile görseli yüklendi.
- Gerçek JSON mutation smoke: CSRF header ile varyantsız add `200`, quantity update `200`, uyumsuz campaign+coupon kuralı `409`, remove `200`; kaldırma sonrası item count `0`.
- Merge-sonrası `CommerceHardeningTests`: 11/11; guest-cart recovery, callback input bounds, ownership ve duplicate-return senaryoları ayrıca kilitlendi.
- Gerçek Chrome smoke: desktop top/scroll navbar, 390×844 mobil disclosure/Escape/focus/overflow ve console/network hata kontrolü geçti.

Kapsanan senaryolar: domain price/SKU/slug/stock kuralları, state transition, çok sayfalı PDF, katalog filtre/paging/projection, favorite/cart ownership, guest merge, JSON model binding, varsayılan varyant seçimi, kampanya-kupon matematiği ve limitleri, checkout idempotency, success/fail, tekrar callback, tekrar provider transaction, ödeme sonrası stok tükenmesi/refund, concurrency token ile negatif stok önleme, cancellation restore-once, invoice/order/return IDOR, delivered-purchase review, return restock, rapor matematiği/CSV, outbox mock delivery, route aileleri, antiforgery ve admin policy.

## Bilinen entegrasyon notları

- Public, Account ve Admin commerce view'ları integration'dadır. Contact sayfası ile teslim edilmiş sipariş satırından return/review oluşturma formları `90a7efd` ile tamamlandı; `OrderLineDetails` gerçek `OrderItemId` taşır.
- SQL Server LocalDB/runtime eksikliği Development SQLite override ile giderildi; `dotnet run` ve gerçek tarayıcı smoke artık çalışır. Development SQLite `EnsureCreated` kullandığı için ileride model değiştiğinde yerel `.db` dosyası yeniden oluşturulmalıdır; production migration akışı SQL Server olarak değişmeden kalır.
- Production tax oranı varsayılmadı; ürün verisinden gelir. Production shipping threshold/fee, provider ve SMTP/SMS değerleri deployment configuration ile açıkça verilmelidir.
- Production notification mock'u ve gerçek adapter'ı olmayan payment/shipping mock'ları startup validation'da fail-closed davranır. Gerçek payment/shipping adapter, secret yönetimi ve provider contract testleri deployment ön koşuludur.
- `Product` global filter'ı ile required `StockMovement.Product` navigation'ı için kalan EF uyarısı kayıtları silmez ve doğrudan inventory/audit sorgularını gizlemez. Soft-delete ürün hareketlerinin navigation join'li tarihsel raporlarda görünmesi için `IgnoreQueryFilters()` veya optional/history ilişki migration'ı izlenmelidir.
- `Program.cs`, auth/security configuration ve dondurulmuş contract dosyaları değiştirilmedi.
