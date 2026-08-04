# Agent Board

| Ajan | Branch | Durum | Gate |
|---|---|---|---|
| Ajan 3 — Architecture/Security | `agent/codex-architecture-security` | Foundation integration’a merge edildi (`bcfbf8d`) | Tamamlandı; Ajan 3 sonrasında yalnızca kalan auth/security işlerine devam eder, devredilen dosyalara dokunmaz |
| Ajan 1 — Design/Pages | `agent/claude-design-pages` | Integration'a merge edildi | Tamamlandı (design system + sayfalar + ProductCard); kalan kapsam (Public ürün/kategori/sepet sayfaları) Ajan 4'ün Controller/ViewModel'lerini bekliyor |
| Ajan 2 — Hero/Motion | `agent/claude-home-hero` | Integration'a merge edildi | Tamamlandı (frame pipeline + motion engine + Home/Index entegrasyonu) |
| Ajan 4 — Commerce | `agent/codex-commerce` | Integration'a merge edildi | Tamamlandı (domain/service/controller/migration katmanı, 58/58 test); **bilinçli olarak Razor view'ları yok** — bu artık Ajan 1'in sırada bekleyen işi |

Coordinator build/test doğrulaması (2026-08-04):
- `agent/codex-architecture-security` → `integration` merge (`bcfbf8d`, no-ff, çakışmasız); build/test 0 hata, 8/8+16/16.
- `agent/claude-design-pages` → `integration` merge (çakışmasız); build/test 0 hata, 8/8+16/16.
- `agent/claude-home-hero` → `integration` merge (çakışmasız); build/test 0 hata, 8/8+16/16.
- Coordinator entegrasyon düzeltmesi (`466d9ae`): `Views/Home/Index.cshtml`'deki `data-product-card-list` container'ı `_ProductCard` partial'ına bağlandı (orkestrasyon §19 "entegrasyon düzeltmeleri" adımı).
- HTTP smoke test: `/`, `/account/login`, `/admin/login` → 200; `/admin` (anonim) → 302; anasayfada `/admin`/`/superadmin` linki yok; hero (`data-home-hero`/`data-hero-pin`/`data-hero-canvas`), featured-products ve frame/font/js asset'leri (manifest.json, Mellos-Regular.woff2, home-frame-sequence.js) hepsi 200.
- Coordinator bug düzeltmesi (`7c0568d`): `HomePageViewModel.IsReducedMotionFallbackAvailable` varsayılanı `true` olduğu için scroll animasyonu hiç tetiklenmiyordu (her zaman static fallback), `false` yapıldı; ayrıca `navbar.css`/`navbar-motion.js` hiç yoktu, eklendi.
- `agent/codex-commerce` → `integration` merge (çakışmasız); kaynak worktree'de build/test 0 hata, 21/21+37/37=58/58; merge sonrası integration'da build/test tekrar 0 hata, 21/21+37/37.
- **Ortam sınırlaması**: bu ortamda çalışan bir SQL Server instance'ı yok. Commerce merge'i sonrası `HomeController` gerçek DB sorgusu yaptığı için `dotnet run` (Development, gerçek SQL Server provider'ı) DB bağlantı hatasıyla başlamıyor; `NotificationDeliveryWorker` de aynı sebeple host'u düşürüyor. `dotnet test` etkilenmiyor (SQLite fixture kullanıyor). Gerçek tarayıcı doğrulaması için ya yerel/konteyner SQL Server ya da Development için SQLite'a geçici geçiş gerekir — bu bir contract/Program.cs kararı, Coordinator'ın kullanıcıyla netleştirmesi gerekiyor.

Bu dosya foundation merge’inden sonra yalnız Coordinator tarafından güncellenir.
