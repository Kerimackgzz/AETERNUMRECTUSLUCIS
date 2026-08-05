# Ajan 1 — Claude Design System ve Sayfalar — Durum Raporu

## Branch / Repo durumu

- Branch: `agent/claude-design-pages`; 2026-08-05 turu başında `integration` (`446e9c4`) ile fast-forward senkronlandı.
- Ajan 3 AFK client request'i backend sözleşmesine dokunulmadan Ajan 1 sahipliğinde tamamlandı; backend ajanıyla logout redirect/cookie davranışı doğrudan teyit edildi.
- Son uygulama/test commit'i: `80cd6db`; ilk AFK runtime commit'i: `fae14fd`.

## Tamamlanan sayfalar / bileşenler

**Design system foundation**
- `wwwroot/css/tokens.css` — renk (siyah/orman yeşili/altın), tipografi ölçeği, spacing, radius (düşük, pill yok), gölge, motion, z-index token'ları.
- `wwwroot/css/typography.css` — Mellos `@font-face` (otf+ttf, `font-display: swap`), h1–h3/logo Mellos, gövde/form/tablo sans-serif.
- `wwwroot/css/base.css` — reset, `prefers-reduced-motion` global kapatma, belirgin `:focus-visible`, skip-link, form/tablo temel stilleri.
- `wwwroot/css/layout.css` — container, section/stack/cluster/grid yardımcıları, `.surface` yüzey deseni.
- `wwwroot/css/components/button.css` — düşük-radius buton sistemi (primary/secondary/outline/ghost).
- `wwwroot/font/Mellos.otf`, `Mellos.ttf` — `assets/font/`'tan web projesine kopyalandı.

**Layoutlar** (hook'lar korunarak: `_Navbar`, `_PageTransitionOverlay`, Styles/PageScripts section, antiforgery, hero data attribute'ları)
- `_PublicLayout.cshtml`, `_AccountLayout.cshtml` — stylesheet bağlantıları + skip-link.
- `Areas/Admin` ve `Areas/SuperAdmin` `_Layout.cshtml` — stylesheet bağlantıları, topbar (marka + güvenli çıkış formu), skip-link, `_IdleWarningDialog` partial + `idle-session.js`.
- **Yeni** `_ManagementAuthLayout.cshtml` (`Views/Shared`, Area fallback ile Admin/SuperAdmin'den de bulunur): login/access-denied gibi kimliksiz sayfalar için — topbar ve AFK script'i **yok**, böylece idle-session.js girişten önce hiç çalışmaz.

**Sayfalar**
- Account (customer): Login, Register, ForgotPassword, ResetPassword, Index (dashboard skeleton), AccessDenied — `account-card`/`account-form` deseniyle yeniden tasarlandı.
- Admin/SuperAdmin: Login, AccessDenied (yeni `_ManagementAuthLayout` ile), Home/Index dashboard (durum kartı grid'i + boş-durum deseni; `Statuses` boşken görünür).
- Home/Privacy, Views/Shared/Error.cshtml — container/surface deseniyle sadeleştirildi (İngilizce scaffold metni Türkçeleştirildi).

**AFK modalının base görsel componenti**
- `Views/Shared/_IdleWarningDialog.cshtml` (`[data-idle-warning-dialog]`, `[data-idle-remaining]`, `[data-idle-continue]` hook'ları), `wwwroot/css/components/idle-warning.css`.
- `wwwroot/js/admin/idle-session.js`: status/keep-alive/logout sözleşmesini uygular (status poll aktivite saymaz, keep-alive antiforgery header'ı gönderir, 401'de login'e yönlendirir); Admin Home/Index ve SuperAdmin Home/Index'teki foundation'ın geçici inline scaffold'ı (tekrarlı dialog/logout) kaldırılıp bu ortak component'e devredildi.

**ProductCard (base markup + style)** — ikinci dilim (`45b43b5`)
- `Views/Shared/_ProductCard.cshtml`: `ProductCardViewModel`'i frozen contract'taki tüm hook'larla render eder (`data-product-card`, `data-product-id`, `data-product-card-surface`, `data-product-card-image`, `data-product-card-price`, `data-add-to-cart-url`, `data-toggle-favorite-url`, `data-detail-url`); fiyat `tr-TR` kültürüyle biçimlendirilir; stokta yokken `disabled` attribute'u koşullu render edilir (Razor boolean-attribute davranışı doğrulandı).
- `wwwroot/css/components/product-card.css`: yalnız base görsel (yüzey, rozetler, favori butonu, fiyat satırı) — reveal/shimmer motion `product-card-motion.css`'te (Ajan 2, dokunulmadı) kalıyor.
- `typography.css`: Ajan 2'nin ürettiği `Mellos-Regular.woff2`'yi birincil kaynak yaptım (otf/ttf fallback olarak kaldı) — home-hero.css'teki geçici `@font-face` artık tamamen aynı önceliği kullanıyor, çakışma yok.
- **Not:** `Views/Home/Index.cshtml` (hero+featured-products) tamamen Ajan 2 sahipliğinde olduğu için dokunulmadı. Container zaten hazır (`data-product-card-list`); entegrasyon için tek satır yeterli: `@foreach (var p in Model.FeaturedProducts) { <partial name="_ProductCard" model="p" /> }`.

**Commerce view'ları** — üçüncü dilim (`2f53f36`, Coordinator merge sonrası)
- `Products/Index.cshtml` + `Categories/Detail.cshtml` (ikisi de `_ProductListing.cshtml` partial'ını paylaşır): filtre formu (koleksiyon/fiyat/kahve-çekirdek-kavurma-menşei/stok/indirim), sıralama, `_Pagination.cshtml`.
- `Products/Detail.cshtml` + `product-detail.js`: galeri, gramaj/varyant seçimi, adet stepper, sepete ekle, favori.
- `Campaigns/Index.cshtml`, `Favorites/Index.cshtml`, `Cart/Index.cshtml` + `cart.js`, `Checkout/Index.cshtml` + `checkout.js` (mock ödeme başlatma → callback → sipariş sayfasına yönlendirme).
- Checkout'un işlevsel olması için gereken minimum Account dilimi: `Addresses/Index.cshtml` (liste+ekleme formu) + `addresses.js`, `Orders/Index.cshtml`, `Orders/Detail.cshtml`.
- Paylaşılan JS: `js/core/commerce-api.js` (fetch+antiforgery+`CommerceMutationResponse`), `js/components/toast.js` + `toast.css`, `js/components/product-card-actions.js` (gerçek sepete-ekle/favori — Ajan 2'nin yalnızca motion olan `product-card-motion.js`'inden ayrı).
- İki gerçek hata düzeltildi: (1) sıralama `<select>`'i inline `onchange` ile submit ediyordu, mevcut CSP (`default-src 'self'`, unsafe-inline yok) bunu engelliyordu — görünür buton yapıldı; (2) `.empty-state` yalnız `admin.css`'te tanımlıydı, public sayfalarda stil almıyordu — `base.css`'e taşındı, ayrıca ortak `.status-badge` component'i eklendi.
- `ProductSummary` → `ProductCardViewModel` dönüşümü, `HomeController`'ın kullandığı aynı desenle (`Url.Action` tabanlı) her listeleme view'ında `@functions` bloğuyla tekrarlandı (Controller'a dokunmadan yapılabilecek tek yol).

**Kalan Account + tüm Admin commerce view'ları** — dördüncü dilim (`2988382`, Coordinator merge sonrası)
- Account: `Invoices/Index.cshtml` (liste+PDF indir), `Returns/Index.cshtml` (liste — oluşturma bloklu), `Reviews/Index.cshtml` (liste+sil), `Notifications/Index.cshtml` (liste+okundu işaretle/tümünü okundu işaretle).
- Admin (Ajan 4'ün `Areas/Admin/Controllers/CommerceControllers.cs`'teki 11 controller'ının tamamı): `Catalog` (kind bazlı lookup ekleme — `_LookupGroup.cshtml` partial'ı 6 kez reuse edilir), `Products` (liste + stok +/-), `Orders` (liste + durum geçiş formu), `Invoices` (liste+indir), `Shipments` (oluştur/takip et/iptal), `Campaigns`, `Coupons` (ikisi de oluşturma formu + liste), `Returns` (durum+restock kararı), `Reviews` (moderasyon), `Messages` (durum), `Reports` (satış özet kutuları + CSV export linki).
- `commerce-api.js`'e `postForm()` eklendi: Admin Orders/Returns/Reviews'ın `Status` action'ları `[FromQuery]` + `[FromForm]` karışımı bekliyor (JSON body kabul etmiyor) — `postCommerce`'in JSON gövdesi bunlarla çalışmıyordu.
- `base.css`'e genel `input/select/textarea` zemin stili eklendi — bu dilimdeki birçok form (admin formları, shipment/campaign/coupon oluşturma) `account-form`/`shop-filters` gibi özel context'lerin dışında düz kontrol kullanıyordu, aksi halde tarayıcı varsayılanıyla (beyaz zemin) koyu tema üzerinde bozuk görünürdü.
- **Contract request kapatıldı** (`90a7efd`): `OrderLineDetails.OrderItemId`, Contact GET/JSON POST sayfası ve sipariş detayındaki Return/Review oluşturma formları integration'a eklendi.
- İkinci geçici (commit edilmeyen) smoke test: 4 kalan Account sayfası + 11 Admin sayfası, giriş yapmış customer/admin olarak SQLite test host'unda 200 döndü.

## Değiştirilen dosyalar

`git show --stat 28dc5a9` (foundation dilimi) ve `git show --stat 45b43b5` (ProductCard dilimi) — toplam 35 dosya, tamamı `src/AETKAHVE.Web/**` altında. Detaylı liste commit mesajlarında ve repoda mevcuttur.

## Kullanılan skill/MCP

Bu ortamda frontend/UI-UX/accessibility/performance skill'i veya browser/screenshot/console MCP aracı bulunmuyor. `dotnet build`/`dotnet test` ile terminal tabanlı doğrulama yapıldı; gerçek tarayıcı görsel/etkileşim testi için elimde araç yok — bu dürüstçe bir sınırlama olarak belirtilir (aşağıda).

## Build ve test sonuçları

- `dotnet restore AETKAHVE.sln`: başarılı.
- `dotnet build AETKAHVE.sln --no-restore`: başarılı, 0 uyarı / 0 hata (her iki dilimde de, Ajan 2'nin çalışma dizinindeki commit'siz dosyalarıyla birlikte).
- `dotnet test AETKAHVE.sln --no-restore`: 8/8 unit, 16/16 integration başarılı (Ajan 3'ün foundation testleri; bu oturum yeni test eklemedi).
- HTTP smoke test (`dotnet run`, yerel curl): `/`, `/account/login`, `/account/register`, `/account/forgot-password`, `/admin/login`, `/superadmin/login` → 200; `/admin` (anonim) → 302; `/css/tokens.css`, `/css/admin/admin.css`, `/font/Mellos.otf`, `/js/admin/idle-session.js` → 200. `/account/login` içeriğinde `account-card`/`csrf-token`/`tokens.css`; `/` içeriğinde `data-frame-manifest-url`/`data-product-card-list` (hero hook'ları bozulmamış); `/admin/login` içeriğinde `admin-topbar` **yok** (doğru — kimliksiz sayfa).
- ProductCard doğrulaması: `Views/Home/Index.cshtml`'e **geçici olarak** (Ajan 2 sahipliğindeki dosyada, commit edilmeden) iki örnek `ProductCardViewModel` ile `_ProductCard` partial'ı bağlanıp `dotnet run` + curl ile gerçek render alındı — tüm data-* hook'ları, `tr-TR` fiyat biçimi ("349,90 ₺") ve stok durumuna göre koşullu `disabled` attribute'u doğru render edildi. Doğrulama sonrası dosya `git diff` ile bire bir Ajan 2'nin haline geri alındı (kalıntı yok, doğrulandı).

**2026-08-05 AFK istemci doğrulaması**
- `npm run test:frontend`: 5/5 başarılı (yerel expiry logout POST'u, ağ timeout'u, portal-scope cross-tab logout/expiry, başarılı keep-alive senkronizasyonu ve BroadcastChannel yokken storage fallback'i).
- `ManagementFrontendContractTests`: 3/3 başarılı (Admin/SuperAdmin layout hook'ları ile dağıtılan statik runtime sözleşmesi).
- `dotnet build AETKAHVE.sln --no-restore`: başarılı, 0 uyarı / 0 hata; UnitTests 23/23 başarılı.
- İlk tam integration koşusunda 46/47 başarılı; tek hata frontend dışındaki `Idle_timeout_deletes_the_management_authentication_cookie` test clock/cookie clock uyumsuzluğu olarak Ajan 3'e iletildi. Ajan 3 düzeltmesi integration'a alındıktan sonra tam suite yeniden koşulacaktır.

## Contract request

Yok. Shared ViewModel property'leri, route'lar ve frozen contract dosyaları değiştirilmedi. `DashboardSummaryViewModel.Statuses` şu an boş geliyor (controller'lar benim sahiplik alanım dışında) — mevcut boş-durum bileşeniyle karşılandı, ek contract gerekmedi.

**Admin ürün oluşturma + kampanya hedefleme** — beşinci dilim (`e922196`, Coordinator merge sonrası)
- Katalog lookup öğeleri artık ID'lerini tıkla-kopyala butonu olarak gösteriyor (`catalog.js`, `navigator.clipboard`) — Admin Products sayfasının kendi lookup verisi olmadığından (`ProductsController.Index` yalnız `PagedResult<ProductSummary>` döndürüyor), bu ID'yi formlara yapıştırma en pratik çözümdü.
- Admin Products sayfasına tam "yeni ürün ekle" formu eklendi (`AdminProductInput`'un tüm alanları; Koleksiyon zorunlu, diğer lookup'lar opsiyonel, hepsi yapıştırılan ID ile).
- Admin Campaigns formuna ürün/kategori hedefleme eklendi (virgül/satırla ayrılmış ID textarea'ları, client-side parse edilip `ProductIds`/`CategoryIds`'e bağlanıyor); boş bırakılırsa kampanya genel kapsamlı kalıyor (önceki davranış korunuyor).

**AFK client expiry + cross-tab hardening** — altıncı dilim (`fae14fd`, `80cd6db`)
- Ajan 3'ün `codex-architecture-security-20260804-afk-client-expiry.md` talebi backend route/JSON sözleşmesi değiştirilmeden karşılandı.
- Yerel sayaç sıfıra indiğinde `logoutUrl` adresine CSRF header ve `credentials: same-origin` ile yalnız bir POST gönderilir. Yanıt/401 sonrasında veya 3 saniyelik ağ timeout'unda login route'una `location.replace` ile geçilir.
- `BroadcastChannel`, hata/uyumsuzluk halinde `localStorage` fallback'i kullanır. Mesajlar version, portal (`admin|superadmin`), kaynak ve iki dakikalık freshness sınırıyla doğrulanır; bir portalın olayı diğer portalı kapatmaz.
- Açık logout ve expiry diğer aynı-portal sekmelerini gecikmeden kapatır; alıcı sekme logout POST'unu çoğaltmaz. Başarılı keep-alive yeni server deadline'ını paylaşır; status polling hâlâ aktivite sayılmaz ve sunucu kaydı nihai gerçektir.
- Native logout formu antiforgery otoritesi olarak korunur; ikinci submit engellenir. JS devre dışıysa mevcut server-side AFK enforcement değişmeden çalışır.

## Bilinen sorunlar

- **Gerçek tarayıcı/ekran görüntüsü doğrulaması yapılmadı** — bu ortamda SQL Server olmadığı için `dotnet run` gerçek veriyle başlatılamıyor; doğrulama SQLite tabanlı `WebApplicationFactory` testleri ve HTML içerik kontrolüyle yapıldı. Görsel/responsive/klavye-focus/renk kontrastı incelemesi kullanıcı veya browser MCP ile teyit edilmeli.
- **İki contract gap'i kapatıldı** (`90a7efd`): `/contact` ve OrderItemId tabanlı Return/Review oluşturma akışları çalışıyor.
- Admin ürün/kampanya formlarında lookup ID'leri gerçek bir `<select>` yerine kopyala-yapıştır ile giriliyor — kullanılabilir ama ideal değil; gerçek dropdown için Admin Products/Campaigns action'larının da `CatalogLookupSet` döndürmesi gerekir (küçük, contract request'e dahil edilmemiş bir iyileştirme fırsatı).
- Adres düzenleme (yalnız ekleme/silme var) ve checkout'ta adres CRUD'unun tam entegrasyonu (şu an checkout sayfası adres yoksa Addresses'e yönlendiriyor, aynı akışta ekleyip geri dönme yok) basitleştirildi.

## Son commit hash

AFK runtime commitleri: `fae14fd` (uygulama + Node davranış testleri), `80cd6db` (same-origin/double-submit guard + .NET contract testleri). Önceki son tasarım commit'i: `e922196`.

## Merge hazır durumu

Ajan 4'ün açtığı her route için view vardır; Admin ürün oluşturma ve kampanya hedefleme dahil önceki dilimler integration'a merge edildi. AFK istemci dilimi kendi `agent/claude-design-pages` branch'inde doğrulandı; Ajan 3 backend regresyon düzeltmesi integration'a girdikten sonra son kez senkronlanıp merge'e hazır teslim edilecektir.
