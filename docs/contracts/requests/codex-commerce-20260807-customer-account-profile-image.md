# Contract Request — Customer account profile image

Tarih: 2026-08-07
Sahip: Ajan 4 / Commerce-AppDbContext (ADR-008)

## Talep

Authenticated müşteri hesap merkezinde owner-only profil fotoğrafı desteği için `ApplicationUser` modeline aşağıdaki nullable alan eklenmiştir:

- `ProfileImageStorageKey`: `string?`, azami 512 karakter.

Identity/authentication tablo ilişkileri, cookie şemaları, roller ve mevcut route sözleşmeleri değiştirilmemiştir. Alan yalnız güvenli dosya deposundaki rastgele storage key'ini tutar; görsel public static path altında sunulmaz.

## Migration

- `AddCustomerAccountProfileImage`
- `AspNetUsers.ProfileImageStorageKey nvarchar(512) NULL`
- Geri alma işlemi yalnız bu kolonu kaldırır.

## Güvenlik davranışı

- Fotoğraf yalnız kullanıcı kimliği parametresi almayan `GET /account/profile/photo` owner endpoint'inden açılır.
- JPEG/PNG/WebP, magic-byte/MIME/uzantı eşleşmesi ve 2 MiB üst sınırı uygulanır.
- Yeni dosya kaydedilip kullanıcı güncellemesi başarısız olursa yeni dosya silinir; başarılı replacement sonrasında eski dosya kaldırılır.
