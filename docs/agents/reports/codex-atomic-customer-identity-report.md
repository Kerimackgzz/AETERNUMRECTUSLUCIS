# Ajan 3 — Atomik Customer Identity Raporu

Tarih: 2026-08-05

## Sonuç

- Register POST artık `AspNetUsers` kaydı oluşturmaz; Identity parola hash'i ve SHA-256 token hash'i süreli `PendingCustomerRegistrations` kaydında tutulur.
- Confirm GET salt okunur ekran sunar. Customer hesabı, doğrulanmış e-posta, rol ve audit yalnız antiforgery korumalı POST transaction'ında oluşturulur; pending kayıt aynı transaction'da silinir.
- Aynı e-postayla tekrar kayıt veya yeniden gönderim tokenı döndürür ve eski bağlantıyı geçersiz kılar. Token 60 dakika, pending veri 7 gün geçerlidir.
- Forgot-password ve reset GET kullanıcı verisini değiştirmez. Geçerli reset POST parola/security stamp ve audit kaydını transaction içinde tamamlar.
- SMTP outbox, henüz gerçek kullanıcı olmayan pending kaydı `ReservedUserId` üzerinden kabul eder. Identity e-posta gövdesi Data Protection ile şifrelenerek saklanır ve yalnız worker teslimatında çözülür; düz parola/token loglanmaz veya veritabanına düz metin olarak yazılmaz.

## Kullanıcı talimatıyla yerel veri temizliği

- Development SQLite üzerinde `kerimgirdap@gmail.com` kaydı silme öncesi doğrulandı: `EmailConfirmed=false`, yalnız `Customer` rolü, korunması gereken commerce verisi yok.
- Customer rol bağlantısı ve kullanıcı tek transaction'da silindi. İki başarısız giriş audit kaydı korundu, kullanıcı bağlantısı anonimleştirildi.
- Silme sonrası aynı normalize e-postaya ait `AspNetUsers` kayıt sayısı `0` olarak doğrulandı.
- Development SQLite verisi silinmeden yeni pending tablo ve indeksleri eklendi.

## Migration ve doğrulama

- Yeni migration: `AddPendingCustomerRegistration`; önceki dört migration değiştirilmedi.
- `dotnet ef migrations has-pending-model-changes`: temiz.
- Gerçek `.\SQLEXPRESS` smoke veritabanında beş migration uygulandı.
- Migration history `5`, pending tablo `1`, benzersiz normalize e-posta indeksi `1` olarak doğrulandı.
- 56.741 baytlık idempotent script iki kez hatasız çalıştı; `DBCC CHECKDB` temizdi ve smoke veritabanı kaldırıldı.
- Build: 0 uyarı / 0 hata. Unit: 75/75. Integration: 83/83. Frontend: 9/9.
