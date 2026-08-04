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

- Gerçek SMTP/outbox production entegrasyonu yok; in-memory mock kullanılır.
- AFK frontend runtime’ı sözleşmeyle ayrılmıştır; sunucu enforcement çalışır.
- Bu oturum dışında oluşan Ajan 2 kök `wwwroot/*` ve raporu korunmuş, Ajan 3 commit’lerinden dışlanmıştır.

## Merge Hazır Durumu

Foundation, final EF ve Git kontrolleri başarılı olduktan sonra Coordinator review/merge için hazırdır. Ajan 3 doğrudan `integration` veya `main` üzerine merge yapmaz.

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
