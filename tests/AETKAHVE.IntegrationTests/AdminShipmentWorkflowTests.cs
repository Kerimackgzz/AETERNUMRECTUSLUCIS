using System.Net;
using System.Net.Http.Json;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Infrastructure.Identity;
using AETKAHVE.Infrastructure.Persistence;
using AETKAHVE.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AETKAHVE.IntegrationTests;

public sealed class AdminShipmentWorkflowTests(AeternumWebApplicationFactory factory)
    : IClassFixture<AeternumWebApplicationFactory>
{
    [Fact]
    public async Task Draft_is_created_inline_and_repeated_creation_is_idempotent_before_tracking()
    {
        var seeded = await SeedShipmentAsync();
        using var client = await CreateAdminClientAsync();
        var (page, token) = await FormClient.GetFormAsync(client, "/admin/shipments");
        var html = await page.Content.ReadAsStringAsync();
        var draftRow = ShipmentRow(html, seeded.OrderId);

        Assert.DoesNotContain("data-shipment-create-form", html, StringComparison.Ordinal);
        Assert.Contains("data-shipment-create", draftRow, StringComparison.Ordinal);
        Assert.Contains("data-shipment-note", draftRow, StringComparison.Ordinal);
        Assert.DoesNotContain("data-shipment-track", draftRow, StringComparison.Ordinal);
        Assert.DoesNotContain("data-shipment-cancel", draftRow, StringComparison.Ordinal);

        var auditCountBefore = await CountAuditsAsync(seeded.AdminId, "ShipmentCreated");
        using var created = await PostJsonAsync(client, "/admin/shipments", token, new
        {
            orderId = seeded.OrderId,
            note = "Kapıya bırakmayın",
            estimatedDeliveryDateUtc = (DateTimeOffset?)null,
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        using var repeated = await PostJsonAsync(client, "/admin/shipments", token, new
        {
            orderId = seeded.OrderId,
            note = "Bu not mevcut sonucu ezmemeli",
            estimatedDeliveryDateUtc = (DateTimeOffset?)null,
        });
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var shipment = await db.Shipments.SingleAsync(x => x.OrderId == seeded.OrderId);
            var order = await db.Orders.SingleAsync(x => x.Id == seeded.OrderId);
            Assert.Equal(seeded.ShipmentId, shipment.Id);
            Assert.Equal(ShipmentStatus.Created, shipment.Status);
            Assert.Equal(ShipmentStatus.Created, order.ShippingStatus);
            Assert.False(string.IsNullOrWhiteSpace(shipment.TrackingNumber));
            Assert.False(string.IsNullOrWhiteSpace(shipment.TrackingUrl));
            Assert.Equal("AETERNUM Mock Shipping", shipment.ShippingCompany);
            Assert.Equal("Kapıya bırakmayın", shipment.ShippingNote);
            Assert.Equal(1, await db.ShipmentStatusHistory.CountAsync(x => x.ShipmentId == shipment.Id));
        }
        Assert.Equal(auditCountBefore + 1, await CountAuditsAsync(seeded.AdminId, "ShipmentCreated"));

        var (createdPage, createdToken) = await FormClient.GetFormAsync(client, "/admin/shipments");
        var createdHtml = await createdPage.Content.ReadAsStringAsync();
        var createdRow = ShipmentRow(createdHtml, seeded.OrderId);
        Assert.DoesNotContain("data-shipment-create", createdRow, StringComparison.Ordinal);
        Assert.Contains("data-shipment-track", createdRow, StringComparison.Ordinal);
        Assert.Contains("data-shipment-cancel", createdRow, StringComparison.Ordinal);

        using var tracked = await PostWithoutBodyAsync(client, $"/admin/shipments/{seeded.OrderId}/track", createdToken);
        Assert.Equal(HttpStatusCode.OK, tracked.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(ShipmentStatus.Shipped, (await db.Shipments.SingleAsync(x => x.OrderId == seeded.OrderId)).Status);
            Assert.Equal(ShipmentStatus.Shipped, (await db.Orders.SingleAsync(x => x.Id == seeded.OrderId)).ShippingStatus);
            Assert.Equal(2, await db.ShipmentStatusHistory.CountAsync(x => x.ShipmentId == seeded.ShipmentId));
        }
    }

    [Fact]
    public async Task Draft_track_and_cancel_return_clear_conflicts_and_terminal_rows_hide_actions()
    {
        var draft = await SeedShipmentAsync();
        var delivered = await SeedShipmentAsync(ShipmentStatus.Delivered, "ARL-DELIVERED-TEST");
        using var client = await CreateAdminClientAsync();
        var (page, token) = await FormClient.GetFormAsync(client, "/admin/shipments");
        var html = await page.Content.ReadAsStringAsync();

        var terminalRow = ShipmentRow(html, delivered.OrderId);
        Assert.Contains("Tamamlandı", terminalRow, StringComparison.Ordinal);
        Assert.DoesNotContain("data-shipment-create", terminalRow, StringComparison.Ordinal);
        Assert.DoesNotContain("data-shipment-track", terminalRow, StringComparison.Ordinal);
        Assert.DoesNotContain("data-shipment-cancel", terminalRow, StringComparison.Ordinal);

        using var track = await PostWithoutBodyAsync(client, $"/admin/shipments/{draft.OrderId}/track", token);
        var trackBody = await track.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Conflict, track.StatusCode);
        Assert.Contains("henüz oluşturulmadı", trackBody, StringComparison.OrdinalIgnoreCase);

        using var cancel = await PostWithoutBodyAsync(client, $"/admin/shipments/{draft.OrderId}/cancel", token);
        var cancelBody = await cancel.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        Assert.Contains("henüz oluşturulmadı", cancelBody, StringComparison.OrdinalIgnoreCase);

        using var missing = await PostWithoutBodyAsync(client, $"/admin/shipments/{Guid.NewGuid()}/track", token);
        var missingBody = await missing.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode);
        Assert.Contains("Kargo kaydı bulunamadı", missingBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Parallel_creation_requests_leave_one_created_shipment_history_and_audit()
    {
        var seeded = await SeedShipmentAsync();
        using var firstClient = await CreateAdminClientAsync();
        using var secondClient = await CreateAdminClientAsync();
        var (firstPage, firstToken) = await FormClient.GetFormAsync(firstClient, "/admin/shipments");
        var (secondPage, secondToken) = await FormClient.GetFormAsync(secondClient, "/admin/shipments");
        firstPage.Dispose();
        secondPage.Dispose();
        var auditCountBefore = await CountAuditsAsync(seeded.AdminId, "ShipmentCreated");

        var firstRequest = PostJsonAsync(firstClient, "/admin/shipments", firstToken, new
        {
            orderId = seeded.OrderId,
            note = "İlk paralel istek",
            estimatedDeliveryDateUtc = (DateTimeOffset?)null,
        });
        var secondRequest = PostJsonAsync(secondClient, "/admin/shipments", secondToken, new
        {
            orderId = seeded.OrderId,
            note = "İkinci paralel istek",
            estimatedDeliveryDateUtc = (DateTimeOffset?)null,
        });
        var responses = await Task.WhenAll(firstRequest, secondRequest);
        foreach (var response in responses)
        {
            using (response)
            {
                Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
                Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
            }
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var shipment = await db.Shipments.SingleAsync(x => x.OrderId == seeded.OrderId);
        Assert.Equal(seeded.ShipmentId, shipment.Id);
        Assert.Equal(ShipmentStatus.Created, shipment.Status);
        Assert.False(string.IsNullOrWhiteSpace(shipment.TrackingNumber));
        Assert.Equal(1, await db.ShipmentStatusHistory.CountAsync(x => x.ShipmentId == seeded.ShipmentId));
        Assert.Equal(auditCountBefore + 1, await db.AuditLogs
            .CountAsync(x => x.AdminUserId == seeded.AdminId && x.ActionType == "ShipmentCreated"));
    }

    private async Task<(Guid OrderId, Guid ShipmentId, Guid AdminId)> SeedShipmentAsync(
        ShipmentStatus status = ShipmentStatus.Pending,
        string? trackingNumber = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var customer = Assert.IsType<ApplicationUser>(await userManager.FindByEmailAsync(AeternumWebApplicationFactory.CustomerEmail));
        var admin = Assert.IsType<ApplicationUser>(await userManager.FindByEmailAsync(AeternumWebApplicationFactory.AdminEmail));
        var db = services.GetRequiredService<AppDbContext>();
        var now = factory.Clock.GetUtcNow();
        var token = Guid.NewGuid().ToString("N");
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = $"SHIP-{token[..12].ToUpperInvariant()}",
            UserId = customer.Id,
            Status = OrderStatus.PaymentReceived,
            PaymentStatus = PaymentStatus.Succeeded,
            ShippingStatus = status,
            BillingAddressSnapshot = "{}",
            ShippingAddressSnapshot = "{}",
            Subtotal = 100,
            GrandTotal = 100,
            Currency = "TRY",
            IdempotencyKey = $"shipment-{token}",
            PaidAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        var shipment = new Shipment
        {
            Id = Guid.NewGuid(),
            Order = order,
            OrderId = order.Id,
            ShippingCompany = trackingNumber is null ? "Pending" : "AETERNUM Mock Shipping",
            TrackingNumber = trackingNumber,
            TrackingUrl = trackingNumber is null ? null : $"https://shipping.example.invalid/track/{trackingNumber}",
            Status = status,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        order.Shipment = shipment;
        db.Orders.Add(order);
        db.Shipments.Add(shipment);
        await db.SaveChangesAsync();
        return (order.Id, shipment.Id, admin.Id);
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = factory.CreateClientWithoutRedirects();
        using var login = await FormClient.LoginAsync(client, "/admin", AeternumWebApplicationFactory.AdminEmail);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        return client;
    }

    private async Task<int> CountAuditsAsync(Guid adminId, string action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs
            .CountAsync(x => x.AdminUserId == adminId && x.ActionType == action);
    }

    private static string ShipmentRow(string html, Guid orderId)
    {
        var marker = $"data-order-id=\"{orderId}\"";
        var markerIndex = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        Assert.True(markerIndex >= 0, $"Shipment row for order {orderId} was not found.");
        var start = html.LastIndexOf("<tr", markerIndex, StringComparison.OrdinalIgnoreCase);
        var end = html.IndexOf("</tr>", markerIndex, StringComparison.OrdinalIgnoreCase);
        Assert.True(start >= 0 && end > start, "Shipment row markup was incomplete.");
        return html[start..(end + 5)];
    }

    private static Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string path, string token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("RequestVerificationToken", token);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> PostWithoutBodyAsync(HttpClient client, string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("RequestVerificationToken", token);
        return client.SendAsync(request);
    }
}
