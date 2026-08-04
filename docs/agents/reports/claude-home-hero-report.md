# Ajan 2 — Claude Home Hero ve Motion — Durum Raporu

## Branch / Repo durumu

- Branch: `agent/claude-home-hero` — worktree: `../aeternum-claude-hero`. `git config --global --add safe.directory` kullanıcı tarafından çalıştırıldıktan sonra "dubious ownership" engeli kalktı.
- Branch, ana checkout o sırada `agent/claude-design-pages` üzerinde ve **aktif** olduğu için (Ajan 1 paralel çalışıyordu — commit `50918f9` benim worktree kurulumum sırasında geldi), doğrudan `integration`'dan değil, Ajan 1'in o anki ucundan (`agent/claude-design-pages`) forklandı; böylece ana checkout'a hiç dokunulmadı/kesintiye uğratılmadı. Kendi dosyalarım ayrı worktree'ye taşındıktan sonra ana checkout tamamen temiz bırakıldı (`git status` boş).
- Son commit: `956d3c1` — "feat: add home hero frame-sequence engine and product-card reveal motion" (446 dosya). Worktree'de temiz bir `dotnet restore`+`build`+`test` ile tekrar doğrulandı (0 hata/uyarı, 8/8 + 16/16).
- **Güncelleme (aynı gün, ikinci tur):** `agent/claude-home-hero` bu sırada zaten `integration`'a merge edilmişti (`1d42fe4`); branch'im `git merge integration` ile güncel `integration` ucuna (`7c0568d`) senkronize edildi (fast-forward, çakışmasız). Bu, Ajan 1'in `_ProductCard.cshtml`'ini, Coordinator'ın `data-product-card-list`→partial bağlantısını, ve kullanıcının eklediği `navbar.css`/`navbar-motion.js` taslağı ile `HomePageViewModel.IsReducedMotionFallbackAvailable` varsayılan-değer düzeltmesini branch'ime taşıdı.

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
- **Layout bağlantısı benim yapamadığım kısım**: `page-transition.js/css`'in `_PublicLayout.cshtml`'e bağlanması (script/link tag'i) layout sahibinin işi — `docs/contracts/requests/claude-hero-20260804-page-transition-script-tag.md` ile contract request bırakıldı (navbar.css/navbar-motion.js için `7c0568d`'de yapılanın aynısı istendi).

## Kullanılan skill/MCP

Kayıtlı bir frontend/motion/performance skill'i veya browser/screenshot MCP aracı yok; `dotnet build`/`test`/`run` + `curl` ile terminal tabanlı doğrulama yapıldı.

## Contract request

`docs/contracts/requests/claude-hero-20260804-page-transition-script-tag.md` — `_PublicLayout.cshtml`'e `page-transition.css/js` için link/script tag'i eklenmesi rica edildi (layout dosyasına ben dokunamıyorum). Frozen ViewModel/contract dosyalarına dokunulmadı.

## Bilinen sınırlamalar

- **Gerçek tarayıcı/görsel doğrulama yapılamadı** — yalnızca HTTP durum kodu + HTML içerik kontrolü yapıldı (Ajan 1'in raporundaki dürüst sınırlamayla aynı durum).
- `Model.HeroPosterUrl` şu an `/images/home/hero-poster.webp` değerini taşıyor ama bu dosya **yok** (404) — muhtemelen backend/seed verisi henüz gerçek içerik sağlamıyor (Ajan 4'ün alanı). JS bunu sessizce (catch) tolere ediyor, sayfa kırılmıyor; breakpoint manifest posterleri (`/frames/home/{bp}/poster.webp`) zaten mevcut ve devreye giriyor.
- `data-reduced-motion` artık `HomePageViewModel.IsReducedMotionFallbackAvailable` varsayılanı `false` olacak şekilde düzeltildi (`7c0568d`, kullanıcı tarafından) — hero artık varsayılan olarak canlı/scroll-scrub modda başlıyor, HTTP smoke testte doğrulandı (`data-reduced-motion="false"`).
- Ajan 1'in `45b43b5`/`50918f9` commit'leriyle eklediği gerçek `_ProductCard.cshtml` incelendi: tüm hook'lar (`data-product-card`, `data-product-card-surface`, vb.) benim `product-card-motion.js/css`'imin beklediğiyle **birebir uyuşuyor**. `data-product-count="0"` hâlâ boş (commerce/seed verisi yok, Ajan 4'ün alanı) — gerçek ürün verisiyle tarayıcıda görsel doğrulama hâlâ yapılamadı.
- Navbar harf-mask ve page-transition runtime bu turda yazıldı (bkz. yukarı); page-transition'ın layout'a bağlanması bir contract request'e bağlı, o adım tamamlanana kadar `page-transition.js/css` sayfada aktif olmayacak (dosyalar mevcut ve 200 dönüyor, ama hiçbir `<script>`/`<link>` onları henüz çağırmıyor).

## Merge hazır durumu

Evet. Build/test/HTTP smoke doğrulamasından geçti, `agent/claude-home-hero` branch'i `integration` ile senkron, sahiplik dışı dosya değişikliği yok, ana checkout'a müdahale edilmedi. Tek açık nokta: page-transition contract request'inin layout sahibi tarafından uygulanması.
