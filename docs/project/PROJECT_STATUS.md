# Project Status

Son güncelleme: 2026-08-04
Foundation, Ajan 1 (design system + Public/Account/Admin commerce view'ları), Ajan 2 (home hero/navbar/motion) ve Ajan 4 (commerce backend + Development SQLite) `integration`'a merge edildi. Public navbar hedefleri, contact ve post-purchase iade/yorum akışları da tamamlandı. Birleşik doğrulama kapısı 23 unit + 44 integration = 67/67.

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
- Commerce backend: katalog/sepet/checkout/sipariş/ödeme/iade/fatura/stok/engagement servisleri, migration'lar, Product/Cart/Checkout/Favorites/Notifications/Admin-commerce controller'ları (Ajan 4).
- Commerce Razor view'ları — tamamı: Products (liste+filtre+detay), Categories, Campaigns, Cart, Checkout, Favorites, Account'un tamamı (Addresses/Orders/Invoices/Returns/Reviews/Notifications), 11 Admin commerce sayfası (Catalog/Products/Orders/Invoices/Shipments/Campaigns/Coupons/Returns/Reviews/Messages/Reports) (Ajan 1).
- 23 unit ve 44 integration test, hepsi geçiyor.

## Sonraki Aşamalar

- [x] Coordinator foundation branch'ini `integration` üzerine merge etti (`bcfbf8d`).
- [x] Ajan 1, Ajan 2 ve Ajan 4 `integration`'a merge edildi; her merge sonrası build/test doğrulandı.
- [x] Ajan 1, Ajan 4'ün açtığı **her** route için view ekledi — Public shop akışı, Account'un tamamı, tüm Admin commerce sayfaları.
- **Kapatıldı (`90a7efd`)**: `/contact` GET/JSON POST, `OrderLineDetails.OrderItemId` ve sipariş detayındaki Return/Review oluşturma akışları integration'da.
- Production SMTP/outbox ve kalıcı Data Protection key store eklenir.
- [x] Development SQLite + deterministik seed ile `dotnet run` ve localhost HTTP/HTML/asset smoke tamamlandı.

## Bilinen Sınırlamalar

- Gerçek SMTP bilgisi olmadığı için Identity mesajları in-memory mock sender’a gider.
- SQL Server instance’ı bu ortamda çalıştırılmadı; production migration üretimi/script/model kontrolleri SQL Server sağlayıcısıyla yapılır. Development runtime ve integration testleri SQLite kullanır; localhost uygulaması çalışır.
- AFK uyarı modalının istemci runtime’ı frontend sahibine bırakılmıştır; timeout sunucuda zorunlu olarak uygulanır (uygulandı: `js/admin/idle-session.js`).
- `Model.FeaturedProducts` gerçek katalog verisiyle dolduruluyor; Development seed'indeki `Eternal Light` kartı localhost smoke'ta görünür.
- Gerçek tarayıcı/ekran görüntüsü doğrulaması (scroll/reduced-motion/responsive görsel inceleme) yapılmadı; yalnızca HTTP/HTML/Razor render doğrulaması yapıldı.
