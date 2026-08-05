# Architecture Decisions

## ADR-001 — Adlandırma

Teknik solution/namespace `AETKAHVE`, public marka `AETERNUM RECTUS LUCIS` olarak tutulur. Toplu namespace rename yapılmaz.

## ADR-002 — Platform ve Katmanlar

.NET 10.0.301, ASP.NET Core MVC, EF Core 10.0.10 ve SQL Server seçildi. Domain → Application → Infrastructure → Web bağımlılık yönü korunur. Controller’lar doğrudan `AppDbContext` kullanmaz.

## ADR-003 — Ayrılmış Authentication

Customer, Admin ve SuperAdmin için farklı cookie scheme ve cookie adları kullanılır. Admin alanı policy scheme ile Admin veya SuperAdmin cookie’sini kabul eder; SuperAdmin alanı yalnız SuperAdmin cookie’sini kabul eder. Route gizliliği güvenlik sayılmaz.

## ADR-004 — AFK ve Remember Me

Remember Me yalnız cookie kalıcılığını belirler. Yönetim AFK’sı veritabanındaki `ManagementSession` kaydı, security stamp, mutlak süre ve son aktivite üzerinden doğrulanır. Status polling aktivite sayılmaz; keep-alive antiforgery korumalıdır.

## ADR-005 — Concurrency

Management session güncellemeleri sağlayıcıdan bağımsız `Guid ConcurrencyToken` ile korunur. Bu yaklaşım SQL Server ve SQLite test sağlayıcısında aynı davranışı verir.

## ADR-006 — Bildirim ve Secret

SMTP bilgisi bulunmadığından test edilebilir in-memory Identity message sender kullanılır. Parola, reset tokenı veya doğrulama kodu loglanmaz. Seed parolaları yalnız environment/user-secrets üzerinden alınır.

## ADR-007 — Test Veritabanı

Runtime SQL Server’dır; hızlı ve bağımsız integration testleri açık SQLite in-memory bağlantısı kullanır. SQLite native paketindeki bilinen yüksek önem dereceli advisory nedeniyle `SQLitePCLRaw.bundle_e_sqlite3` 3.0.5 doğrudan sabitlenmiştir.

## ADR-008 — Migration Sahipliği

`InitialIdentity` Ajan 3 tarafından üretilmiştir. Foundation integration’a merge edildikten sonra `AppDbContext`, migrations ve ModelSnapshot yalnız Ajan 4 tarafından değiştirilir; auth schema ihtiyacı contract request gerektirir.

## ADR-009 — Atomik Customer Üyeliği

E-posta sahipliği doğrulanmadan `AspNetUsers` kaydı oluşturulmaz. Kayıt verisi süreli `PendingCustomerRegistrations` tablosunda yalnız parola hash'i ve doğrulama token hash'iyle tutulur. Identity e-posta outbox gövdesi Data Protection ile şifreli saklanır. Confirm GET salt okunurdur; kullanıcı, Customer rolü, audit ve pending silme işlemi antiforgery korumalı confirm POST transaction'ında birlikte tamamlanır. Parola sıfırlama da yalnız geçerli tokenla form POST edildiğinde kullanıcı verisini değiştirir.
