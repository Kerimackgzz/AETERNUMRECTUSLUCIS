namespace AETKAHVE.Web.Models;

public sealed class HomePageViewModel
{
    public IReadOnlyList<ProductCardViewModel> FeaturedProducts { get; init; } = [];

    public string HeroFrameManifestUrl { get; init; } = "/frames/home/manifest.json";

    public string HeroPosterUrl { get; init; } = "/frames/home/desktop/poster.webp";

    public string HeroTitle { get; init; } = "AETERNUM RECTUS LUCIS";

    public string HeroSubtitle { get; init; } = "Kahvenin zamansız ritüeli.";

    public string HeroAccessibilityDescription { get; init; } = "Kahve çekirdeklerinin akışını gösteren sinematik sahne.";

    public bool IsReducedMotionFallbackAvailable { get; init; }
}

public sealed class AboutPageViewModel
{
    public string StoryFrameManifestUrl { get; init; } = "/frames/about/manifest.json";

    public string StoryPosterUrl { get; init; } = "/frames/about/desktop/poster.webp";

    public string StoryAccessibilityDescription { get; init; } = "Bir güneşin yavaşça bir kahve çekirdeğine dönüştüğü sinematik sahne.";

    public bool IsReducedMotionFallbackAvailable { get; init; }
}

public sealed class ProductCardViewModel
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string PrimaryImageUrl { get; init; } = string.Empty;

    public string PrimaryImageAlt { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;

    public string OriginName { get; init; } = string.Empty;

    public string RoastLevelName { get; init; } = string.Empty;

    public decimal DisplayPrice { get; init; }

    public decimal? OriginalPrice { get; init; }

    public bool IsDiscounted { get; init; }

    public bool IsInStock { get; init; }

    public bool IsFavorite { get; init; }

    public string AddToCartUrl { get; init; } = string.Empty;

    public string ToggleFavoriteUrl { get; init; } = string.Empty;

    public string DetailUrl { get; init; } = string.Empty;
}

