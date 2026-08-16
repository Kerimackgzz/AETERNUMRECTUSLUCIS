# Codex — Çift Rollü Yönetim Hesabı ve Güvenlik Merkezi Raporu

Tarih: 2026-08-07  
Branch: `integration`

## Teslim edilen kapsam

- Aynı normalize e-posta için tek Identity kullanıcısında `Admin` ve `SuperAdmin` rollerini birleştiren idempotent seed akışı eklendi.
- Eksik çift, aynı e-posta için farklı parola, Production açık onayı eksikliği ve Customer-only hesabın sessiz yükseltilmesi startup doğrulamasıyla reddediliyor.
- Eski yönetim hesapları için replacement ve rol kontrolleri yapan açık onaylı, tek seferlik `UserManager` retirement akışı eklendi.
- `/admin/security` ve `/superadmin/security` altında ortak e-posta/parola güvenlik merkezi eklendi.
- Anonim cihazda açılabilen, GET'te salt okunur, antiforgery POST'unda tamamlanan e-posta doğrulama akışı iki portala bağlandı.
- Kritik credential değişikliklerinde Admin ve SuperAdmin yönetim oturumları birlikte iptal ediliyor.
- Yönetim login/logout, AFK ve security-stamp sonlandırmaları ortak, tek kullanımlık üst bildirimlerle açıklanıyor.
- Parola alanlarında erişilebilir görünürlük düğmeleri ve iki yönetim layout'unda Güvenlik bağlantıları eklendi.
- Credential mantığı `IAccountCredentialService` altında ortaklaştırıldı; mevcut müşteri servisi sözleşmesi korundu.

## Development provisioning sonucu

- Bootstrap parolası yalnız Development user-secrets üzerinden geçici olarak sağlandı; kaynak koda, config dosyasına, teste veya bu rapora yazılmadı.
- Bootstrap öncesi SQLite veritabanı geri yüklenebilir biçimde `artifacts/local-backups/aetkahve-development-before-management-bootstrap-20260807-115139.db` konumuna yedeklendi.
- Tek replacement kullanıcı oluşturuldu ve veritabanında tam olarak `Admin,SuperAdmin` rolleri doğrulandı.
- Gerçek HTTP form/antiforgery/cookie akışında `/admin/login` → `/admin` ve `/superadmin/login` → `/superadmin` yönlendirmeleri doğrulandı.
- Retirement allow-list'i idempotent olarak çalıştırıldı; final veritabanı kontrolünde eski iki `.test` yönetim hesabı bulunmuyor.
- Doğrulama tamamlanınca bütün `IdentitySeed` anahtarları Development user-secrets'tan kaldırıldı; kalıcı olan yalnız Identity parola hash'idir.
- Test sunucuları kapatıldı ve geçici portlarda listener bırakılmadı.

## Production durumu

- Canlı Production secret-store/veritabanı erişimi bu workspace'te bulunmadığı için Production hesabı provision edilmedi.
- `docs/deployment/PRODUCTION_SETUP.md`; bakım penceresi, yedek, ayrı ve en az 24 karakterlik deployment parolası, çift portal doğrulaması, kontrollü retirement ve seed secret temizliği sırasını içeriyor.
- Sohbette sağlanan Development parolası Production için kullanılmamalıdır.

## Doğrulama

- `dotnet build AETKAHVE.sln -c Release --no-restore`: 0 uyarı, 0 hata.
- Unit testleri: 82/82 başarılı.
- Integration testleri: 108/108 başarılı.
- Frontend testleri: 10/10 başarılı.
- Yönetim güvenlik merkezi hedef testleri: 4/4 başarılı.
- `git diff --check`: whitespace hatası yok.
- Veritabanı migrationı gerekmedi; şema değiştirilmedi.

