# Agent Board

| Ajan | Branch | Durum | Gate |
|---|---|---|---|
| Ajan 3 — Architecture/Security | `agent/codex-architecture-security` | Foundation integration’a merge edildi (`bcfbf8d`) | Tamamlandı; Ajan 3 sonrasında yalnızca kalan auth/security işlerine devam eder, devredilen dosyalara dokunmaz |
| Ajan 1 — Design/Pages | `agent/claude-design-pages` | Integration'a merge edildi | Tamamlandı (design system + sayfalar + ProductCard); kalan kapsam (Public ürün/kategori/sepet sayfaları) Ajan 4'ün Controller/ViewModel'lerini bekliyor |
| Ajan 2 — Hero/Motion | `agent/claude-home-hero` | Integration'a merge edildi | Tamamlandı (frame pipeline + motion engine + Home/Index entegrasyonu) |
| Ajan 4 — Commerce | `agent/codex-commerce` | Beklemeli (worktree hazır, henüz commit yok) | Migration sahipliğini devralıp Product/Category/Cart/Order/Payment modülünü kurabilir; ProductCardViewModel/FeaturedProducts sözleşmesi hazır ve bekliyor |

Coordinator build/test doğrulaması (2026-08-04):
- `agent/codex-architecture-security` → `integration` merge (`bcfbf8d`, no-ff, çakışmasız); build/test 0 hata, 8/8+16/16.
- `agent/claude-design-pages` → `integration` merge (çakışmasız); build/test 0 hata, 8/8+16/16.
- `agent/claude-home-hero` → `integration` merge (çakışmasız); build/test 0 hata, 8/8+16/16.
- Coordinator entegrasyon düzeltmesi (`466d9ae`): `Views/Home/Index.cshtml`'deki `data-product-card-list` container'ı `_ProductCard` partial'ına bağlandı (orkestrasyon §19 "entegrasyon düzeltmeleri" adımı).
- HTTP smoke test: `/`, `/account/login`, `/admin/login` → 200; `/admin` (anonim) → 302; anasayfada `/admin`/`/superadmin` linki yok; hero (`data-home-hero`/`data-hero-pin`/`data-hero-canvas`), featured-products ve frame/font/js asset'leri (manifest.json, Mellos-Regular.woff2, home-frame-sequence.js) hepsi 200.

Bu dosya foundation merge’inden sonra yalnız Coordinator tarafından güncellenir.
