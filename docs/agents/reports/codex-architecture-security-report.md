# Codex Architecture & Security Foundation Report

## Mimari

- .NET 10.0.301; Domain, Application, Infrastructure, MVC Web, UnitTests ve IntegrationTests.
- `Program.cs` yalnız Ajan 3 sahipliğinde; gerekli beş module/endpoint extension çağrısı hazır.
- SQL Server runtime, SQLite integration test sağlayıcısı.

## Authentication Scheme

- `AETKAHVE.Customer` / `AETKAHVE.Customer.Auth`
- `AETKAHVE.Admin` / `AETKAHVE.Admin.Auth`
- `AETKAHVE.SuperAdmin` / `AETKAHVE.SuperAdmin.Auth`
- `AETKAHVE.Management` policy selector Admin alanında Admin veya SuperAdmin cookie’sini seçer.

## Route ve Policy

- `CustomerOnly`: Customer scheme + Customer rolü.
- `AdminArea`: Management selector + Admin veya SuperAdmin rolü.
- `SuperAdminArea`: yalnız SuperAdmin scheme + SuperAdmin rolü.
- Public sayfalarda yönetim giriş bağlantısı yoktur.

## Cookie / AFK

- Remember Me false: session cookie; true: yapılandırılmış süreli persistent cookie.
- Customer 30 gün; Admin 12 saat; SuperAdmin 4 saat mutlak üst sınır.
- Admin AFK 15, SuperAdmin AFK 10 dakika; 60 saniye uyarı sözleşmesi.
- Session/status polling aktivite sayılmaz; keep-alive ve logout antiforgery korumalıdır.
- Yönetim cevaplarında `Cache-Control: no-store`, CSP, frame/content-type/referrer headerları ve correlation ID bulunur.

## Migration Handoff

- Migration: `InitialIdentity` (`20260804155915_InitialIdentity`).
- Identity, `ManagementSessions`, `AuditLogs` ve concurrency token dahildir.
- Foundation integration’a merge edilince `AppDbContext`, `Persistence/Migrations/*` ve snapshot sahipliği Ajan 4’e geçer. Ajan 3 bundan sonra doğrudan değiştirmez.

## Shared Contracts

- Route, ViewModel ve frontend/backend sözleşmeleri `docs/contracts` altında oluşturulup donduruldu.
- Public/Account/Admin/SuperAdmin layout; navbar, transition, CSRF, hero, product-card ve AFK hookları hazır.

## Build / Test

- `dotnet restore AETKAHVE.sln`: başarılı.
- `dotnet build AETKAHVE.sln --no-restore`: başarılı, 0 warning / 0 error.
- Unit: 8/8 başarılı.
- Integration: 16/16 başarılı.
- `dotnet format --verify-no-changes`: başarılı.
- `dotnet ef migrations list`: `20260804155915_InitialIdentity` listelendi; yerel SQL Server bulunmadığı için uygulanmış/pending veritabanı durumu sorgulanamadı.
- `dotnet ef migrations has-pending-model-changes`: model değişikliği yok.
- Idempotent SQL üretimi: başarılı; `artifacts/InitialIdentity.sql` 9.809 bayt ve gerekli Identity/session/audit tablolarını içeriyor.

## Commit

- Foundation implementation commit: `1ec0439`
- Initial repository commit: `b3c22b9`
- Final documentation/verification commit hash’i teslim mesajında bildirilecektir.

## Bilinen Konular

- Production SMTP/Identity SQL outbox entegrasyonu vardır; gerçek SMTP secret'ları deployment ortamında sağlanmalıdır.
- AFK frontend runtime'ı tek logout POST'u ve cross-tab senkronizasyonuyla tamamlanmıştır; sunucu enforcement nihai otoritedir.
- Bu oturum dışında oluşan Ajan 2 kök `wwwroot/*` ve raporu korunmuş, Ajan 3 commit’lerinden dışlanmıştır.

## Merge Hazır Durumu

Foundation ve sonraki production security hardening Coordinator tarafından `integration` üzerine alınmıştır. `main` değiştirilmemiştir; migration/snapshot sahipliği Ajan 4'e devredilmiştir.

## Post-Foundation Security Hardening — 2026-08-04

- Ajan 1 ve Ajan 2 değişikliklerinin `integration` üzerinde olduğu doğrulandı. Ajan 4 commerce çekirdeği denetim sırasında `6b2b47d` merge commit’iyle Ajan 3 dalına ulaştı; `integration` dalı bu sırada `7c0568d` üzerinde kaldı.
- `EventsType` kullanımının cookie options içindeki management redirect callback’lerini gölgelediği görüldü. `/admin/session/*` ve `/superadmin/session/*` challenge/forbid cevapları gerçek event sınıfında `401`/`403` üretecek şekilde düzeltildi.
- AFK, revoke, security-stamp, rol veya hesap durumu doğrulaması başarısız olduğunda principal reddine ek olarak ilgili authentication cookie’si artık açıkça siliniyor.
- AFK sonrası management cookie silinmesi ve customer security-stamp değişimi sonrası cookie silinmesi için iki integration testi eklendi.
- Build: 0 warning / 0 error. Unit: 21/21. Integration: 39/39. Toplam: 60/60.
- Değişen Ajan 3 dosyaları için `dotnet format --verify-no-changes --include ...` ve `git diff --check` başarılı.
- Tam solution format kontrolü, Ajan 4’e ait yeni commerce dosyalarındaki mevcut whitespace ihlalleri nedeniyle başarısızdır. Migration/commerce sahipliği gereği bu dosyalar Ajan 3 tarafından değiştirilmedi; Ajan 4/Coordinator düzeltme kapısı olarak kaydedildi.

## Identity Abuse Hardening — 2026-08-04

- Ajan 3 ayrı worktree’si güncel `integration` (`f008db0`) üzerine fast-forward edildi; ana Coordinator/Ajan 1 checkout’una dokunulmadı.
- Login limitleri hard-coded değerlerden `SecurityOptions` içindeki doğrulanan per-minute ayarlara taşındı.
- Customer registration ve forgot/reset password POST’larına ayrı IP-bölümlü rate-limit policy’leri eklendi.
- `429 Too Many Requests` cevapları fixed-window metadata’sından hesaplanan `Retry-After` başlığını döndürüyor.
- Options validation, registration limiter, password recovery limiter ve `Retry-After` için üç yeni test eklendi.
- Restore/build başarılı, 0 warning / 0 error. Unit: 22/22. Integration: 42/42. Toplam: 64/64.
- Commerce production provider/webhook/contact riskleri doğrudan sahiplik dışı kod değiştirilmeden `docs/contracts/requests/codex-architecture-security-20260804-commerce-security-hardening.md` ile Ajan 4/Coordinator’a iletildi.
- AFK istemcisinin süre sonunda `logoutUrl` çağırmaması ve cross-tab senkronizasyon eksikliği, backend logout cookie testiyle birlikte `docs/contracts/requests/codex-architecture-security-20260804-afk-client-expiry.md` üzerinden frontend sahiplerine iletildi.
- Tam solution format kontrolü commerce whitespace borcu nedeniyle hâlâ başarısız; Ajan 3’e ait değişen C# dosyalarının scoped format kontrolü ayrıca uygulanır.

## Correlation ID Hardening — 2026-08-04

- İstemci tarafından sağlanan `X-Correlation-ID` artık yalnız ASCII harf/rakam ile `-`, `_`, `.` karakterlerini ve en fazla 128 karakteri kabul eder.
- Güvensiz değerler response/audit/log katmanına taşınmadan 32 karakterlik server-generated GUID ile değiştirilir.
- Safe passthrough ve unsafe replacement davranışı integration testiyle doğrulandı.
- Güncel test toplamı: Unit 22/22, Integration 43/43, toplam 65/65.

## Integration Security Regression — 2026-08-04

- Ajan 3 worktree'sine güncel `integration` (`446e9c4`) çatışmasız olarak alındı; son Ajan 3 sync merge commit'i `70a62f5` oldu.
- Yeni Account/Admin görünümleri, public page-transition/contact/post-purchase akışları ve Ajan 4 SQLite development/commerce mutation düzeltmeleri birleşik kod tabanında denetlendi.
- `SecurityOptions.AdminRoute` ve `SuperAdminRoute` değerlerinin cookie yönlendirmeleri ile sabit controller route'larını ayırması engellendi. Sözleşmedeki `admin` / `superadmin` değerlerinden sapma artık startup options validation sırasında fail-fast sonuçlanır.
- Birleşen 11 Admin commerce sayfası anonim challenge, Admin erişimi, SuperAdmin'in Admin alanına erişimi, Customer reddi, `Cache-Control: no-store`, CSP/frame/content-type/correlation header'ları ve JSON mutation antiforgery davranışıyla kilitlendi.
- `dotnet restore` ve `dotnet build --no-restore`: başarılı, 0 warning / 0 error.
- Unit: 25/25; Integration: 52/52; toplam: 77/77 başarılı.
- EF migration zinciri: `InitialIdentity`, `AddCommerceCatalogAndCustomer`, `AddCommerceCheckoutAndFulfillment`, `AddCommerceEngagement`. Pending model değişikliği yok; idempotent SQL üretimi başarılı.
- `dotnet list AETKAHVE.sln package --vulnerable --include-transitive`: doğrudan veya transitif bilinen savunmasız paket bulunmadı.
- Ajan 3'e ait değişen dosyalarda scoped `dotnet format --verify-no-changes` ve `git diff --check` başarılı.
- Tam solution format kapısı hâlâ Ajan 4 sahipliğindeki commerce dosyalarının mevcut whitespace borcu nedeniyle başarısızdır; sahiplik sınırı gereği Ajan 3 bu dosyaları değiştirmedi.
- Public `POST /contact`, `Security:ContactRequestsPerMinute` ile yapılandırılan IP-bölümlü fixed-window global limiter tarafından korunur; limit aşımında `429` ve `Retry-After` integration testiyle doğrulandı.
- `codex-architecture-security-20260804-commerce-security-hardening.md` isteğinde gerçek production payment/webhook doğrulaması ile trusted-proxy yapılandırması henüz uygulanmış değildir.
- `codex-architecture-security-20260804-afk-client-expiry.md` isteğindeki yerel expiry sırasında antiforgery korumalı logout ve cross-tab senkronizasyonu henüz uygulanmış değildir; backend AFK enforcement ve cookie silme testleri çalışmaktadır.
- Bu turdaki uygulama commit'leri: `31381ff91501cd55555e118d7a69968b327822e5`, `9243a1d725e68c7ecadda12693c91e0a64c436e2`.
- Rapor commit'inden sonraki nihai branch HEAD hash'i teslim mesajında bildirilir; Ajan 3 doğrudan `integration` veya `main` üzerine merge yapmaz.

## Final integrated security closure — 2026-08-05

- Ajan 3 production security dalı `b86da0c` ile `integration` üzerine alındı. Ajan 4 payment/webhook teslimi `84610f2`, Ajan 1 AFK frontend teslimi `243b9f6` ile bunu izledi.
- Forwarded headers varsayılan kapalıdır. Etkinleştirme explicit `KnownProxies`/`KnownNetworks`, sınırlı forward count ve header symmetry gerektirir; middleware rate limiter'dan önce çalışır. Trusted, untrusted ve disabled proxy partition testleri geçer.
- Production Data Protection absolute durable key-ring path ve thumbprint veya PFX sertifikası ister. Persist edilen key XML'i sertifikayla şifrelenir; eksik path, erişilemeyen dizin, bulunamayan/private-key içermeyen/geçersiz sertifika fail-fast sonuçlanır.
- SMTP/Identity SQL outbox ve notification retry/lease işleri korunmuş; eski relative ve uygulama seviyesinde şifrelenmemiş key repository implementasyonu kaldırılmıştır.
- Customer/Admin/SuperAdmin scheme, rol ve policy ayrımı korunur. Cookie handler ve management session aynı uygulama `TimeProvider`'ını kullanır; test fixture başlangıcı `HttpClient` cookie wall-clock davranışıyla hizalıdır.
- Customer login/register/password recovery, Admin/SuperAdmin login ve public contact limitleri doğrulanan IP-bölümlü ayarlara bağlıdır. Güvensiz correlation ID değerleri audit/log/response'a taşınmadan yenilenir.
- AFK expiry isteği tamamlandı: tek antiforgery same-origin logout POST'u, üç saniyelik redirect fallback'i, duplicate-submit guard ve Admin/SuperAdmin portal-scope cross-tab logout/keep-alive senkronizasyonu vardır.
- Commerce security isteği tamamlandı: Production ödeme varsayılanı `Disabled`, Mock yalnız Development/Testing, unknown provider reddi, active-provider eşleşmesi, 64 KiB body sınırı ve HMAC-SHA256/timestamp/constant-time/replay doğrulaması uygulanır.
- Final doğrulama: Release build 0 uyarı / 0 hata; frontend 5/5; unit 54/54; integration 77/77.
- EF zinciri `InitialIdentity` + üç commerce migration'ından oluşur; pending model change yoktur. SQL Server idempotent script 54.628 bayt, SHA-256 `DF956CA76B4DED1E5885DB67F90FAE385F1CAF9FDACD32CDAE147E6F1FC705DF`.
- NuGet direct/transitive vulnerability audit temiz; 26 JavaScript syntax kontrolü, repository-geneli `dotnet format --verify-no-changes` ve `git diff --check` başarılı.
- Açık production işleri: gerçek payment/shipping adapter'ları, çok-instance deployment için distributed/durable replay store, deployment SMTP secret'ları ve private-key erişimli Data Protection sertifikası.
- Bu raporun documentation commit'i self-reference nedeniyle kendi hash'ini içermez; temiz final `integration` hash'i teslim mesajında bildirilir. `main` değiştirilmemiştir.

## Gerçek SQL Server migration doğrulaması — 2026-08-05

- Dört migration gerçek `.\SQLEXPRESS` instance'ındaki benzersiz smoke veritabanına hatasız uygulandı.
- 54.628 baytlık idempotent SQL aynı veritabanında art arda iki kez hatasız çalıştı; history 4 kayıtta kaldı ve `DBCC CHECKDB` temizdi.
- Smoke veritabanı ve temp script silindi; migration/AppDbContext/appsettings kaynakları değiştirilmedi.
- Uyumsuzluk bulunmadığı için Ajan 4 contract request'i gerekmedi.
- Ayrıntılı kanıt: `docs/agents/reports/codex-architecture-security-sqlserver-smoke-report.md`.
