# Ajan 4 — Commerce Çekirdeği Raporu

## Handoff

- Branch: `agent/codex-commerce`
- Worktree: `C:\Users\Kerim Açıkgöz\Desktop\aeternum-codex-commerce`
- Güncel taban: `integration` / `bfd58e9` (Ajan 1, 2 ve 3 teslimleri, hero/navbar düzeltmesi ve invalid-cookie sertleştirmesi dahil)
- Uygulama commit'i: `2673542` (`feat: implement commerce core`)
- Development SQLite runtime commit'i: `f4af055` (`feat: enable sqlite commerce development runtime`)
- Güncel integration merge commit'i: `d68cb10` (`Merge branch 'integration' into agent/codex-commerce`)
- Entegrasyon contract regresyon testi: `9c940fa` (`test: lock hero and navbar integration contract`)
- Merge durumu: Commerce çekirdeği `6b2b47d` ile integration geçmişine alındı ve Coordinator checkout yarışını `bfd58e9` ile kayda geçirdi. Sonraki navbar contract testi ile Development SQLite runtime'ı henüz integration'da değildir; Coordinator güncel `agent/codex-commerce` HEAD'ini almalıdır.
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
- `IDiscountEngine`: toplam faydaya göre en iyi kampanya, kombinasyon kurallı kupon, limit kontrolleri, negatif olmayan toplam, server-side tax/shipping hesabı.
- `ICheckoutService`: PendingPayment snapshot, checkout/provider transaction idempotency, callback transaction'ı, concurrency kontrollü stok, invoice ve notification outbox.
- Callback anında stok yoksa otomatik refund denenir ve sipariş iptal edilir; refund başarısızlığı kalıcı kayıtla görünür bırakılır.
- `IOrderService`: sevkiyat öncesi refund-aware iptal ve stoğun yalnız bir kez iadesi; order/invoice ownership kontrolü.
- `IReturnService`: delivered purchase + configuration tabanlı pencere; miktar sınırı; stok yalnız ProductReceived + admin restock kararında döner.
- `IReviewService`: delivered order item ownership ve sipariş kalemi başına ömür boyu tek yorum.
- `IReportingService`: brüt satış, indirim, vergi, kargo, tamamlanmış refund ve net gelir ayrı; UTF-8 BOM ve spreadsheet-formula korumalı CSV.
- Tüm unsafe MVC action'ları global antiforgery filtresinde; doğrulanmış provider webhook'u istisna. Customer IDOR filtreleri ve mevcut `AdminArea` policy'si korunuyor.
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

- Public: `/products`, `/products/{slug}`, `/search`, `/categories/{slug}`, `/campaigns`, `/cart`, `/favorites`, `/checkout`, `/payments/{provider}/callback`.
- Customer: `/account/addresses`, `/account/orders`, `/account/invoices`, `/account/returns`, `/account/reviews`, `/account/notifications`.
- Admin: `/admin/products`, `/admin/catalog`, `/admin/orders`, `/admin/shipments`, `/admin/invoices`, `/admin/returns`, `/admin/campaigns`, `/admin/coupons`, `/admin/reviews`, `/admin/messages`, `/admin/reports`.
- Ayrıntılı method ve ViewModel sözleşmesi: `docs/contracts/requests/codex-commerce-20260804-commerce-routes-viewmodels.md`.
- `HomeController` async featured query'ye bağlandı; frozen Home/ProductCard property şekilleri aynen korundu ve endpoint URL'leri server-side üretildi.
- Razor, CSS ve JavaScript dosyaları Ajan 4 uygulama commit'inde değiştirilmedi. Ajan 1/2 teslimleri branch'e alındıktan sonra anasayfanın hero/navbar markup ve asset bağlantıları integration contract testiyle doğrulandı.

## Test sonucu

- Restore: başarılı.
- Build: 0 uyarı, 0 hata.
- Unit: 23/23 geçti.
- Integration/contract: 41/41 geçti.
- Toplam: 64/64 geçti.
- NuGet direct/transitive vulnerability taraması: tüm projeler temiz.
- Anasayfa navbar CSS/JS bağlantıları, `is-scrolled` davranış kancaları ve hero reduced-motion değerinin yanlışlıkla zorlanmaması için regresyon testi eklendi.
- Development browser smoke: `/` ve `/account/login` 200, anonim `/admin` 302; navbar üstte şeffaf ve scroll'da `is-scrolled`; seeded `Eternal Light` kartı ile görseli yüklendi.

Kapsanan senaryolar: domain price/SKU/slug/stock kuralları, state transition, çok sayfalı PDF, katalog filtre/paging/projection, favorite/cart ownership, guest merge, kampanya-kupon matematiği ve limitleri, checkout idempotency, success/fail, tekrar callback, tekrar provider transaction, ödeme sonrası stok tükenmesi/refund, concurrency token ile negatif stok önleme, cancellation restore-once, invoice/order/return IDOR, delivered-purchase review, return restock, rapor matematiği/CSV, outbox mock delivery, route aileleri, antiforgery ve admin policy.

## Bilinen entegrasyon notları

- Commerce Razor view'ları bu branch'te bilinçli olarak yoktur; controller/service smoke tamamlandı. `/products`, `/cart` ve diğer commerce sayfalarının tam browser smoke'u için Ajan 1'in route/ViewModel request belgesindeki view'ları eklemesi gerekir.
- SQL Server LocalDB/runtime eksikliği Development SQLite override ile giderildi; `dotnet run` ve gerçek tarayıcı smoke artık çalışır. Development SQLite `EnsureCreated` kullandığı için ileride model değiştiğinde yerel `.db` dosyası yeniden oluşturulmalıdır; production migration akışı SQL Server olarak değişmeden kalır.
- Production tax oranı varsayılmadı; ürün verisinden gelir. Production shipping threshold/fee, provider ve SMTP/SMS değerleri deployment configuration ile açıkça verilmelidir.
- `Program.cs`, auth/security configuration ve dondurulmuş contract dosyaları değiştirilmedi.
