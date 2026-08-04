# Project Status

Son güncelleme: 2026-08-04
Aktif foundation branch’i: `agent/codex-architecture-security`

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
- 8 unit ve 16 integration test.

## Sonraki Aşamalar

- Coordinator foundation branch’ini `integration` üzerine merge eder.
- Merge sonrasında `AppDbContext`, migration klasörü ve snapshot Ajan 4’e devredilir.
- Ajan 4 commerce modülünü ve `AddCommerceModule` içini tamamlar.
- Ajan 1/2 dondurulmuş ViewModel ve hook sözleşmelerini kullanarak frontend’i geliştirir.
- Production SMTP/outbox ve kalıcı Data Protection key store eklenir.

## Bilinen Sınırlamalar

- Gerçek SMTP bilgisi olmadığı için Identity mesajları in-memory mock sender’a gider.
- SQL Server instance’ı bu ortamda çalıştırılmadı; migration üretimi/script/model kontrolleri SQL Server sağlayıcısıyla yapılır, çalışan integration testleri SQLite kullanır.
- AFK uyarı modalının istemci runtime’ı frontend sahibine bırakılmıştır; timeout sunucuda zorunlu olarak uygulanır.
- Foundation sırasında Ajan 2’ye ait kök `wwwroot/*` ve ayrı rapor dosyaları dışarıdan oluştu; Ajan 3 bunlara dokunmadı ve commit’lerine dahil etmedi.
