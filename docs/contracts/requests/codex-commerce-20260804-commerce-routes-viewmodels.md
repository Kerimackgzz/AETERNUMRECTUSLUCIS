# Commerce Route ve ViewModel Contract Request

Durum: Coordinator incelemesine hazır  
Kaynak branch: `agent/codex-commerce`  
Kapsam: Yalnız commerce modülü; dondurulmuş auth, route ve ortak ViewModel sözleşmeleri değiştirilmedi.

## Ortak davranış

- Unsafe MVC istekleri global `AutoValidateAntiforgeryToken` filtresini kullanır. Doğrulanmış provider callback POST'u bunun tek istisnasıdır.
- Customer route'ları `CustomerOnly`, admin route'ları mevcut `AdminArea` policy'siyle korunur.
- Customer kaynak sorguları her zaman authenticated `Guid UserId` ile filtrelenir; bulunamayan yabancı kaynak için veri sızdırmadan `404`/başarısız sonuç döner.
- Mutation JSON şekli: `CommerceMutationResponse(bool Success, string Message, int? CartItemCount, decimal? Subtotal, decimal? GrandTotal, object? Data)`.
- `HomePageViewModel` ve `ProductCardViewModel` şekilleri aynen korunmuştur. Kart URL'leri `AddToCartUrl`, `ToggleFavoriteUrl`, `DetailUrl` alanlarına server-side yazılır.

## Public route'lar

| Method | Route | Action / Model |
|---|---|---|
| GET | `/products` | `ProductListViewModel` |
| GET | `/products/{slug}` | `ProductDetailPageViewModel` |
| GET | `/search` | `ProductListViewModel` |
| GET | `/categories/{slug}` | `ProductListViewModel` |
| GET | `/campaigns` | `CampaignListViewModel` |
| GET | `/cart` | `CartPageViewModel` |
| POST | `/cart/items` | `AddCartItemInput` |
| POST | `/cart/items/{itemId}/quantity` | `UpdateCartQuantityInput` |
| POST | `/cart/items/{itemId}/remove` | mutation response |
| POST | `/cart/clear` | mutation response |
| POST | `/cart/coupon` | `CouponInput` |
| POST | `/cart/coupon/remove` | mutation response |
| POST | `/contact` | `ContactInputModel` |
| GET/POST | `/payments/{provider}/callback` | provider doğrulaması sonrası `CheckoutCompletionResult` |

Misafir sepet cookie'si `AETKAHVE.GuestCart`; Data Protection ile korunur, `HttpOnly`, `SameSite=Lax`, HTTPS'te `Secure` ve configuration tabanlı ömür kullanır. İlk authenticated commerce/home action'ında bir kez kullanıcı sepetine merge edilir.

## Customer route'ları

| Method | Route | Action / Model |
|---|---|---|
| GET/POST | `/favorites`, `/favorites/{productId}/toggle` | `FavoritePageViewModel`, mutation response |
| GET/POST | `/checkout` | `CheckoutPageViewModel`, `CheckoutInput` |
| GET/POST | `/account/addresses`, `/account/addresses/{id}/delete` | `AddressListViewModel`, `AddressInputModel` |
| GET | `/account/orders`, `/account/orders/{id}` | `OrderListViewModel`, `OrderDetailViewModel` |
| POST | `/account/orders/{id}/cancel` | refund-aware cancellation |
| GET | `/account/invoices`, `/account/invoices/{id}/download` | `InvoiceListViewModel`, PDF file |
| GET/POST | `/account/returns` | `ReturnListViewModel`, `ReturnInputModel` |
| GET/POST | `/account/reviews`, `/account/reviews/{id}/delete` | `ReviewListViewModel`, `ReviewInputModel` |
| GET/POST | `/account/notifications`, `/account/notifications/{id}/read`, `/account/notifications/read-all` | notification list / mutation response |

## Admin route'ları

| Route family | GET modeli | Mutation'lar |
|---|---|---|
| `/admin/products` | `ProductListViewModel` | product save, stock adjustment |
| `/admin/catalog` | `AdminCatalogViewModel` | lookup create/update |
| `/admin/orders` | `PagedResult<OrderSummary>` | status transition |
| `/admin/shipments` | `AdminShipmentListViewModel` | create, track, cancel |
| `/admin/invoices` | `AdminInvoiceListViewModel` | PDF download |
| `/admin/returns` | `AdminReturnListViewModel` | status/refund/restock decision |
| `/admin/campaigns` | `AdminCampaignListViewModel` | campaign + product/category targets save |
| `/admin/coupons` | `AdminCouponListViewModel` | coupon save |
| `/admin/reviews` | `AdminReviewListViewModel` | moderation |
| `/admin/messages` | `AdminMessageListViewModel` | status update |
| `/admin/reports` | `SalesReport` | UTF-8 CSV export at `/admin/reports/sales.csv` |

## Module-specific model özeti

- `ProductListViewModel`: `PagedResult<ProductSummary> Products`, `CatalogLookupSet Lookups`, `ProductQuery Query`.
- `ProductDetailPageViewModel`: `ProductDetails Product`.
- `CartPageViewModel`: `CartSummary Cart`.
- `CheckoutPageViewModel`: `CartSummary Cart`, `IReadOnlyList<AddressDetails> Addresses`, `string IdempotencyKey`.
- Liste modelleri, ilgili `PagedResult<T>` değerini tek property olarak taşır.
- `CatalogLookupSet`: categories, brands, coffee types, bean types, roast levels ve origins koleksiyonları.

## Razor handoff

Controller ve modeller hazırdır; bu branch Razor markup oluşturmaz. Ajan 1'in route adıyla eşleşen standart view klasörlerini eklemesi ve unsafe fetch/form çağrılarında frozen CSRF meta sözleşmesini kullanması beklenir. Tam browser smoke testi bu view'lar integration'a alındıktan sonra çalıştırılmalıdır.
