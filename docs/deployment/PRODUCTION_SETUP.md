# Production Setup Runbook — SMTP ve Data Protection Key-Ring

## Yönetim hesabı bootstrap ve eski hesapları emekliye ayırma

Yönetim seed'i normal deployment'ta kapalı kalır. İlk kurulum veya kontrollü hesap değişikliğinde:

1. Veritabanının zaman damgalı, geri yüklenebilir yedeğini alın ve bakım penceresi açın.
2. Secret store'da `IdentitySeed:Enabled=true` ve `IdentitySeed:AllowInProduction=true` ayarlayın.
3. Tek replacement e-postasını hem `AdminEmail` hem `SuperAdminEmail` alanına yazın. Aynı, secret store tarafından oluşturulmuş en az 24 karakterlik parolayı iki parola alanına verin.
4. Uygulamayı bir kez başlatın; replacement hesabıyla hem `/admin/login` hem `/superadmin/login` girişini doğrulayın.
5. Production başlangıç kontrolünün veritabanında tam olarak bir aktif, silinmemiş `SuperAdmin` bulması gerekir. İkinci bir SuperAdmin veya kullanılabilir SuperAdmin bulunmaması uygulamanın açılışını güvenlik hatasıyla durdurur.
6. Eski hesapları kaldırmak için `IdentitySeed:AllowDestructiveRetirement=true` ve sıralı `IdentitySeed:RetireManagementEmails` secret değerlerini ekleyip uygulamayı tekrar başlatın. Silme yalnız replacement hazırlandıktan sonra `UserManager` ile yapılır; bulunmayan hesaplar idempotent olarak atlanır.
7. Giriş ve audit doğrulamasından sonra `IdentitySeed` altındaki parolaları, retirement listesini ve iki allow bayrağını secret store'dan kaldırın; `Enabled=false` durumuna dönün.

Startup; eksik e-posta/parola çiftinde, aynı e-posta için farklı parolalarda veya Production opt-in'i eksikken durur. Seeder mevcut Customer-only hesabı yöneticiye yükseltmez. Retirement; replacement e-postasını, Customer rolü bulunan hesabı ve management dışı rol taşıyan hesabı reddeder. Secret ve parolalar uygulama loglarına yazılmaz.

Bu belge, `docs/project/PROJECT_STATUS.md` içindeki "Production SMTP değerleri ve kalıcı/shared Data Protection key-ring yolu deployment ortamında açıkça sağlanmalıdır" açık kapısını kapatmak için gereken adımları anlatır. Hangi config anahtarının hangi ortam değişkeninden geldiğini, bu değerlerin deployment öncesi nasıl sağlanacağını ve neden bir key-ring'in birden fazla instance arasında paylaşılması gerektiğini adım adım açıklar.

## Müşteri profil fotoğrafları

`FileStorage:RootDirectory` (`FileStorage__RootDirectory`) Production'da container veya uygulama instance'ının ephemeral dosya sistemine verilmemelidir. Profil fotoğrafları bu private kökte saklandığı için bütün replica'ların okuyup yazabildiği kalıcı/paylaşılan bir volume kullanılmalıdır. Dizin public web root'un dışında kalmalı, yalnız uygulama identity'sine okuma-yazma izni verilmeli, yedeklenmeli ve uygulama loglarına storage key dışında dosya içeriği veya kullanıcı profil verisi yazılmamalıdır.

Örnek: `FileStorage__RootDirectory=/mnt/aetkahve-private/uploads`. Kubernetes'te uygun bir RWX PersistentVolume, VM/on-prem kurulumunda erişim kontrollü kalıcı disk/NFS/SMB mount kullanılabilir. Rolling deployment öncesinde yeni ve eski replica'ların aynı yolu gördüğü doğrulanmalıdır.

Kapının teknik/kod tarafı zaten tamamdır: `SmtpOptions` (`src/AETKAHVE.Infrastructure/Options/CommerceOptions.cs`) ve `DataProtectionKeyRingOptions` (`src/AETKAHVE.Infrastructure/Options/ProductionSecurityOptions.cs`), ilgili validator'ları (`ProductionOptionsValidators.cs`, `ProductionSecurityOptionsValidators.cs`) ile Production'da `ValidateOnStart()` zinciriyle zorunlu kılınır. Eksik veya geçersiz config ile host, ilk `IOptions<T>` çözümlemesinde veya `AddWebSecurityModule` sırasında `OptionsValidationException` fırlatarak başlamayı reddeder (fail-closed). Bu belge sadece **operasyonel** boşluğu kapatır: gerçek bir deployment'ta bu değerler nereden ve nasıl sağlanır.

Genel gate özeti ve outbox/retry operasyon detayları için bkz. [`docs/project/PRODUCTION_DEPLOYMENT.md`](../project/PRODUCTION_DEPLOYMENT.md). Placeholder değerlerin tam şekli için bkz. [`src/AETKAHVE.Web/appsettings.Example.json`](../../src/AETKAHVE.Web/appsettings.Example.json).

## 1. Config anahtarı → kaynak eşlemesi

.NET configuration, ortam değişkenlerinde `Section__Key` (çift alt çizgi) biçimini `Section:Key` olarak okur. Aşağıdaki tablo her anahtarın appsettings karşılığını, önerilen kaynağını ve neden gizli/açık olduğunu listeler.

| appsettings anahtarı | Ortam değişkeni | Kaynak önerisi | Not |
| --- | --- | --- | --- |
| `Smtp:Host` | `Smtp__Host` | Deployment config (gizli değil) | Örn. `smtp.sendgrid.net` |
| `Smtp:Port` | `Smtp__Port` | Deployment config | Varsayılan `587` çoğu sağlayıcı için doğrudur |
| `Smtp:UseSsl` | `Smtp__UseSsl` | Deployment config | Production'da `true` olmalı; `false` validation tarafından reddedilir |
| `Smtp:UserName` | `Smtp__UserName` | **Secret store** | `Password` ile birlikte verilmeli veya ikisi de boş bırakılmalı |
| `Smtp:Password` | `Smtp__Password` | **Secret store** | Repository'ye, appsettings dosyalarına veya commit'e asla yazılmaz |
| `Smtp:FromAddress` | `Smtp__FromAddress` | Deployment config | `.invalid` uzantılı adresler Production'da reddedilir |
| `Smtp:FromName` | `Smtp__FromName` | Deployment config | Zorunlu, boş bırakılamaz |
| `DataProtection:ApplicationName` | `DataProtection__ApplicationName` | Deployment config | Tüm replica'larda aynı olmalı |
| `DataProtection:KeyRingPath` | `DataProtection__KeyRingPath` | Deployment config (altyapı tarafından provision edilir) | Absolute path; kalıcı ve paylaşılan volume |
| `DataProtection:CertificateThumbprint` | `DataProtection__CertificateThumbprint` | Deployment config + host sertifika deposu | CurrentUser/LocalMachine personal store'da önceden yüklenmiş olmalı |
| `DataProtection:CertificatePath` / `CertificatePassword` | `DataProtection__CertificatePath` / `DataProtection__CertificatePassword` | **Secret store** (PFX dosyası + parola) | Thumbprint yerine PFX kullanılıyorsa; ikisi birden yapılandırılamaz |

`Notifications:UseMockProviders=false` olmadan `Smtp` alanları hiç doğrulanmaz — mock sağlayıcılar Development/Testing'de veya bu bayrak açıkken kullanılır. Production'da `NotificationOptionsValidator`, `UseMockProviders=true` kalırsa startup'ı ayrıca durdurur.

## 2. Secret'ları nereden sağlamalı

Repository'de hiçbir gerçek SMTP kimlik bilgisi, sertifika private key'i veya parola bulunmaz; `appsettings.Example.json` yalnız placeholder değerler içerir. Gerçek değerler ortam bazında şu sırayla değerlendirilmelidir:

1. **Yerel geliştirme / manuel staging denemesi** — `dotnet user-secrets` kullanın; secret'lar repository dışında, kullanıcı profilinde saklanır ve yanlışlıkla commit edilemez:
   ```
   dotnet user-secrets init --project src/AETKAHVE.Web
   dotnet user-secrets set "Smtp:UserName" "<deneme-kullanıcısı>" --project src/AETKAHVE.Web
   dotnet user-secrets set "Smtp:Password" "<deneme-parolası>" --project src/AETKAHVE.Web
   ```
   User Secrets yalnız Development ortamında otomatik yüklenir; Production'da hiç kullanılmamalıdır.
2. **Gerçek deployment (staging/production)** — platformun secret store'u üzerinden environment variable olarak enjekte edin: Azure App Service/Container Apps için Key Vault referansı, AWS için Secrets Manager/Parameter Store (SecureString), Kubernetes için `Secret` nesnesi + `envFrom`, on-prem için sistem servis şablonundaki (systemd `EnvironmentFile`, örn. `/etc/aetkahve/aetkahve.env`, dosya izinleri `600`) şifreli bir dosya. Secret store, credential rotasyonunu ve erişim denetimini uygulama koduna hiç dokunmadan sağlar.
3. **CI/CD pipeline** — build/test aşamasında gerçek SMTP/sertifika secret'ı hiç gerekmez (testler mock/self-signed sertifika üretir); yalnız deploy adımı, secret store'dan çektiği değerleri hedef ortamın environment variable'larına yazar.

Sertifika (`CertificateThumbprint` yolunda) private key'iyle birlikte hedef makinenin/container image'ının sertifika deposuna deployment sırasında (örn. konteyner init script'i veya VM provisioning) yüklenir; repository bu adımı tetiklemez ve sertifika dosyasını içermez.

## 3. Data Protection key-ring neden paylaşılmalı

`DataProtection:KeyRingPath`, ASP.NET Core'un authentication cookie'leri, antiforgery token'ları ve korumalı misafir-sepeti cookie'sini şifrelemek/imzalamak için kullandığı anahtarları saklar. Bu key-ring **her uygulama instance'ında ayrı ayrı değil, tüm replica'lar arasında paylaşılan tek bir kalıcı dizin** olmalıdır:

- Aynı deployment'ın birden fazla instance'ı (yatay ölçekleme, rolling update, container yeniden zamanlama) varsa ve her biri kendi lokal/ephemeral key-ring'ini kullanırsa, bir instance'ın verdiği cookie/antiforgery token'ı başka bir instance tarafından çözülemez. Sonuç: kullanıcılar rastgele 400 (antiforgery hatası) veya beklenmedik logout görür, load balancer round-robin yaptıkça sorun tutarsız şekilde tekrar eder.
- Container/VM yeniden başlatıldığında (deploy, autoscaling, crash recovery) ephemeral bir dizin (örn. container'ın yazılabilir katmanı) sıfırlanır; key-ring kaybolur ve tüm aktif oturumlar/antiforgery token'ları anında geçersiz kalır.
- Bu nedenle `KeyRingPath`, altyapı tarafından provision edilen kalıcı ve paylaşılan bir volume'a işaret etmelidir: Kubernetes'te `ReadWriteMany` PersistentVolume (NFS, Azure Files, EFS), VM/on-prem'de merkezi bir NFS/SMB mount, tek-instance deployment'ta bile container yeniden oluşturulduğunda hayatta kalan kalıcı bir disk.
- `DataProtectionKeyRingOptionsValidator`, Production'da `KeyRingPath` boşsa veya relative bir path verilmişse startup'ı durdurur; bu, "unutulan paylaşımlı volume" hatasının sessizce prod'a çıkmasını engeller.
- Persist edilen XML anahtarları uygulama seviyesinde `CertificateThumbprint`/`CertificatePath` sertifikasıyla şifrelenir; sertifikanın private key'i olmadan volume'a erişim tek başına anahtarları açığa çıkarmaz. Volume yine de altyapı seviyesinde şifreli ve yedekli olmalıdır (bkz. [`PRODUCTION_DEPLOYMENT.md`](../project/PRODUCTION_DEPLOYMENT.md) "Data Protection key-ring" bölümü).
- Key-ring dizini kaybolursa: tüm aktif auth/antiforgery/sepet cookie'leri ve bazı Identity token'ları geçersiz kalır, kullanıcılar yeniden login olmak zorunda kalır. Bu, veri kaybı değildir ama planlı bir "graceful re-auth" penceresi olmadan yapılmamalıdır.

## 4. Deployment öncesi kontrol listesi

1. Paylaşılan, kalıcı, altyapı seviyesinde şifreli bir volume provision edin ve tüm replica'ları aynı `DataProtection:KeyRingPath`'e mount edin.
2. Key-encryption sertifikasını (thumbprint veya PFX) hedef ortamın sertifika deposuna/secret store'una yükleyin; uygulama identity'sinin private key'e okuma erişimi olduğunu doğrulayın.
3. SMTP host/port/SSL değerlerini deployment config'ine, `UserName`/`Password`'ü secret store'a yazın.
4. `Notifications:UseMockProviders=false` olarak ayarlayın (aksi halde SMTP hiç doğrulanmaz ve mock sağlayıcı Production'da sessizce kalır — ki bu zaten `NotificationOptionsValidator` tarafından reddedilir).
5. Uygulamayı Production ortam adıyla başlatıp startup loglarını izleyin: eksik/geçersiz bir değer varsa host, `OptionsValidationException` mesajıyla (hangi anahtarın eksik olduğunu belirterek) hemen kapanır — bu beklenen ve istenen fail-closed davranıştır.
6. Başarılı başlatmadan sonra iki ayrı instance/replica arasında bir oturumun (login cookie) ve bir antiforgery token'ının geçerli kaldığını doğrulayın; bu, key-ring'in gerçekten paylaşıldığının kanıtıdır.

Doğrulama testleri: `tests/AETKAHVE.UnitTests/ProductionSecurityOptionsValidatorTests.cs`, `tests/AETKAHVE.UnitTests/ProductionReadinessTests.cs`, `tests/AETKAHVE.UnitTests/CommerceProviderTests.cs` ve `tests/AETKAHVE.IntegrationTests/ProductionSecurityInfrastructureTests.cs`, eksik Production config ile hem `DataProtectionKeyRingOptions` hem `SmtpOptions` zincirinin gerçekten fail-closed olduğunu (host hiç başlamadan) kanıtlar.
