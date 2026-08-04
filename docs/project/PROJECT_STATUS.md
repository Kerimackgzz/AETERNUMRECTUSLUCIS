# Project Status

Son güncelleme: 2026-08-04
Foundation, Ajan 1 (design system + commerce view'ları), Ajan 2 (home hero/motion) ve Ajan 4 (commerce backend) `integration`'a merge edildi. Sırada: Admin commerce sayfaları + kalan Account sayfaları (Ajan 1) ve gerçek SQL Server ortamı.

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
- Commerce Razor view'ları: Products (liste+filtre+detay), Categories, Campaigns, Cart, Checkout, Favorites, Account Addresses/Orders (liste+detay) (Ajan 1).
- 21 unit ve 39 integration test, hepsi geçiyor.

## Sonraki Aşamalar

- [x] Coordinator foundation branch'ini `integration` üzerine merge etti (`bcfbf8d`).
- [x] Ajan 1, Ajan 2 ve Ajan 4 `integration`'a merge edildi; her merge sonrası build/test doğrulandı.
- [x] Ajan 1, Ajan 4'ün commerce Controller/ViewModel'lerine karşılık gelen temel alışveriş akışı view'larını ekledi (ürün listesi/detay, sepet, ödeme, favoriler, adresler, siparişler).
- **Kalan (Ajan 1)**: Account invoices/returns/reviews/notifications sayfaları, Contact formu, tüm Admin commerce sayfaları (products/catalog/orders/shipments/invoices/returns/campaigns/coupons/reviews/messages/reports).
- Production SMTP/outbox ve kalıcı Data Protection key store eklenir.
- Bu ortamda çalışan bir SQL Server olmadığı için gerçek tarayıcı/`dotnet run` doğrulaması yapılamıyor (bkz. Bilinen Sınırlamalar).

## Bilinen Sınırlamalar

- Gerçek SMTP bilgisi olmadığı için Identity mesajları in-memory mock sender’a gider.
- SQL Server instance’ı bu ortamda çalıştırılmadı; migration üretimi/script/model kontrolleri SQL Server sağlayıcısıyla yapılır, çalışan testler SQLite kullanır. Bu yüzden `dotnet run` ile canlı tarayıcı doğrulaması yapılamıyor; tüm yeni commerce sayfaları SQLite tabanlı `WebApplicationFactory` testleriyle (200 render) doğrulandı.
- AFK uyarı modalının istemci runtime’ı frontend sahibine bırakılmıştır; timeout sunucuda zorunlu olarak uygulanır (uygulandı: `js/admin/idle-session.js`).
- `Model.FeaturedProducts` artık gerçek katalog verisiyle dolduruluyor (Ajan 4); yalnızca gerçek SQL Server ortamında görünür hale gelecek.
- Gerçek tarayıcı/ekran görüntüsü doğrulaması (scroll/reduced-motion/responsive görsel inceleme) yapılmadı; yalnızca HTTP/HTML/Razor render doğrulaması yapıldı.
