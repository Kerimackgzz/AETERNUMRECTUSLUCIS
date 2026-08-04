# Project Status

Son güncelleme: 2026-08-04
Foundation (`bcfbf8d`), Ajan 1 design/pages+ProductCard ve Ajan 2 home hero/motion `integration`'a merge edildi (`466d9ae`). Sırada: Ajan 4 commerce.

## Tamamlandı

- .NET 10 katmanlı solution ve iki test projesi.
- SQL Server EF Core/Identity modeli ve `InitialIdentity` migration/snapshot.
- Customer/Admin/SuperAdmin ayrık cookie scheme, route ve policy’leri.
- Customer kayıt, doğrulama, login/logout, forgot/reset password.
- Yönetim lockout, IP-bölümlü rate limit, güvenli hata ve audit kayıtları.
- Remember Me session/persistent cookie davranışı.
- Veritabanı tabanlı Admin/SuperAdmin AFK session, status, keep-alive ve revoke.
- Global exception, correlation ID, security header, management `no-store` ve health skeleton.
- Shared ViewModel, Razor layout ve frontend/backend hook sözleşmeleri.
- Design token sistemi, Mellos font, Public/Account/Admin/SuperAdmin sayfa tasarımları, AFK modal base component'i (Ajan 1).
- Home hero frame-sequence motoru, product-card reveal/shimmer motion, frame asset pipeline (Ajan 2).
- ProductCard base markup + motion entegrasyonu; `data-product-card-list` gerçek partial'a bağlandı (Coordinator).
- 8 unit ve 16 integration test (foundation değişmedi, hepsi geçiyor).

## Sonraki Aşamalar

- [x] Coordinator foundation branch'ini `integration` üzerine merge etti (`bcfbf8d`).
- [x] Ajan 1 ve Ajan 2 `integration`'a merge edildi (`466d9ae`'ye kadar); build/test/HTTP smoke doğrulandı.
- `AppDbContext`, migration klasörü ve snapshot Ajan 4'e devredilir; Ajan 4 commerce modülünü ve `AddCommerceModule` içini tamamlar.
- Ajan 4'ün Controller/ViewModel'leri gelince `HomeController` gerçek `FeaturedProducts` verisiyle dolduracak — ProductCard zaten hazır, ek frontend değişikliği gerekmez.
- Production SMTP/outbox ve kalıcı Data Protection key store eklenir.

## Bilinen Sınırlamalar

- Gerçek SMTP bilgisi olmadığı için Identity mesajları in-memory mock sender’a gider.
- SQL Server instance’ı bu ortamda çalıştırılmadı; migration üretimi/script/model kontrolleri SQL Server sağlayıcısıyla yapılır, çalışan integration testleri SQLite kullanır.
- AFK uyarı modalının istemci runtime’ı frontend sahibine bırakılmıştır; timeout sunucuda zorunlu olarak uygulanır (uygulandı: `js/admin/idle-session.js`).
- `Model.FeaturedProducts` şu an boş (commerce verisi yok) — anasayfada henüz gerçek ürün kartı görünmüyor, bu beklenen bir durum.
- Gerçek tarayıcı/ekran görüntüsü doğrulaması (scroll/reduced-motion/responsive görsel inceleme) yapılmadı; yalnızca HTTP/HTML doğrulaması yapıldı.
