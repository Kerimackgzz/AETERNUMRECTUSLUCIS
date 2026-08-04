# Ajan 2 — Claude Home Hero ve Motion — Durum Raporu

## Branch / Repo durumu

- Branch: `agent/claude-home-hero` — worktree: `../aeternum-claude-hero`. `git config --global --add safe.directory` kullanıcı tarafından çalıştırıldıktan sonra "dubious ownership" engeli kalktı.
- Branch, ana checkout o sırada `agent/claude-design-pages` üzerinde ve **aktif** olduğu için (Ajan 1 paralel çalışıyordu — commit `50918f9` benim worktree kurulumum sırasında geldi), doğrudan `integration`'dan değil, Ajan 1'in o anki ucundan (`agent/claude-design-pages`) forklandı; böylece ana checkout'a hiç dokunulmadı/kesintiye uğratılmadı. Kendi dosyalarım ayrı worktree'ye taşındıktan sonra ana checkout tamamen temiz bırakıldı (`git status` boş).
- Son commit: `956d3c1` — "feat: add home hero frame-sequence engine and product-card reveal motion" (446 dosya). Worktree'de temiz bir `dotnet restore`+`build`+`test` ile tekrar doğrulandı (0 hata/uyarı, 8/8 + 16/16).
- **Güncelleme (aynı gün, ikinci tur):** `agent/claude-home-hero` bu sırada zaten `integration`'a merge edilmişti (`1d42fe4`); branch'im `git merge integration` ile güncel `integration` ucuna (`7c0568d`) senkronize edildi (fast-forward, çakışmasız). Bu, Ajan 1'in `_ProductCard.cshtml`'ini, Coordinator'ın `data-product-card-list`→partial bağlantısını, ve kullanıcının eklediği `navbar.css`/`navbar-motion.js` taslağı ile `HomePageViewModel.IsReducedMotionFallbackAvailable` varsayılan-değer düzeltmesini branch'ime taşıdı. Navbar harf-mask + page-transition eklendi, commit `94b5f10`.
- **Güncelleme (üçüncü tur):** `integration` bu sırada Ajan 4'ün commerce backend'ini (58 test) ve Ajan 1'in karşılık gelen commerce Razor view'larını (Products/Cart/Checkout/Favorites/Addresses/Orders) almıştı — branch'im tekrar `git merge integration` ile senkronize edildi (çakışmasız, "ort" stratejisiyle merge commit). Yeni eklenen `tests/AETKAHVE.IntegrationTests/CommerceContractTests.cs` içindeki `Public_home_renders_the_hero_and_navbar_motion_contract` testi (hero + navbar çıktımı birebir kilitleyen bir regresyon testi) dahil **tüm testler (21 unit + 39 integration) geçti** — navbar harf-mask eklemem mevcut sözleşmeyi bozmadı.
- **Ortam değişikliği (sonradan çözüldü)**: Commerce backend'in ilk merge anında SQL Server olmadığı için canlı HTTP smoke duruyordu. Ajan 4'ün Development SQLite runtime'ı (`f4af055`, integration `62e4de5`) bu engeli kaldırdı; `dotnet run` ve seeded localhost smoke artık çalışıyor.

## Yapılan iş (bu oturum)

### 1. Kritik mimari düzeltme

Önceki oturumda foundation yokken kurduğum tasarım (hero + ürün kartları **tek** sticky pin içinde, iki fazlı tek koreografi) **gerçek dondurulmuş sözleşmeyle uyuşmuyordu**. `docs/contracts/FRONTEND_BACKEND_CONTRACT.md` ve gerçek `Views/Home/Index.cshtml` incelendiğinde görüldü ki:

- Hero (`#home-hero`, `[data-home-hero]`) ve ürünler (`#featured-products`, `[data-featured-products]`) **iki ayrı `<section>`**, aynı pin içinde değil.
- Hero kendi pininde biter, serbest kalır; `featured-products` normal doküman akışında gelir.

Buna göre:
- `home-frame-sequence.js`'den reveal-fazı tamamen kaldırıldı — artık tek fazlı, sade scroll-scrub (eski/ilk tasarıma yakın).
- `product-card-motion.js` **IntersectionObserver tabanlı** "sahneye girince kartlar aşağıdan yukarı belirir" (`.is-revealed`, staggered) modeline çevrildi — artık hero'nun `herosequence:reveal` event'ine değil, `featured-products` section'ının viewport'a girişine tepki veriyor. Kullanıcının istediği "video bitince ürünler yukarı gelsin" davranışı hâlâ sağlanıyor, sadece tetikleyici mekanizma gerçek markup'a uyacak şekilde değişti.

### 2. Gerçek data-* sözleşmesine uyum

Önceki oturumda kendi varsaydığım hook isimleri (`data-hero-frame-sequence`, `data-manifest-url` vb.) ile gerçek frozen contract **farklı** çıktı. Kod gerçek isimlere göre güncellendi:

| Önceki varsayımım | Gerçek (frozen) sözleşme |
|---|---|
| `[data-hero-frame-sequence]` | `[data-home-hero]` (`#home-hero`) |
| `data-manifest-url` | `data-frame-manifest-url` (ViewModel: `HeroFrameManifestUrl`) |
| — | `data-poster-url` (ViewModel: `HeroPosterUrl`) — sayfa yüklenir yüklenmez, manifest beklenmeden hemen gösteriliyor (LCP) |
| client-only `prefers-reduced-motion` | + sunucu `data-reduced-motion` (ViewModel: `IsReducedMotionFallbackAvailable`) — ikisinin OR'u alınıyor |
| `.hero-frame-sequence__products` (hero içi) | `[data-featured-products]` (ayrı section, Ajan 3/1 tarafından zaten oluşturulmuş) |
| basit placeholder kart | `[data-product-card]`/`data-product-id`, `[data-product-card-image]`, `[data-product-card-surface]`, `[data-product-card-price]`, `[data-add-to-cart-url]`, `[data-toggle-favorite-url]`, `[data-detail-url]` — motion katmanı `[data-product-card]` + varsa `[data-product-card-surface]` (ışıltı) kullanıyor |

`Views/Home/Index.cshtml` (kendi sahiplik alanım — "hero ve featured-products alanı") güncellendi: hero section'ın içine `data-hero-pin`/`data-hero-canvas`/`.hero-frame-sequence__content` eklendi (canvas ve pin wrapper'ı önceden hiç yoktu, sadece `<h1>`/`<p>` vardı); `@section Styles` / `@section PageScripts` ile kendi CSS/JS dosyalarım bağlandı. `ViewModel`'in `HeroTitle`/`HeroSubtitle` alanları korunarak kullanıldı; "AETKAHVE" eyebrow metni ViewModel'de olmayan bir veri değil, sabit marka etiketi olarak eklendi.

### 3. Dosya taşıma

Önceki oturumda foundation yokken proje köküne (`AETERNUMRECTUSLUCIS/wwwroot/`) yazdığım tüm dosyalar, gerçek web projesinin içine taşındı:

| Eski (yanlış) konum | Yeni (doğru) konum |
|---|---|
| `wwwroot/frames/home/**` | `src/AETKAHVE.Web/wwwroot/frames/home/**` (145 kare × 3 breakpoint + poster'lar, `manifest.json` — sayı/klasör yapısı bozulmadı) |
| `wwwroot/js/pages/home-frame-sequence.js` | `src/AETKAHVE.Web/wwwroot/js/pages/home-frame-sequence.js` |
| `wwwroot/js/components/product-card-motion.js` | `src/AETKAHVE.Web/wwwroot/js/components/product-card-motion.js` |
| `wwwroot/css/pages/home-hero.css` | `src/AETKAHVE.Web/wwwroot/css/pages/home-hero.css` |
| `wwwroot/css/components/product-card-motion.css` | `src/AETKAHVE.Web/wwwroot/css/components/product-card-motion.css` |
| `wwwroot/fonts/Mellos-Regular.woff2` | `src/AETKAHVE.Web/wwwroot/font/Mellos-Regular.woff2` (dikkat: gerçek klasör **tekil** `font/`, Ajan 1'in zaten koyduğu `Mellos.otf`/`.ttf` ile birlikte) |

Kök `wwwroot/` klasörü (ve içindeki geçici `_preview-hero.html` QA dosyam) tamamen silindi — artık hiçbir dosya yanlış konumda değil.

### 4. Build / test / HTTP smoke doğrulaması

- `dotnet build AETKAHVE.sln --no-restore`: **0 hata, 0 uyarı**.
- `dotnet test AETKAHVE.sln --no-restore`: **8/8 unit, 16/16 integration** başarılı (Ajan 3'ün foundation testleri; bu oturum yeni test eklemedi).
- Gerçek `dotnet run` ile HTTP smoke test (`localhost:5299`):
  - `/` → 200; render edilen HTML'de `id="home-hero"`, `data-frame-manifest-url="/frames/home/manifest.json"`, `data-poster-url="..."`, `data-reduced-motion="true"`, `<canvas data-hero-canvas>`, `#featured-products`, `data-product-card-list data-product-count="0"` doğrulandı.
  - `/frames/home/manifest.json`, `/js/pages/home-frame-sequence.js`, `/js/components/product-card-motion.js`, `/css/pages/home-hero.css`, `/css/components/product-card-motion.css`, `/font/Mellos-Regular.woff2`, `/frames/home/desktop/frame-0000.webp`, `/frames/home/desktop/poster.webp` → hepsi 200.

### 5. Navbar harf-mask + page-transition runtime (kalan sorumluluklar)

`docs/agents/AGENT_BOARD.md` benim işimi "Tamamlandı" olarak işaretlemişti, ama görev dosyamda (`05`) hâlâ eksik iki sorumluluk vardı — kullanıcının "diğer ajanları kontrol et, işine başla" talimatı üzerine tamamlandı:

- **Navbar harf-mask animasyonu**: Kullanıcının eklediği basit `navbar.css`/`navbar-motion.js` (transparan→koyu scroll geçişi) **korunarak**, üzerine `[data-navbar-brand]` metnini runtime'da harflere bölüp (`.navbar-brand__letter-mask` > `.navbar-brand__letter`) staggered `translateY` reveal ile giriş animasyonu eklendi. JS çalışmazsa düz metin görünür kalır (progressive enhancement); `prefers-reduced-motion`'da animasyon iptal.
- **Page transition runtime**: `Views/Shared/_PageTransitionOverlay.cshtml` hook'u zaten vardı ama onu kullanan hiçbir kod yoktu. `wwwroot/css/core/page-transition.css` + `wwwroot/js/core/page-transition.js` eklendi: yalnızca aynı-origin `<a>` tıklamalarını yakalar (form submit/yeni sekme/modifier-key/download/hash-only/farklı-origin'e hiç dokunmaz), kısa bir opacity-fade overlay gösterip navigasyonu tamamlar; `pageshow` ile ilk yükleme ve bfcache geri dönüşünde overlay her zaman temiz başlar, `popstate`'e kasıtlı olarak dokunulmadı (back/forward akışı bozulmasın diye).
- **Layout bağlantısı**: Contract request Coordinator tarafından `c4a669d` ile uygulandı; `page-transition.js/css` `_PublicLayout.cshtml` içinde yükleniyor ve integration contract testi asset/tag/hook davranışını kilitliyor.

## Kullanılan skill/MCP

Kayıtlı bir frontend/motion/performance skill'i veya browser/screenshot MCP aracı yok; `dotnet build`/`test`/`run` + `curl` ile terminal tabanlı doğrulama yapıldı.

## Contract request

`docs/contracts/requests/claude-hero-20260804-page-transition-script-tag.md` uygulandı (`c4a669d`); `_PublicLayout.cshtml` hem CSS hem module script tag'ini içeriyor.

## Bilinen sınırlamalar

- Development SQLite runtime sayesinde birleşik integration üzerinde gerçek localhost HTTP/HTML ve asset smoke yapılabiliyor.
- `HeroPosterUrl`, shipped `/frames/home/desktop/poster.webp` asset'ine bağlandı ve 200 contract testiyle doğrulandı (`1de48f8`).
- `data-reduced-motion` artık `HomePageViewModel.IsReducedMotionFallbackAvailable` varsayılanı `false` olacak şekilde düzeltildi (`7c0568d`, kullanıcı tarafından) — hero artık varsayılan olarak canlı/scroll-scrub modda başlıyor, HTTP smoke testte doğrulandı (`data-reduced-motion="false"`).
- ProductCard hook'ları motion katmanıyla birebir uyumlu; Development seed'indeki `Eternal Light` kartı ve görseli localhost smoke'ta doğrulandı.
- Navbar harf-mask, transparan→koyu scroll davranışı ve page-transition runtime integration'da aktif; tag ve hook'lar contract testiyle korunuyor.

## Merge hazır durumu

Tamamlandı ve `6113dac` ile integration'a merge edildi; layout bağlantısı `c4a669d`, poster düzeltmesi `1de48f8` ile kapandı.
