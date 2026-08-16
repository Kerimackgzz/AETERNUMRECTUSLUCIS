# Contract Request — Koleksiyon kapak görseli, açıklama ve ürün sayısı

**Ajan:** Claude (Design/Frontend — Koleksiyonlar sayfası redesign'ı)
**Tarih:** 2026-08-16
**Konu:** `Category` için kapak görseli + açıklama alanı, admin yükleme UI'ı, opsiyonel ürün sayısı projeksiyonu

## İstek

`/categories` (Koleksiyonlar) sayfasını büyük editoryal `CollectionCard`'larla yeniden tasarladım. Zincirde (`Category` domain entity → `CatalogQueryService.GetCategoriesAsync` → `CatalogLookupItem(Id, Name, Slug)` → `CategoryListViewModel`) **hiçbir yerde** görsel, açıklama veya ürün sayısı alanı yok — sadece `Id`/`Name`/`Slug`. Frontend'de bu alanları uydurmadım; kendi sahipliğim (`AETKAHVE.Web`) içinde yeni bir sunum ViewModel'i (`CollectionCardViewModel`) ekledim ve `ImageUrl`/`Description`/`ProductCount` şimdilik `null` bırakıldı — kart bileşeni bu alanlar `null` iken de (markaya uygun bir fallback poster ile) iyi görünüyor, alanlar dolduğunda otomatik devreye girecek.

İhtiyaç duyulanlar:

1. **`Category` entity'sine kapak görseli** (`ImageStorageKey` veya `ImageUrl` — projedeki mevcut `IFileStorageService`/`LocalFileStorageService` deseniyle tutarlı, `ProductImage` için kullanılan yaklaşımın aynısı önerilir) + gerekli migration.
2. **`Category` entity'sine kısa açıklama** (`Description`, makul bir `MaxLength`, ör. 240) + migration. (Ürün sayısı için yeni bir kolon gerekmez — bkz. madde 4.)
3. **Admin katalog UI'sinde** (`Areas/Admin/Views/Catalog/_LookupGroup.cshtml` + `AdminCatalogLookupInput`) kategori satırlarına: görsel önizleme, dosya seçme/yükleme (mevcut ürün görseli validasyon kurallarıyla tutarlı — boyut/uzantı/içerik-tipi kontrolü), mevcut görseli değiştirme ve kaldırma aksiyonu. `AdminCatalogLookupInput`'a `Description` alanı da eklenmesi gerekecek.
4. **(Nice-to-have, "uygunsa")** `CatalogQueryService.GetCategoriesAsync`'in ürün sayısını da projekte etmesi (`Products.Count(p => p.CategoryId == c.Id && p.IsActive)` gibi bir join/count — yeni kolon gerektirmez, yalnızca sorgu değişikliği). Bu olmadan da kart tasarımı sorunsuz çalışır (metadata satırı basitçe gösterilmez).

## Frontend tarafı zaten hazır

- `src/AETKAHVE.Web/Models/CommerceViewModels.cs`: `CollectionCardViewModel(Guid Id, string Name, string Slug, string? ImageUrl = null, string? Description = null, int? ProductCount = null)`.
- `src/AETKAHVE.Web/Controllers/ProductsController.cs` (`CategoriesController.Index`): şu an `CatalogLookupItem` → `CollectionCardViewModel` maplerken üç yeni alanı `null` bırakıyor.
- `src/AETKAHVE.Web/Views/Categories/Index.cshtml`: `category.ImageUrl`/`Description`/`ProductCount` için zaten koşullu render var (`@if (!string.IsNullOrWhiteSpace(...))` / `@if (... is not null)`) — `null` değilse otomatik görünür, backend hazır olduğunda frontend'de ek değişiklik gerekmez.

Backend tarafı `CatalogLookupItem`'a (`AETKAHVE.Application/Commerce/CommerceModels.cs`) aynı üç alanı (nullable, mevcut çağrı yerlerini bozmayacak şekilde varsayılan değerli) eklerse, ben (frontend) `CategoriesController.Index`'teki mapleme satırını `c.ImageUrl`/`c.Description`/`c.ProductCount` okuyacak şekilde tek satırlık bir düzenlemeyle güncelleyebilirim.

## Neden

Kullanıcı talebi açıkça "alan mevcut değilse frontend'de sahte property üretme, backend talebi oluştur" yönündeydi — bu dosya o talebi karşılıyor. Sayfa bugün (alanlar `null` iken) zaten üretim kalitesinde görünüyor; bu yalnızca gelecekteki bir zenginleştirme.
