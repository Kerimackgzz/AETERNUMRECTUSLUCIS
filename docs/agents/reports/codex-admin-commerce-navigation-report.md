# Codex — Admin Commerce Dashboard ve Modül Navigasyonu

Tarih: 2026-08-07  
Branch: `integration`

## Teslim

- `/admin` ve `/superadmin` placeholder dashboard'ları gerçek commerce projection'ına bağlandı.
- Son 30 gün net gelir, ödenen sipariş, ortalama sipariş ve kritik stok metrikleri eklendi.
- Sipariş, sevkiyat, iade, yorum ve mesaj operasyon kuyrukları gerçek veritabanı sayımlarıyla gösteriliyor.
- Son beş sipariş server-side projection ile dashboard'a taşındı.
- Ürün, katalog, sipariş, sevkiyat, fatura, iade, kampanya, kupon, yorum, mesaj ve rapor modülleri ortak yönetim navigasyonuna bağlandı.
- Admin ve SuperAdmin layout'ları aynı modüler kabuğu kullanıyor; portal dashboard ve güvenlik bağlantıları kendi route ailelerini koruyor.
- Desktop'ta kalıcı sidebar, mobilde modal drawer uygulandı. Drawer; `aria-expanded`, `aria-current`, Escape, backdrop click, focus restore, focus döngüsü, `inert`, body scroll lock ve reduced-motion davranışlarını içeriyor.
- Dondurulmuş `DashboardSummaryViewModel` değiştirilmedi; yeni `AdminDashboardSummary` ve `AdminDashboardViewModel` kullanıldı.
- Şema değişmedi ve migration gerekmedi.

## Doğrulama

- Release build: 0 uyarı, 0 hata.
- Unit testleri: 82/82 başarılı.
- Integration testleri: 110/110 başarılı.
- Frontend testleri: 12/12 başarılı.
- Yeni dashboard/navigation integration testleri: 2/2 başarılı.
- `git diff --check`: whitespace hatası yok.

