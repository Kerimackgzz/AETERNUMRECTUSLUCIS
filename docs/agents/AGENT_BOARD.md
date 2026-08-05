# Agent Board

| Ajan | Branch | Durum | Gate |
|---|---|---|---|
| Ajan 3 — Architecture/Security | `agent/codex-architecture-security` | Foundation ve auth-clock düzeltmesi integration’a merge edildi (`bcfbf8d`, `211127a`) | Tamamlandı; tüm cookie handler'ları uygulama `TimeProvider`'ını kullanıyor, tarih bağımlı AFK regresyonu kapandı |
| Ajan 1 — Design/Pages | `agent/claude-design-pages` | Integration'a merge edildi — **kapsam tamamlandı** | Design system, ProductCard ve Public/Account/Admin commerce view'ları tamamlandı; `/contact`, koleksiyonlar, hikâye ve post-purchase iade/yorum bağlantıları Coordinator tarafından kapatıldı (`90a7efd`) |
| Ajan 2 — Hero/Motion + Frontend QA | `agent/claude-home-hero`, `agent/frontend-qa` | Integration'a merge edildi (`ec20497`) | Tamamlandı: frame pipeline, motion engine, şeffaf/scroll navbar, mobil disclosure, fetch auth redirect ve accessibility hardening |
| Ajan 4 — Commerce | `agent/codex-commerce`, `agent/commerce-hardening` | Integration'a merge edildi (`62e4de5`, `18d5d99`) | Tamamlandı: çekirdek, migration, Development SQLite, sahiplik/idempotency ve edge-case hardening (`7cabb93`) |
| Ortak — Production readiness | `agent/prod-readiness` | Integration'a merge edildi (`49d0fc8`) | Kalıcı key-ring, SMTP/Identity outbox ve worker hardening tamam; gerçek payment/shipping adapter'ları gelene kadar Production fail-closed |

Coordinator build/test doğrulaması (2026-08-04):
- `agent/codex-architecture-security` → `integration` merge (`bcfbf8d`, no-ff, çakışmasız); build/test 0 hata, 8/8+16/16.
- `agent/claude-design-pages` → `integration` merge (çakışmasız); build/test 0 hata, 8/8+16/16.
- `agent/claude-home-hero` → `integration` merge (çakışmasız); build/test 0 hata, 8/8+16/16.
- Coordinator entegrasyon düzeltmesi (`466d9ae`): `Views/Home/Index.cshtml`'deki `data-product-card-list` container'ı `_ProductCard` partial'ına bağlandı (orkestrasyon §19 "entegrasyon düzeltmeleri" adımı).
- HTTP smoke test: `/`, `/account/login`, `/admin/login` → 200; `/admin` (anonim) → 302; anasayfada `/admin`/`/superadmin` linki yok; hero (`data-home-hero`/`data-hero-pin`/`data-hero-canvas`), featured-products ve frame/font/js asset'leri (manifest.json, Mellos-Regular.woff2, home-frame-sequence.js) hepsi 200.
- Coordinator bug düzeltmesi (`7c0568d`): `HomePageViewModel.IsReducedMotionFallbackAvailable` varsayılanı `true` olduğu için scroll animasyonu hiç tetiklenmiyordu (her zaman static fallback), `false` yapıldı; ayrıca `navbar.css`/`navbar-motion.js` hiç yoktu, eklendi.
- `agent/codex-commerce` → `integration` merge (çakışmasız); kaynak worktree'de build/test 0 hata, 21/21+37/37=58/58; merge sonrası integration'da build/test tekrar 0 hata, 21/21+37/37.
- **Development runtime çözümü**: Production/design-time SQL Server akışı korunurken Development configuration SQLite kullanıyor; deterministik seed ile `dotnet run` ve localhost smoke çalışıyor (`f4af055`, integration merge `62e4de5`).
- **Düzeltilen branch olayı**: Ajan 3 için hiç ayrı worktree açılmamıştı; Ajan 3'ün oturumu bu kök dizinde canlı çalışırken Coordinator'ın Ajan 4 merge'i checkout yarışı yüzünden yanlışlıkla `agent/codex-architecture-security` üzerine düştü (`6b2b47d`, `fcd47df`). Ajan 3 kendi işini (`c381098` — invalid auth cookie sertleştirmesi, 2 yeni integration test) commit ettikten sonra, `integration` o branch'e `--ff-only` ile fast-forward edildi (kayıpsız, veri bütünlüğü korunarak). Bundan sonra Ajan 3 için de `../aeternum-codex-architecture-security` worktree'si açıldı — aynı yarış bir daha olmamalı.
- Ajan 4'ün merge'i sonrası Ajan 1, commerce Controller'larının karşılığı olan Razor view'ları ekledi (`2f53f36` → `integration`'da merge commit). Bulunan iki ek hata: (1) sıralama `<select>`'i inline `onchange` ile otomatik submit etmeye çalışıyordu — mevcut sıkı CSP (`default-src 'self'`, `unsafe-inline` yok) bunu sessizce engeller, görünür bir "Uygula" butonuna çevrildi; (2) `.empty-state` yalnız `admin.css`'te tanımlıydı, public sayfalarda hiç stil almıyordu — `base.css`'e taşındı.
- Son Coordinator turu: Ajan 2 navbar/page-transition teslimi `6113dac`, Ajan 4 güncel commerce HEAD'i `62e4de5` ile integration'a alındı; page-transition layout bağlantısı `c4a669d`, public navigation + contact + post-purchase akışları `90a7efd`, gerçek hero poster bağlantısı `1de48f8` ile tamamlandı.

Coordinator final hardening doğrulaması (2026-08-05):

- Security/test-clock fix `60cddc2`, commerce hardening `7cabb93`, production-readiness `8432baa` + `91b614a` ve frontend QA `98c17ef` sırasıyla `211127a`, `18d5d99`, `49d0fc8`, `ec20497` merge commit'leriyle alındı.
- `dotnet restore` ve `dotnet build --no-restore`: başarılı, 0 uyarı / 0 hata. Testler: 33/33 unit + 60/60 integration = 93/93.
- EF: dört migration doğru sırada, pending model change yok, 54.628 bayt SQL Server idempotent script üretildi. NuGet vulnerability audit temiz; 26 JavaScript dosyasının syntax kontrolü geçti.
- Gerçek Chrome: desktop top navbar şeffaf (`::before opacity 0`, yükseklik 81.39 px); 1000 px scroll'da `is-scrolled`, opacity 1 ve 65.39 px. 390×844 mobilde kapalı navbar 69 px, 390/390 taşmasız; 6 linkli menü, body lock, Escape ve focus restore geçti; console/network hatası yok.
- Localhost: keşfedilen 26 public HTML/asset isteği 200. `/account/orders`, `/favorites`, `/checkout`, `/admin`, `/superadmin` doğru login route'larına 302.
- Production startup iki ayrı prosesle doğrulandı: varsayılan config notification mock guard'ında; güvenli SMTP/notification override sonrası payment ve shipping mock guard'larında fail-closed. Listener ve dış SMTP bağlantısı oluşmadı.
- Bilinen EF uyarısı `Product` query filter + required `StockMovement.Product` navigation'ı içindir. Doğrudan audit/idempotency sorguları görünür kalır; soft-delete ürün hareketlerini navigation join'li tarihsel raporlarda korumak için `IgnoreQueryFilters()` veya optional/history ilişki tasarımı takip edilmelidir.

Bu dosya foundation merge’inden sonra yalnız Coordinator tarafından güncellenir.
