# AFK Client Expiry ve Cross-Tab Contract Request

Durum: Ajan 1/Ajan 2 ve Coordinator incelemesine hazır

Kaynak branch: `agent/codex-architecture-security`

Kapsam: `src/AETKAHVE.Web/wwwroot/js/admin/idle-session.js`; backend AFK route ve JSON sözleşmesi değişmez.

## Bulgu

- Runtime `data-session-logout-url` değerini okuyor ancak `logoutUrl` değişkenini hiç kullanmıyor.
- İstemci sayacı sıfıra indiğinde doğrudan login sayfasına yönleniyor. Login route’u anonymous olduğundan management cookie doğrulaması o istekte çalışmayabilir; persistent cookie, sonraki korumalı management isteğine kadar tarayıcıda kalabilir.
- Sekmeler arasında logout/expiry/keep-alive bildirimi yok; her sekme yalnız kendi 30 saniyelik status poll’una dayanıyor.

## İstenen frontend davranışı

1. Yerel sayaç sıfıra indiğinde antiforgery header ve `credentials: same-origin` ile `logoutUrl` adresine POST gönder; cevap `401` olsa bile `Set-Cookie` silme işleminin tarayıcı tarafından uygulanmasına fırsat ver, ardından login’e yönlen.
2. Kullanıcının açık logout’u, AFK expiry ve başarılı keep-alive olaylarını `BroadcastChannel` veya güvenli `storage` event’iyle aynı portalın diğer sekmelerine bildir.
3. Cross-tab mesajı yalnız UI/timer senkronizasyonudur; server status/session kaydı nihai gerçek olmaya devam eder.
4. Ağ hatasında sonsuz bekleme yapma; kısa timeout sonrasında login’e yönlen ve bir sonraki korumalı istekte backend’in cookie validation/silme davranışına güven.

## Backend kanıtı

- `/admin/logout` ve `/superadmin/logout` antiforgery korumalıdır.
- AFK/revoke/security-stamp validation başarısızlığında ilgili cookie açıkça silinir.
- `Management_logout_deletes_the_persistent_authentication_cookie` integration testi logout endpoint’inin persistent cookie’yi sildiğini doğrular.

## Kabul kriterleri

- Süre dolduğunda logout POST’u ağ panelinde görülür ve management cookie silinir.
- Bir sekmede logout veya expiry diğer sekmeleri gecikmeden login’e yönlendirir.
- Status polling aktivite sayılmaz; bilinçli keep-alive mevcut backend sözleşmesini kullanır.
- Reduced motion veya JavaScript hatası backend AFK enforcement’ını devre dışı bırakmaz.
