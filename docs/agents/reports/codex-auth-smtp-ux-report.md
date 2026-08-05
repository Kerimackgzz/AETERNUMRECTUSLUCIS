# Ajan 3 — Auth SMTP ve Form UX Raporu

## Sonuç

- Development ortamında gerçek SMTP teslimi güvenli ve açık onaylı hale getirildi. Varsayılan davranış mock olarak kaldı; Testing ortamında dış teslimat hiçbir ayarla açılamaz.
- SMTP parolası için repository içinde alan/değer oluşturulmadı. Web projesine `UserSecretsId` eklendi ve README'de yalnız örnek değerlerle kurulum anlatıldı.
- Doğrulanmamış kullanıcılar için `/account/resend-confirmation` GET/POST akışı eklendi. Akış gerçek Identity tokenı üretir, kullanıcı varlığını ifşa etmeyen genel cevap verir ve mevcut rate-limit politikasını kullanır.
- Customer kayıt/giriş/reset, Admin giriş ve SuperAdmin giriş alanlarına açıklayıcı placeholder'lar eklendi.
- Parola alanlarına klavye ve ekran okuyucu uyumlu göz düğmesi eklendi. Düğme, parola metni ile göz ikonunu ve `aria-label`/`aria-pressed` durumunu birlikte değiştirir.
- Kayıt ve parola sıfırlama ekranlarında uygulanan Identity kuralları görünür hale getirildi: en az 12 karakter, küçük harf, büyük harf, rakam, özel karakter ve en az 4 benzersiz karakter.

## Güvenlik Kararları

- Development dış teslimatı için hem `Notifications:UseMockProviders=false` hem `Notifications:AllowExternalDeliveryInDevelopment=true` gerekir.
- Testing her koşulda mock sağlayıcı kullanır; test sırasında yanlışlıkla gerçek e-posta gönderilemez.
- SMTP secret'ları loglanmaz, kaynak dosyaya yazılmaz ve rapora dahil edilmez.
- Yeniden doğrulama isteği hesabın kayıtlı olup olmadığını veya durumunu cevapta açıklamaz.

## Doğrulama

- `dotnet build AETKAHVE.sln --no-restore`: başarılı, 0 uyarı, 0 hata.
- `dotnet test AETKAHVE.sln --no-build`: başarılı; Unit 56/56, Integration 77/77.
- `npm run test:frontend`: başarılı; 6/6.

Gerçek SMTP teslimi, kullanıcıya ait sağlayıcı secret'ları local user-secrets'e girildikten sonra uçtan uca sınanabilir. Secret değerleri bu teslimin parçası değildir.
