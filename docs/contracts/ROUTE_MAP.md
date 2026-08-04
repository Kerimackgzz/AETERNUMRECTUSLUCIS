# Route Map — Frozen Foundation Contract

| Route | Method | Scheme / Policy | Davranış |
|---|---|---|---|
| `/` | GET | Public | Ana sayfa ve hero hookları |
| `/account` | GET | `CustomerOnly` | Customer dashboard skeleton |
| `/account/login` | GET/POST | Public / Customer sign-in | Customer rolü dışında session açmaz |
| `/account/register` | GET/POST | Public | Customer oluşturur, e-posta doğrulaması ister |
| `/account/confirm-email` | GET | Public token | Identity e-posta tokenını doğrular |
| `/account/forgot-password` | GET/POST | Public | Hesap varlığını ifşa etmeyen cevap |
| `/account/reset-password` | GET/POST | Public token | Identity reset tokenıyla parola değiştirir |
| `/account/logout` | POST | `CustomerOnly` | Antiforgery zorunlu |
| `/account/access-denied` | GET | Public | Customer erişim reddi |
| `/admin` | GET | `AdminArea` | Admin veya SuperAdmin; anonim kullanıcı `/admin/login` |
| `/admin/login` | GET/POST | Public / Admin sign-in | Yalnız Admin rolü, POST rate limited |
| `/admin/session/status` | GET | `AdminArea` | AFK durumunu döner, aktiviteyi yenilemez |
| `/admin/session/keep-alive` | POST | `AdminArea` | Antiforgery zorunlu, aktiviteyi yeniler |
| `/admin/logout` | POST | `AdminArea` | Session revoke ve cookie temizleme |
| `/superadmin` | GET | `SuperAdminArea` | Yalnız SuperAdmin; anonim kullanıcı `/superadmin/login` |
| `/superadmin/login` | GET/POST | Public / SuperAdmin sign-in | Yalnız SuperAdmin rolü, POST rate limited |
| `/superadmin/session/status` | GET | `SuperAdminArea` | AFK durumunu döner, aktiviteyi yenilemez |
| `/superadmin/session/keep-alive` | POST | `SuperAdminArea` | Antiforgery zorunlu, aktiviteyi yeniler |
| `/superadmin/logout` | POST | `SuperAdminArea` | Session revoke ve cookie temizleme |
| `/health/live` | GET | Public | Uygulama liveness |
| `/health/ready` | GET | Public | SQL Server readiness; hassas detay döndürmez |

Public sayfalarda `/admin` veya `/superadmin` bağlantısı bulunamaz. Yeni veya değişen ortak route için contract request gerekir.
