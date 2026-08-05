# Ajan 3 — Gerçek SQL Server Migration Smoke Raporu

Tarih: 2026-08-05

## Kapsam ve sınırlar

- Branch `agent/codex-architecture-security`, `integration` / `0960d14` üzerine `git merge integration` ile fast-forward edildi.
- `AppDbContext`, `src/AETKAHVE.Infrastructure/Persistence/Migrations/*`, model snapshot ve `src/AETKAHVE.Web/appsettings.json` değiştirilmedi.
- Bağlantı yalnız `dotnet ef database update --connection` CLI override'ı ile verildi; geçici appsettings dosyası oluşturulmadı.
- Gerçek instance: `.\SQLEXPRESS`; servis `MSSQL$SQLEXPRESS` çalışır durumda.
- SQL Server: Express Edition (64-bit), sürüm `16.0.1000.6`.
- Yalnız bu doğrulama için benzersiz `AETKAHVE_Agent3_MigrationSmoke_20260805_6F2C91A4` veritabanı kullanıldı.

## Baseline

- `dotnet restore AETKAHVE.sln`: başarılı.
- Release build: 0 uyarı / 0 hata.
- Unit: 54/54 başarılı.
- Integration: 77/77 başarılı.

## Gerçek migration uygulaması

`dotnet ef database update`, SQL Express bağlantısı `--connection` ile override edilerek çalıştırıldı ve şu migration'ları sırayla uyguladı:

1. `20260804155915_InitialIdentity`
2. `20260804170142_AddCommerceCatalogAndCustomer`
3. `20260804170153_AddCommerceCheckoutAndFulfillment`
4. `20260804170203_AddCommerceEngagement`

Sonuç:

- Dört migration da hatasız uygulandı.
- `dbo.__EFMigrationsHistory` tam 4 kayıt içerdi; her kaydın EF ürün sürümü `10.0.10` idi.
- Oluşan kullanıcı tablosu sayısı: 43.
- İlk migration `InitialIdentity`, son migration `AddCommerceEngagement` olarak doğrulandı.
- `DBCC CHECKDB` hata bildirmedi.

## İdempotent SQL doğrulaması

- Script yeniden üretildi: 54.628 bayt.
- SHA-256: `DF956CA76B4DED1E5885DB67F90FAE385F1CAF9FDACD32CDAE147E6F1FC705DF`.
- Script gerçek smoke veritabanına `sqlcmd -b` ile art arda iki kez uygulandı.
- Her iki çalıştırma da exit code 0 ile tamamlandı.
- İkinci çalıştırma sonrasında migration history sayısı 4 olarak kaldı; duplicate migration veya SQL hatası oluşmadı.

## Temizlik ve repository bütünlüğü

- Smoke veritabanı `DROP DATABASE` ile kaldırıldı; `sys.databases` doğrulaması kalan kayıt sayısını 0 gösterdi.
- Üretilen temp SQL script'i silindi.
- Geçici configuration dosyası oluşturulmadı.
- `git diff --name-only -- src/AETKAHVE.Infrastructure/Persistence src/AETKAHVE.Web/appsettings.json` boş döndü.
- Migration veya model uyumsuzluğu bulunmadı; bu nedenle Ajan 4'e contract request açılmadı.

## Sonuç

Mevcut dört migration hem EF Core migration runner hem de idempotent SQL script üzerinden gerçek SQL Server Express 2022 üzerinde doğrulandı. ADR-008 sahiplik sınırı korunmuştur.
