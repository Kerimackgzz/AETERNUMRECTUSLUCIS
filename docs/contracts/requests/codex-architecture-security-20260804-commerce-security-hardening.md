# Commerce Security Hardening Contract Request

Durum: Contact limiti Ajan 3 branch'inde uygulandı; payment/webhook/proxy maddeleri Ajan 4 ve Coordinator incelemesinde

Kaynak branch: `agent/codex-architecture-security`

Kapsam: Commerce sahipliğindeki production provider ve public abuse sınırları. Bu istek migration veya frozen ViewModel/route değişikliği gerektirmez.

## Bulgular

1. `Payment:Provider` varsayılanı ve örnek production ayarı `Mock`; DI içinde yalnız `MockPaymentGateway` kayıtlı. Mock callback, bilinen in-memory referans için `fail`/`cancel` dışındaki status değerlerini başarılı kabul eder. Bu davranış Development/Testing için uygundur, Production için değildir.
2. `POST /payments/{provider}/callback` antiforgery istisnasıdır. Gerçek sağlayıcıya geçildiğinde istisna ancak provider imzası/MAC, timestamp-replay penceresi, amount/currency ve idempotency doğrulamasından sonra korunmalıdır.
3. Public `POST /contact` için Ajan 3 security pipeline'ında configuration tabanlı, IP-bölümlü fixed-window limiter eklendi. İleri düzey bot/spam yükü için gerektiğinde CAPTCHA/honeypot adaptörü ayrıca değerlendirilebilir.
4. Ajan 3 rate-limit partition’ları `RemoteIpAddress` kullanır. Reverse proxy arkasında güvenilir proxy/network listesi Coordinator tarafından yapılandırılmadan `X-Forwarded-For` doğrudan kabul edilmemelidir.

## İstenen uygulama

- Production başlangıcında Mock payment provider seçiliyse fail-fast validation yap veya gerçek provider adaptörü kaydet.
- Gerçek webhook doğrulamasını provider-specific abstraction içinde uygula; imza/timestamp/replay testleri ekle.
- Contact mutation limiti `Security:ContactRequestsPerMinute` ile Ajan 3 tarafından sağlandı; frontend veya commerce controller değişikliği gerektirmez.
- Deployment katmanında trusted proxy listesini açıkça tanımla; güvenilmeyen forwarded header değerlerini IP partition anahtarı yapma.

## Kabul kriterleri

- Production yanlışlıkla Mock payment ile ayağa kalkmaz.
- Sahte veya replay edilmiş callback sipariş/ödeme durumunu değiştiremez.
- Contact limiti aşıldığında `429` ve `Retry-After` döner.
- Mevcut checkout idempotency, amount/currency ve antiforgery testleri geçmeye devam eder.
