# Contract Request — OrderDetails.Items OrderItemId ve /contact GET rotası

Durum: Coordinator incelemesine hazır
Kaynak: Ajan 1 (`agent/claude-design-pages`)
Kapsam: Yalnızca iki küçük, geriye dönük uyumlu ek — mevcut frozen ViewModel/route sözleşmelerini bozmaz.

## 1. `OrderDetails.Items` (Application/Commerce/CommerceModels.cs) OrderItemId taşımıyor

`OrderDetails.Items` şu an `IReadOnlyList<InvoiceLine>` (Name, Sku, Quantity, UnitPrice, Discount, Tax, Total) — satırın gerçek `OrderItem.Id`'si yok.

Bu yüzden `Orders/Detail.cshtml`'de:
- "İade talebi oluştur" (`ReturnInputModel.Items[].OrderItemId`) ve
- "Yorum yap" (`ReviewInputModel.OrderItemId`)

formları kurulamıyor — hangi satırın iade/yorum edileceğini backend'e bildirecek bir kimlik yok. `Returns/Index.cshtml` ve `Reviews/Index.cshtml` şu an yalnızca mevcut kayıtları listeliyor; yeni talep oluşturma UI'ı bu yüzden eklenmedi.

**Talep:** `OrderDetails`'e satır bazlı `OrderItemId` ekleyen yeni bir record (örn. `OrderLineDetails(Guid OrderItemId, string Name, string Sku, int Quantity, decimal UnitPrice, decimal Discount, decimal Tax, decimal Total)`) veya mevcut `InvoiceLine`'a `OrderItemId` eklenmesi. `InvoiceLine` fatura PDF'inde de kullanıldığından yeni bir tip eklemek muhtemelen daha güvenli (mevcut kullanımları bozmaz).

## 2. `/contact` için GET rotası yok

`_Navbar.cshtml`, `/contact`'e bağlantı veriyor ancak `ContactController` yalnızca `[HttpPost] Submit` içeriyor — sayfayı görüntüleyecek bir `[HttpGet]` action yok. Şu an `/contact`'e gidiş 404 veriyor.

**Talep:** `ContactController`'a `[HttpGet] public IActionResult Index() => View(new ContactInputModel());` gibi bir action eklenmesi (route zaten `[Route("contact")]` olarak tanımlı, yalnızca GET action eksik).

## Etki

Her iki değişiklik de yalnızca ekleme niteliğinde; mevcut `CommerceMutationResponse`, `ReturnInputModel`, `ReviewInputModel`, frozen route/ViewModel sözleşmelerinde hiçbir şey değişmiyor.
