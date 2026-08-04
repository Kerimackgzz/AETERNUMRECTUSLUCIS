# Agent Board

| Ajan | Branch | Durum | Gate |
|---|---|---|---|
| Ajan 3 — Architecture/Security | `agent/codex-architecture-security` | Foundation integration’a merge edildi (`bcfbf8d`) | Tamamlandı; Ajan 3 sonrasında yalnızca kalan auth/security işlerine devam eder, devredilen dosyalara dokunmaz |
| Ajan 1 — Design/Pages | `agent/claude-design-pages` | Aktif | Foundation merge edildi, başladı |
| Ajan 2 — Hero/Motion | `agent/claude-home-hero` | Beklemeli (ön hazırlık dosya sisteminde tamam) | Foundation merge edildi; asset/motion kodu şu an proje köküne yanlış konumda (`wwwroot/`) — gerçek `src/AETKAHVE.Web/wwwroot/` altına taşınmalı ve branch’e commit edilmeli. Tercihen Ajan 1 layout/token merge’i sonrası |
| Ajan 4 — Commerce | `agent/codex-commerce` | Beklemeli | Foundation integration’a merge edildi; migration sahipliği devralınabilir |

Coordinator build/test doğrulaması (2026-08-04): `dotnet restore` / `dotnet build --no-restore` (0 uyarı, 0 hata) / `dotnet test --no-restore` (8/8 unit, 16/16 integration) başarılı; ardından `agent/codex-architecture-security` → `integration` merge (`bcfbf8d`, no-ff, çakışmasız).

Bu dosya foundation merge’inden sonra yalnız Coordinator tarafından güncellenir.
