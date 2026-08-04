# Shared ViewModel Contracts — Frozen

## HomePageViewModel

- `IReadOnlyList<ProductCardViewModel> FeaturedProducts`
- `string HeroFrameManifestUrl`
- `string HeroPosterUrl`
- `string HeroTitle`
- `string HeroSubtitle`
- `string HeroAccessibilityDescription`
- `bool IsReducedMotionFallbackAvailable`

## ProductCardViewModel

- `Guid Id`
- `string Name`, `Slug`
- `string PrimaryImageUrl`, `PrimaryImageAlt`
- `string CategoryName`, `OriginName`, `RoastLevelName`
- `decimal DisplayPrice`, `decimal? OriginalPrice`
- `bool IsDiscounted`, `IsInStock`, `IsFavorite`
- `string AddToCartUrl`, `ToggleFavoriteUrl`, `DetailUrl`

## Account Modelleri

- `LoginViewModel`: `Email`, `Password`, `RememberMe=false`, `ReturnUrl`
- `AdminLoginViewModel`: `LoginViewModel` sözleşmesi
- `SuperAdminLoginViewModel`: `LoginViewModel` sözleşmesi
- `RegisterViewModel`: `FirstName`, `LastName`, `Email`, `Password`, `ConfirmPassword`, `AcceptPrivacyTerms`
- `ForgotPasswordViewModel`: `Email`
- `ResetPasswordViewModel`: `Email`, `Token`, `Password`, `ConfirmPassword`

## Presentation Modelleri

- `DashboardSummaryViewModel`: `Title`, `IReadOnlyList<StatusPresentationViewModel> Statuses`
- `StatusPresentationViewModel`: `Key`, `Label`, `Value`, `Kind`

Property adı veya tipi değiştirilemez. Ek ortak alan için `docs/contracts/requests/<agent>-<yyyyMMdd>-<konu>.md` oluşturulur. Module-specific modeller ilgili modül sahibince eklenebilir.
