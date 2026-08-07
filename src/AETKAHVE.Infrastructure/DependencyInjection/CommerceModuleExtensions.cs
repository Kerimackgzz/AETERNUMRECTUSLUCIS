using AETKAHVE.Application.Commerce;
using AETKAHVE.Application.Notifications;
using AETKAHVE.Infrastructure.Commerce;
using AETKAHVE.Infrastructure.Notifications;
using AETKAHVE.Infrastructure.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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

        services.AddDataProtection();

        services.AddSingleton<IValidateOptions<ShippingOptions>, ShippingOptionsValidator>();
        services.AddSingleton<IValidateOptions<NotificationOptions>, NotificationOptionsValidator>();
        services.AddSingleton<IValidateOptions<SmtpOptions>, SmtpOptionsValidator>();
        services.AddOptions<CommerceOptions>().Bind(configuration.GetSection(CommerceOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<PaymentOptions>, PaymentOptionsValidator>();
        services.AddOptions<PaymentOptions>().Bind(configuration.GetSection(PaymentOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<StripeOptions>, StripeOptionsValidator>();
        services.AddOptions<StripeOptions>().Bind(configuration.GetSection(StripeOptions.SectionName)).ValidateOnStart();
        services.AddOptions<ShippingOptions>().Bind(configuration.GetSection(ShippingOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<InvoiceOptions>().Bind(configuration.GetSection(InvoiceOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<NotificationOptions>().Bind(configuration.GetSection(NotificationOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<FileStorageOptions>().Bind(configuration.GetSection(FileStorageOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SmtpOptions>().Bind(configuration.GetSection(SmtpOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();

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
        services.AddScoped<ICustomerAccountQueryService, CustomerAccountQueryService>();
        services.AddScoped<ICustomerProfileService, CustomerProfileService>();
        services.AddScoped<NotificationDeliveryProcessor>();
        services.AddScoped<IAdminCommerceService, AdminCommerceService>();
        services.AddScoped<INotificationQueue, NotificationQueue>();
        services.AddSingleton<MockPaymentGateway>();
        services.AddSingleton<IPaymentGateway>(provider => provider.GetRequiredService<MockPaymentGateway>());
        services.AddSingleton<StripePaymentGateway>();
        services.AddSingleton<IPaymentGateway>(provider => provider.GetRequiredService<StripePaymentGateway>());
        services.AddSingleton<IPaymentWebhookReplayStore, InMemoryPaymentWebhookReplayStore>();
        services.AddSingleton<MockPaymentWebhookVerifier>();
        services.AddSingleton<DisabledPaymentWebhookVerifier>();
        services.AddSingleton<StripePaymentWebhookVerifier>();
        services.AddSingleton<IPaymentWebhookVerifier>(provider => provider.GetRequiredService<MockPaymentWebhookVerifier>());
        services.AddSingleton<IPaymentWebhookVerifier>(provider => provider.GetRequiredService<DisabledPaymentWebhookVerifier>());
        services.AddSingleton<IPaymentWebhookVerifier>(provider => provider.GetRequiredService<StripePaymentWebhookVerifier>());
        services.AddSingleton<MockShippingProvider>();
        services.AddSingleton<IShippingProvider>(provider => provider.GetRequiredService<MockShippingProvider>());
        services.AddSingleton<MockEmailSender>();
        services.AddSingleton<MockSmsSender>();
        services.AddSingleton<UnconfiguredSmsSender>();
        services.AddSingleton<IEmailSender>(provider =>
            NotificationProviderSelection.UseMockProviders(
                provider.GetRequiredService<IHostEnvironment>(),
                provider.GetRequiredService<IOptions<NotificationOptions>>().Value)
                ? provider.GetRequiredService<MockEmailSender>()
                : ActivatorUtilities.CreateInstance<SmtpEmailSender>(provider));
        services.AddSingleton<ISmsSender>(provider =>
            NotificationProviderSelection.UseMockProviders(
                provider.GetRequiredService<IHostEnvironment>(),
                provider.GetRequiredService<IOptions<NotificationOptions>>().Value)
                ? provider.GetRequiredService<MockSmsSender>()
                : provider.GetRequiredService<UnconfiguredSmsSender>());
        services.AddScoped<OutboxIdentityMessageSender>();
        services.RemoveAll<IIdentityMessageSender>();
        services.AddScoped<IIdentityMessageSender>(provider =>
            NotificationProviderSelection.UseMockProviders(
                provider.GetRequiredService<IHostEnvironment>(),
                provider.GetRequiredService<IOptions<NotificationOptions>>().Value)
                ? provider.GetRequiredService<InMemoryIdentityMessageSender>()
                : provider.GetRequiredService<OutboxIdentityMessageSender>());
        services.AddSingleton<IInvoicePdfGenerator, InvoicePdfGenerator>();
        services.AddSingleton<IInvoiceStorage, LocalInvoiceStorage>();
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();
        services.AddHostedService<CommerceSeedHostedService>();
        services.AddHostedService<NotificationDeliveryWorker>();
        return services;
    }
}

