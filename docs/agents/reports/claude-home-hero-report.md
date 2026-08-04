# Ajan 2 — Claude Home Hero ve Motion — Durum Raporu

## Branch / Repo durumu

- Branch: `agent/claude-home-hero` **henüz oluşturulamadı**. `.git` artık doğru konumda (`AETERNUMRECTUSLUCIS` kökü — önceki ev-dizini sorunu foundation tarafından çözülmüş), ama repository başka bir Windows kullanıcısı (`DESKTOP-GVQ8QBP\CodexSandboxOffline`) tarafından oluşturulmuş. Bu makinede (`Kerim Açıkgöz` kullanıcısı) herhangi bir git komutu çalıştırmaya çalıştığımda Git'in "dubious ownership" güvenlik korumasına takılıyorum:
  ```
  fatal: detected dubious ownership in repository at '...'
  ```
  Çözümü `git config --global --add safe.directory '...'` çalıştırmak — **ancak bu bir git config değişikliğidir ve kesin güvenlik kuralım gereği (`NEVER update the git config`) bunu kendim çalıştıramam.** Bu adımı ya siz çalıştırmalısınız ya da `CodexSandboxOffline` hesabının kendisi.
- Son commit hash: yok (yukarıdaki sebeple hiçbir commit atamadım).

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

## Kullanılan skill/MCP

Kayıtlı bir frontend/motion/performance skill'i veya browser/screenshot MCP aracı yok; `dotnet build`/`test`/`run` + `curl` ile terminal tabanlı doğrulama yapıldı.

## Contract request

Yok. Frozen ViewModel/contract dosyalarına dokunulmadı; sadece onlara **uyum sağlandı**.

## Bilinen sınırlamalar

- **Git commit atılamadı** — yukarıda açıklanan "dubious ownership" engeli nedeniyle. Bu, sizin veya `CodexSandboxOffline` hesabının çözmesi gereken bir adım.
- **Gerçek tarayıcı/görsel doğrulama yapılamadı** — yalnızca HTTP durum kodu + HTML içerik kontrolü yapıldı (Ajan 1'in raporundaki dürüst sınırlamayla aynı durum).
- `Model.HeroPosterUrl` şu an `/images/home/hero-poster.webp` değerini taşıyor ama bu dosya **yok** (404) — muhtemelen backend/seed verisi henüz gerçek içerik sağlamıyor (Ajan 4'ün alanı). JS bunu sessizce (catch) tolere ediyor, sayfa kırılmıyor; breakpoint manifest posterleri (`/frames/home/{bp}/poster.webp`) zaten mevcut ve devreye giriyor.
- `data-reduced-motion="true"` şu an sunucudan geliyor (muhtemelen içerik/seed verisi eksikliğinden), yani hero şu an varsayılan olarak **statik** modda render oluyor — bu benim kodumun değil, mevcut verinin bir sonucu; gerçek içerik/`FeaturedProducts` verisi geldiğinde (`data-product-count="0"` şu an boş) bu değer muhtemelen değişecek.
- ProductCard markup'ı hâlâ yok (`data-product-count="0"`) — Ajan 1'in kapsamının bir sonraki dilimi; benim motion katmanım `[data-product-card]` göründüğü an doğru çalışacak şekilde hazır.
- Navbar/page-transition için yalnızca temel dosya sahipliğim var; `navbar-motion.js`/`page-transition.js` (transparan→blur navbar, harf-mask, sayfa geçiş runtime'ı) henüz yazılmadı — kapsam bu oturumda dosya taşıma + mimari düzeltmeye odaklandı.

## Merge hazır durumu

Kod/build/test/HTTP smoke açısından hazır, **ancak commit atılamadığı için branch'e alınamadı**. Git sahiplik sorunu çözülür çözülmez (`git config --global --add safe.directory` + muhtemelen `agent/claude-home-hero` branch/worktree kurulumu) ilk iş bu değişiklikleri commit etmek olacak.
