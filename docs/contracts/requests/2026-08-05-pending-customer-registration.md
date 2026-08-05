# Contract Request — Pending Customer Registration Schema

## İstek sahibi

Ajan 3 / Architecture & Security

## Migration sahibi

Ajan 4, ADR-008 uyarınca `AppDbContext`, migration ve ModelSnapshot değişikliklerinin sahibidir.

## Gerekçe

E-posta doğrulaması tamamlanmadan `AspNetUsers` kaydı oluşturulması yarım ve kullanılamayan hesap bırakıyor. Gerçek Identity kullanıcısı yalnız doğrulama tamamlandığında atomik olarak oluşturulmalıdır.

## Sözleşme

- Yeni tablo: `PendingCustomerRegistrations`.
- Benzersiz indeks: `NormalizedEmail`.
- Saklanan hassas değerler yalnız Identity parola hash'i ve SHA-256 doğrulama token hash'idir; düz parola/token yasaktır.
- Token ömrü 60 dakika, pending veri saklama süresi 7 gündür.
- `ReservedUserId`, doğrulama tamamlandığında oluşturulacak `AspNetUsers.Id` değeridir ve pre-user outbox teslimat korelasyonu için kullanılır.
- Mevcut Identity/commerce migration'ları geriye dönük değiştirilmez; yalnız ileri yönlü migration eklenir.
- Mevcut yarım `kerimgirdap@gmail.com` hesabı kullanıcı talimatıyla Development SQLite veritabanından ayrıca güvenli biçimde silinmiştir; migration genel kullanıcı silme işlemi yapmaz.
