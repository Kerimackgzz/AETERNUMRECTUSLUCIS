# Ajan 4 — Customer Account Center Report

Tarih: 2026-08-07
Branch: `integration` (kullanıcı talebiyle doğrudan çalışma ağacı)
Uygulama commit'i: `3b4db03` (`feat(account): build customer account center`)
Merge durumu: Değişiklikler doğrudan güncel `integration` üzerinde commitlendi; ayrıca merge beklemiyor.

## Teslim

- `/account` placeholder'ı gerçek, server-side projected müşteri dashboard'u ile değiştirildi.
- Profil, sayaçlar, üç satırlık fiyatlandırılmış sepet özeti, son sipariş ve mevcut hesap sayfalarına quick-link alanları eklendi.
- Auth ekranlarının `_AccountLayout` yapısı korundu; authenticated sayfalar `_CustomerAccountLayout` altında sidebar/mobil disclosure ile birleştirildi. Guest destekli `/cart` public layout'ta kaldı.
- Profil temel bilgileri, private profil fotoğrafı, doğrulamalı e-posta değişikliği ve Identity policy tabanlı parola değişikliği eklendi.
- E-posta/parola değişikliklerinde security stamp yenilenerek mevcut oturum dahil tüm customer cookie'leri geçersiz kılındı.
- E-posta değişikliği confirmation ve eski-adres güvenlik bildirimi korumalı SQL outbox'a yazıldı; token response/log içeriğine çıkarılmadı.

## Persistence ve storage

- `ApplicationUser.ProfileImageStorageKey` nullable, azami 512 karakter.
- Migration: `AddCustomerAccountProfileImage`.
- Contract request: `docs/contracts/requests/codex-commerce-20260807-customer-account-profile-image.md`.
- Fotoğraflar public static dosya değildir; owner-only endpoint üzerinden `private, no-store` ile açılır.
- JPEG/PNG/WebP, MIME/uzantı/magic-byte, random key, safe-path ve hesap katmanında 2 MiB sınırı uygulanır.
- Production private upload kökü için kalıcı/paylaşılan volume gereksinimi deployment runbook'una eklendi.

## Tasarım ve erişilebilirlik

- Mevcut koyu zemin, orman yeşili yüzey, Mellos display font, kırık beyaz metin ve altın hairline tokenları kullanıldı.
- Parlak kart, glassmorphism, renkli admin-dashboard blokları veya pill ağırlıklı dil eklenmedi.
- Mobil menü `aria-expanded`, Escape, dış tıklama, focus restore, `inert` ve body scroll lock uygular.
- Hareket 200–360 ms tokenlarıyla sınırlı ve reduced-motion fallback'i vardır.

## Doğrulama

- `dotnet build AETKAHVE.sln --no-restore`: 0 uyarı, 0 hata.
- Unit: 75/75; integration: 88/88 (5 yeni hesap merkezi regresyonu dahil); frontend: 9/9.
- EF migration listesi doğru sırada; pending model change yok.
- SQL Server idempotent script: 55.690 karakter, yeni migration ve `__EFMigrationsHistory` guard'ı mevcut.
- Eski Development SQLite dosyasının geçici kopyasıyla startup schema upgrade ve dashboard smoke doğrulandı; gerçek kullanıcı veritabanı test sırasında değiştirilmedi.
- Gerçek Chrome: 1440×1000 desktop ve 390×844 mobil; yatay taşma yok, disclosure/open-close/Escape-inert/body-lock/focus restore ve reduced-motion doğru, console/network hatası yok.
- Görsel inceleme: dashboard mevcut navbar, Mellos tipografi, koyu/orman yeşili yüzey ve altın hairline sistemini koruyor; admin template görünümü oluşturmuyor.
