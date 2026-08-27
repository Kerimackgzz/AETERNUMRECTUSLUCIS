using AETKAHVE.Application.Commerce;
using AETKAHVE.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.Commerce;

public sealed class DevelopmentDemoReviewProvider(
    IHostEnvironment environment,
    IOptions<CommerceOptions> commerceOptions)
{
    private static readonly DateTimeOffset BaseDate = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    public IReadOnlyList<ProductReviewDetails> GetForProduct(Guid productId, string productName)
    {
        if (!environment.IsDevelopment() || !commerceOptions.Value.SeedDevelopmentData)
        {
            return [];
        }

        var normalizedName = string.IsNullOrWhiteSpace(productName) ? "Bu kahve" : productName.Trim();
        var productOffset = productId.ToByteArray()[0] % 7;
        return
        [
            new ProductReviewDetails(
                "Ayşe K.",
                5,
                $"{normalizedName} aromasındaki denge ve temiz bitişiyle beklentimi karşıladı. Paketi açar açmaz tazeliği hissediliyor.",
                BaseDate.AddDays(-productOffset),
                true),
            new ProductReviewDetails(
                "Mert D.",
                5,
                $"{normalizedName} günlük kahve rutinime çok yakıştı. Kokusu, gövdesi ve fincandaki dengesi gerçekten başarılı.",
                BaseDate.AddDays(-productOffset - 3),
                true),
            new ProductReviewDetails(
                "Selin A.",
                4,
                $"{normalizedName} genel olarak oldukça keyifliydi. Lezzeti hoş; damak tadıma göre biraz daha yoğun olabilirdi.",
                BaseDate.AddDays(-productOffset - 7),
                true),
        ];
    }
}
