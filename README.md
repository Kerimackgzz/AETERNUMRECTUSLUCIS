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

## Development Ortamında Gerçek SMTP

SMTP parolasını `appsettings*.json` dosyalarına, Git'e veya sohbet mesajına yazmayın. Development ortamında dış e-posta teslimi varsayılan olarak kapalıdır; yalnızca local user-secrets ile açıkça etkinleştirilir:

```powershell
dotnet user-secrets set "Notifications:UseMockProviders" "false" --project .\src\AETKAHVE.Web
dotnet user-secrets set "Notifications:AllowExternalDeliveryInDevelopment" "true" --project .\src\AETKAHVE.Web
dotnet user-secrets set "Smtp:Host" "smtp.example.com" --project .\src\AETKAHVE.Web
dotnet user-secrets set "Smtp:Port" "587" --project .\src\AETKAHVE.Web
dotnet user-secrets set "Smtp:UseSsl" "true" --project .\src\AETKAHVE.Web
dotnet user-secrets set "Smtp:UserName" "mail@example.com" --project .\src\AETKAHVE.Web
dotnet user-secrets set "Smtp:Password" "UYGULAMA_PAROLASI" --project .\src\AETKAHVE.Web
dotnet user-secrets set "Smtp:FromAddress" "mail@example.com" --project .\src\AETKAHVE.Web
dotnet user-secrets set "Smtp:FromName" "AETERNUM RECTUS LUCIS" --project .\src\AETKAHVE.Web
dotnet run --project .\src\AETKAHVE.Web
```

Sağlayıcı destekliyorsa normal hesap parolası yerine uygulama parolası kullanın. Kimlik e-postaları önce kalıcı outbox'a yazılır, arka plan worker'ı tarafından SMTP'ye teslim edilir. Daha önce kayıt olmuş fakat doğrulanmamış kullanıcılar `/account/resend-confirmation` sayfasından yeni doğrulama bağlantısı isteyebilir.

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

## Production Proxy ve Anahtar Yönetimi

Forwarded header işleme varsayılan olarak kapalıdır. Reverse proxy kullanılıyorsa yalnızca gerçek ingress adreslerini veya CIDR bloklarını allow-list'e ekleyin; uygulama `X-Forwarded-For` ve `X-Forwarded-Proto` değerlerini ancak bu kaynaklardan kabul eder. `ForwardLimit`, proxy zincirindeki güvenilir hop sayısıyla aynı olmalıdır.

```text
ForwardedHeaders__Enabled=true
ForwardedHeaders__ForwardLimit=1
ForwardedHeaders__KnownProxies__0=10.0.0.10
```

Production ortamında authentication, antiforgery ve korumalı guest-cart değerlerinin restart/replica sonrası çalışması için paylaşılan, kalıcı ve mutlak bir Data Protection key-ring yolu zorunludur. Persist edilen anahtarlar sertifikayla şifrelenir. Sertifika CurrentUser/LocalMachine personal store thumbprint'iyle veya secret provider'dan gelen PFX path/password ile sağlanabilir.

```text
DataProtection__ApplicationName=AETKAHVE
DataProtection__KeyRingPath=/var/lib/aetkahve/data-protection-keys
DataProtection__CertificateThumbprint=<deployment-certificate-thumbprint>
```

PFX alternatifi:

```text
DataProtection__CertificatePath=/run/secrets/aetkahve-data-protection.pfx
DataProtection__CertificatePassword=<secret-provider-value>
```

Key-ring dizini bütün replica'lar tarafından erişilebilir, yedeklenen ve yalnızca uygulama kimliğine yazma izni verilen durable storage olmalıdır. Allow-list, mutlak key-ring yolu veya key encryption sertifikası hatalı/eksikse production startup açık bir configuration hatasıyla durur.

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

- Testing ortamında kimlik/commerce e-postaları her zaman deterministik in-memory mock sender'larda tutulur. Development da varsayılan olarak mock kullanır; gerçek SMTP ancak iki ayrı opt-in ayarı ve geçerli SMTP secret'larıyla etkinleşir.
- Production'da kimlik e-postaları kalıcı commerce outbox'ına yazılır ve SMTP worker tarafından kontrollü retry ile teslim edilir. Eksik/örnek SMTP ayarları startup validation'ı geçemez.
- Yönetim oturumları ve audit kayıtları SQL Server’da kalıcıdır.
- `/health/live` uygulama, `/health/ready` veritabanı erişimini kontrol eder; cevaplar secret içermez.
- Data Protection key-ring `DataProtection:KeyRingPath` altında kalıcı tutulur ve production'da sertifikayla şifrelenir; tüm replica'ların eriştiği, yedeklenen ve erişim kontrollü bir volume kullanılmalıdır.
- Gerçek payment ve shipping adapter'ları henüz kayıtlı değildir; Production bu iki kritik bağımlılık eklenene kadar fail-closed olarak başlamaz.
- Ayrıntılı production ayarları ve operasyon notları: [`docs/project/PRODUCTION_DEPLOYMENT.md`](docs/project/PRODUCTION_DEPLOYMENT.md).
