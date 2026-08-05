using AETKAHVE.Application.Commerce;
using AETKAHVE.Infrastructure.Commerce;
using AETKAHVE.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.DependencyInjection;

public static class CommerceModuleExtensions
{
    public static IServiceCollection AddCommerceModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<CommerceOptions>().Bind(configuration.GetSection(CommerceOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<PaymentOptions>, PaymentOptionsValidator>();
        services.AddOptions<PaymentOptions>().Bind(configuration.GetSection(PaymentOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<ShippingOptions>().Bind(configuration.GetSection(ShippingOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<InvoiceOptions>().Bind(configuration.GetSection(InvoiceOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<NotificationOptions>().Bind(configuration.GetSection(NotificationOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<FileStorageOptions>().Bind(configuration.GetSection(FileStorageOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SmtpOptions>().Bind(configuration.GetSection(SmtpOptions.SectionName)).ValidateDataAnnotations();

        services.AddScoped<ICatalogQueryService, CatalogQueryService>();
        services.AddScoped<IDiscountEngine, DiscountEngine>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IFavoriteService, FavoriteService>();
        services.AddScoped<IAddressService, AddressService>();
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IReturnService, ReturnService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<NotificationDeliveryProcessor>();
        services.AddScoped<IAdminCommerceService, AdminCommerceService>();
        services.AddScoped<INotificationQueue, NotificationQueue>();
        services.AddSingleton<MockPaymentGateway>();
        services.AddSingleton<IPaymentGateway>(provider => provider.GetRequiredService<MockPaymentGateway>());
        services.AddSingleton<IPaymentWebhookReplayStore, InMemoryPaymentWebhookReplayStore>();
        services.AddSingleton<MockPaymentWebhookVerifier>();
        services.AddSingleton<DisabledPaymentWebhookVerifier>();
        services.AddSingleton<IPaymentWebhookVerifier>(provider => provider.GetRequiredService<MockPaymentWebhookVerifier>());
        services.AddSingleton<IPaymentWebhookVerifier>(provider => provider.GetRequiredService<DisabledPaymentWebhookVerifier>());
        services.AddSingleton<MockShippingProvider>();
        services.AddSingleton<IShippingProvider>(provider => provider.GetRequiredService<MockShippingProvider>());
        services.AddSingleton<MockEmailSender>();
        services.AddSingleton<MockSmsSender>();
        services.AddSingleton<UnconfiguredSmsSender>();
        services.AddSingleton<IEmailSender>(provider =>
            configuration.GetValue<bool>($"{NotificationOptions.SectionName}:UseMockProviders", true)
                ? provider.GetRequiredService<MockEmailSender>()
                : ActivatorUtilities.CreateInstance<SmtpEmailSender>(provider));
        services.AddSingleton<ISmsSender>(provider =>
            configuration.GetValue<bool>($"{NotificationOptions.SectionName}:UseMockProviders", true)
                ? provider.GetRequiredService<MockSmsSender>()
                : provider.GetRequiredService<UnconfiguredSmsSender>());
        services.AddSingleton<IInvoicePdfGenerator, InvoicePdfGenerator>();
        services.AddSingleton<IInvoiceStorage, LocalInvoiceStorage>();
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();
        services.AddHostedService<CommerceSeedHostedService>();
        services.AddHostedService<NotificationDeliveryWorker>();
        return services;
    }
}

