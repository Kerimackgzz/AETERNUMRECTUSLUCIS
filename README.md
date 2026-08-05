# AETERNUM RECTUS LUCIS / AETKAHVE

`AETKAHVE` çözüm, namespace ve teknik proje adıdır. Müşteriye gösterilen marka adı `AETERNUM RECTUS LUCIS` olarak sabittir.

## Mimari

- `src/AETKAHVE.Domain`: framework bağımsız domain temeli.
- `src/AETKAHVE.Application`: uygulama sözleşmeleri, auth sabitleri ve DI extension noktası.
- `src/AETKAHVE.Infrastructure`: EF Core, SQL Server, Identity, cookie/session ve audit altyapısı.
- `src/AETKAHVE.Web`: ASP.NET Core MVC, Razor, route/policy ve layout hookları.
- `tests/*`: xUnit unit ve SQLite tabanlı integration testleri.

Hedef SDK .NET 10.0.301’dir. EF Core/Identity 10.0.10 ve SQL Server kullanılır.

## İlk Kurulum

```powershell
dotnet restore AETKAHVE.sln
dotnet ef database update --project src/AETKAHVE.Infrastructure --startup-project src/AETKAHVE.Web
dotnet run --project src/AETKAHVE.Web
```

Development bağlantısı `Database:ConnectionString` altında LocalDB kullanır. Başka bir SQL Server için environment variable biçimi:

```text
AETKAHVE_Database__ConnectionString=Server=...;Database=AETKAHVE;...
```

ASP.NET Core varsayılan environment değişkeni eşlemesi için `Database__ConnectionString` da kullanılabilir.

## Identity Seed

Roller ve isteğe bağlı development yönetim hesapları `IdentitySeed` seçenekleriyle üretilir. Repository içinde parola bulunmaz.

```text
IdentitySeed__Enabled=true
IdentitySeed__AdminEmail=admin@example.test
IdentitySeed__AdminPassword=<user-secret>
IdentitySeed__SuperAdminEmail=superadmin@example.test
IdentitySeed__SuperAdminPassword=<user-secret>
```

Bu değerleri production config dosyasına yazmayın; environment variable veya user-secrets kullanın.

## Auth ve Yönetim Route’ları

- Customer: `/account/login`
- Admin: doğrudan `/admin`; giriş `/admin/login`
- SuperAdmin: doğrudan `/superadmin`; giriş `/superadmin/login`

Public navbar, footer veya HTML içinde yönetim girişi bağlantısı bulunmaz. Customer, Admin ve SuperAdmin ayrı cookie scheme kullanır. SuperAdmin policy üzerinden Admin alanına girebilir; tersine erişim yasaktır.

`Beni Hatırla` seçilmezse session cookie, seçilirse süreli persistent cookie kullanılır. Yönetim AFK süreleri persistent cookie’den bağımsızdır: Admin 15, SuperAdmin 10 dakika.

## Test ve Kalite

```powershell
dotnet build AETKAHVE.sln --no-restore
dotnet test AETKAHVE.sln --no-build
dotnet format AETKAHVE.sln --verify-no-changes --no-restore
dotnet ef migrations has-pending-model-changes --project src/AETKAHVE.Infrastructure --startup-project src/AETKAHVE.Web --context AppDbContext --no-build
```

Integration testleri gerçek Identity/cookie/token davranışını SQLite, geçici Data Protection anahtarları ve kontrol edilebilir `TimeProvider` ile çalıştırır.

## Mock Servis ve Production Notları

- Development ve Testing ortamlarında kimlik/commerce e-postaları deterministik in-memory mock sender'larda tutulur; bu ortamlarda yapılandırma yanlışlıkla SMTP seçse bile dış gönderim yapılmaz.
- Production'da kimlik e-postaları kalıcı commerce outbox'ına yazılır ve SMTP worker tarafından kontrollü retry ile teslim edilir. Eksik/örnek SMTP ayarları startup validation'ı geçemez.
- Yönetim oturumları ve audit kayıtları SQL Server’da kalıcıdır.
- `/health/live` uygulama, `/health/ready` veritabanı erişimini kontrol eder; cevaplar secret içermez.
- Data Protection key-ring `DataProtection:KeyRingPath` altında kalıcı tutulur; production'da tüm replica'ların eriştiği şifreli ve yedeklenen bir volume kullanılmalıdır.
- Gerçek payment ve shipping adapter'ları henüz kayıtlı değildir; Production bu iki kritik bağımlılık eklenene kadar fail-closed olarak başlamaz.
- Ayrıntılı production ayarları ve operasyon notları: [`docs/project/PRODUCTION_DEPLOYMENT.md`](docs/project/PRODUCTION_DEPLOYMENT.md).
