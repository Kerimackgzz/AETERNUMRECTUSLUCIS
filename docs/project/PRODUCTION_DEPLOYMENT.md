# Production Deployment Gate

Bu belge production ortamında gereken kalıcı anahtar, e-posta/outbox ve dış sağlayıcı kapılarını tanımlar. Repository'ye parola, SMTP credential'ı, sertifika private key'i veya gerçek sağlayıcı secret'ı eklenmez. Secret değerleri deployment platformunun secret store'u üzerinden environment variable olarak verilmelidir.

## Data Protection key-ring

Uygulama auth cookie'leri, antiforgery verileri ve korumalı misafir sepeti cookie'si için aynı kalıcı key-ring'i kullanır:

```text
DataProtection__ApplicationName=AETKAHVE
DataProtection__KeyRingPath=/var/lib/aetkahve/data-protection-keys
DataProtection__CertificateThumbprint=<deployment-certificate-thumbprint>
```

- `KeyRingPath` production'da zorunlu ve absolute bir durable mount path olmalıdır; relative path startup validation tarafından reddedilir.
- Aynı deployment'ın bütün replica'ları aynı `ApplicationName` ve paylaşılan key-ring'i kullanmalıdır.
- Persist edilen XML anahtarları uygulama seviyesinde sertifikayla şifrelenir. Sertifika private key'i repository'ye konmaz; CurrentUser/LocalMachine personal store'da `CertificateThumbprint` ile veya secret mount'taki PFX üzerinden sağlanır.
- PFX kullanılıyorsa `DataProtection__CertificatePath` absolute olmalı ve `DataProtection__CertificatePassword` secret store üzerinden verilmelidir. Thumbprint ve PFX aynı anda yapılandırılamaz.
- Volume kalıcı, yalnız uygulama kimliğinin yazabildiği, altyapı seviyesinde de şifreli ve yedeklenmiş olmalıdır.
- Key-ring kaybı mevcut auth/antiforgery/cart cookie'lerini ve bazı Identity tokenlarını geçersiz kılar. Eski key'ler key lifecycle politikası dışında elle silinmemelidir.
- Eksik key-ring path, erişilemeyen dizin, bulunamayan/private-key içermeyen/süresi geçersiz sertifika production startup'ını açık bir configuration hatasıyla durdurur.

## Production e-posta ve outbox

Production mock provider ile başlamaz. Aşağıdaki public olmayan değerler deployment secret/config katmanında açıkça sağlanır:

```text
Notifications__UseMockProviders=false
Notifications__EmailDeliveryEnabled=true
Notifications__SmsDeliveryEnabled=false
Notifications__WorkerEnabled=true
Notifications__MaximumAttempts=5
Notifications__BatchSize=20
Notifications__PollIntervalSeconds=5
Notifications__ProcessingLeaseSeconds=300
Notifications__MaximumRetryDelayMinutes=60

Smtp__Host=smtp.example.com
Smtp__Port=587
Smtp__UseSsl=true
Smtp__UserName=<secret-store-reference>
Smtp__Password=<secret-store-reference>
Smtp__FromAddress=noreply@example.com
Smtp__FromName=AETERNUM RECTUS LUCIS
Smtp__TimeoutSeconds=30
```

SMTP relay kimlik doğrulaması istemiyorsa `UserName` ve `Password` birlikte boş bırakılabilir; yalnız birinin verilmesi validation hatasıdır. `.invalid` sender adresi, SSL kapalı yapılandırma, eksik host ve Production mock seçimi startup sırasında reddedilir.

Identity confirmation ve password-reset mesajları web request'i içinde SMTP'ye gönderilmez. Önce SQL outbox'a yazılır, sonra commerce mesajlarıyla aynı worker tarafından teslim edilir. Bu sayede restart sonrası kayıt kaybolmaz ve dış sağlayıcı geçici hataları bounded exponential retry alır.

Outbox davranışı:

- Batch boyutu, lease süresi, deneme sayısı ve azami retry gecikmesi configuration ile sınırlıdır.
- Claim sırasında `Guid` concurrency token her save'de döndürülür; aynı kaydı gören ikinci worker stale token ile gönderime geçemez.
- Süresi dolmuş `Processing` lease'leri yeniden alınabilir. Sağlayıcı sonucu alındıktan sonra host shutdown başlasa bile sonuç kaydı tamamlanmaya çalışılır.
- Son denemeden sonra kayıt `Failed` kalır ve `NextAttemptAtUtc` temizlenir. Operasyon ekibi terminal failure sayısını izlemeli ve güvenli bir admin/runbook süreci olmadan attempt count'u elle değiştirmemelidir.
- SMTP doğası gereği tam exactly-once garantisi vermez: sağlayıcı mesajı kabul ettikten hemen sonra process ölürse lease sonunda tekrar gönderim olabilir. Template'ler bu olası duplicate teslimi tolere etmelidir.
- En az bir replica'da `WorkerEnabled=true` olmalıdır. Worker kapalı replica'lar web trafiği alabilir, ancak bütün replica'larda kapatılırsa outbox birikir.
- Production SMS adapter'ı kayıtlı olmadığı için `SmsDeliveryEnabled=true` validation tarafından reddedilir.

Development ve Testing ortamları configuration `UseMockProviders=false` dese bile güvenlik için deterministik mock e-posta/SMS ve in-memory Identity sender kullanır; testler hiçbir gerçek dış gönderim yapmaz.

## Açık production blocker'ları

Payment ve shipping için repository'de yalnız deterministik Mock adapter'lar vardır. Config'e farklı bir provider adı yazmak gerçek adapter kaydetmez. Bu nedenle Production startup validation, gerçek `IPaymentGateway` ve `IShippingProvider` implementasyonları DI'a eklenip doğrulama politikası güncellenene kadar her iki alan için fail-closed davranır.

Bu guard kaldırılmadan önce:

1. Sağlayıcı credential'ları secret store'dan alınan gerçek adapter'lar eklenmeli.
2. Callback signature/origin doğrulaması ve provider idempotency sözleşmesi entegrasyon testleriyle kanıtlanmalı.
3. Shipping create/track/cancel hata ve retry semantiği test edilmeli.
4. Production config'teki provider adı resolve edilen adapter'ın `ProviderName` değeriyle eşleşmeli.

## Deployment doğrulama sırası

1. SQL Server migration'larını idempotent script ile uygulayın ve yedeği doğrulayın.
2. Paylaşılan Data Protection volume'unu ve key-encryption sertifikasını mount/provision edin; uygulama identity'sinin read/write/private-key erişimini doğrulayın.
3. SMTP/notification config ve secret'larını sağlayın; gerçek alıcıya gönderim yapmadan startup validation'ı çalıştırın.
4. Gerçek payment/shipping adapter blocker'larını kapatın.
5. `/health/live` ve `/health/ready` kapılarını geçirin.
6. Outbox pending/processing/terminal-failed metrikleri ile log retention/alert kurallarını etkinleştirin.
