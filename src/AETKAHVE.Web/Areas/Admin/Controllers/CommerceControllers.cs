using System.Security.Claims;
using AETKAHVE.Application.Commerce;
using AETKAHVE.Application.Security;
using AETKAHVE.Domain.Common;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AETKAHVE.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminArea)]
public abstract class AdminCommerceControllerBase : Controller
{
    protected Guid AdminUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new InvalidOperationException("Authenticated admin user id is unavailable.");
}

[Route("admin/catalog")]
public sealed class CatalogController(ICatalogQueryService catalogQueryService) : AdminCommerceControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(new AdminCatalogViewModel(await catalogQueryService.GetLookupSetAsync(cancellationToken)));

    [HttpPost]
    public async Task<IActionResult> Save([FromServices] IAdminCommerceService adminService, [FromBody] AdminCatalogLookupInput input, CancellationToken cancellationToken)
    {
        try { return Ok(new CommerceMutationResponse(true, "Katalog kaydı kaydedildi.", Data: new { id = await adminService.SaveCatalogLookupAsync(AdminUserId, input, cancellationToken) })); }
        catch (CommerceRuleException exception) { return Conflict(new CommerceMutationResponse(false, exception.Message)); }
    }
}

[Route("admin/products")]
public sealed class ProductsController(IAdminCommerceService adminService) : AdminCommerceControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int page, CancellationToken cancellationToken) =>
        View(await adminService.GetProductsAsync(Math.Max(1, page), 50, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] AdminProductInput input, CancellationToken cancellationToken)
    {
        try
        {
            var id = await adminService.SaveProductAsync(AdminUserId, input, cancellationToken);
            return Ok(new CommerceMutationResponse(true, "Ürün kaydedildi.", Data: new { id }));
        }
        catch (CommerceRuleException exception) { return Conflict(new CommerceMutationResponse(false, exception.Message)); }
    }

    [HttpPost("{productId:guid}/stock")]
    public async Task<IActionResult> Stock(Guid productId, [FromQuery] Guid? variantId, [FromQuery] int delta, CancellationToken cancellationToken)
    {
        var result = await adminService.AdjustStockAsync(AdminUserId, productId, variantId, delta, cancellationToken);
        return result.Succeeded ? Ok(new CommerceMutationResponse(true, result.Message)) : Conflict(new CommerceMutationResponse(false, result.Message));
    }

    [HttpPost("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, [FromQuery] bool isActive, CancellationToken cancellationToken)
    {
        var result = await adminService.SetProductActiveAsync(AdminUserId, id, isActive, cancellationToken);
        return result.Succeeded ? Ok(new CommerceMutationResponse(true, result.Message)) : Conflict(new CommerceMutationResponse(false, result.Message));
    }
}

[Route("admin/orders")]
public sealed class OrdersController(IAdminCommerceService adminService) : AdminCommerceControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int page, CancellationToken cancellationToken) => View(await adminService.GetOrdersAsync(Math.Max(1, page), 50, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken)
    {
        var detail = await adminService.GetOrderDetailAsync(id, cancellationToken);
        return detail is null ? NotFound() : View(detail);
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, [FromQuery] OrderStatus status, [FromForm] string description, CancellationToken cancellationToken)
    {
        var result = await adminService.ChangeOrderStatusAsync(AdminUserId, id, status, description, cancellationToken);
        return result.Succeeded ? Ok(new CommerceMutationResponse(true, result.Message)) : Conflict(new CommerceMutationResponse(false, result.Message));
    }

    [HttpPost("{id:guid}/force-status")]
    public async Task<IActionResult> ForceStatus(Guid id, [FromQuery] OrderStatus status, [FromForm] string reason, CancellationToken cancellationToken)
    {
        var result = await adminService.ForceSetOrderStatusAsync(AdminUserId, id, status, reason, cancellationToken);
        return result.Succeeded ? Ok(new CommerceMutationResponse(true, result.Message)) : Conflict(new CommerceMutationResponse(false, result.Message));
    }

    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await adminService.DeleteOrderAsync(AdminUserId, id, cancellationToken);
        return result.Succeeded ? Ok(new CommerceMutationResponse(true, result.Message)) : Conflict(new CommerceMutationResponse(false, result.Message));
    }
}

[Route("admin/invoices")]
public sealed class InvoicesController(IAdminCommerceService adminService) : AdminCommerceControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int page, CancellationToken cancellationToken) =>
        View(new AdminInvoiceListViewModel(await adminService.GetInvoicesAsync(Math.Max(1, page), 50, cancellationToken)));

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await adminService.OpenInvoiceAsync(id, cancellationToken);
        return invoice is null ? NotFound() : File(invoice.Content, "application/pdf", invoice.FileName);
    }
}

[Route("admin/shipments")]
public sealed class ShipmentsController(IAdminCommerceService adminService) : AdminCommerceControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int page, CancellationToken cancellationToken) =>
        View(new AdminShipmentListViewModel(await adminService.GetShipmentsAsync(Math.Max(1, page), 50, cancellationToken)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AdminShipmentInput input, CancellationToken cancellationToken)
    {
        var result = await adminService.CreateShipmentAsync(AdminUserId, input, cancellationToken);
        return result.Succeeded ? Ok(new CommerceMutationResponse(true, result.Message)) : Conflict(new CommerceMutationResponse(false, result.Message));
    }

    [HttpPost("{orderId:guid}/track")]
    public async Task<IActionResult> Track(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await adminService.TrackShipmentAsync(AdminUserId, orderId, cancellationToken);
        return result.Succeeded ? Ok(new CommerceMutationResponse(true, result.Message)) : Conflict(new CommerceMutationResponse(false, result.Message));
    }

    [HttpPost("{orderId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid orderId, CancellationToken cancellationToken)
    {
        var result = await adminService.CancelShipmentAsync(AdminUserId, orderId, cancellationToken);
        return result.Succeeded ? Ok(new CommerceMutationResponse(true, result.Message)) : Conflict(new CommerceMutationResponse(false, result.Message));
    }
}

[Route("admin/campaigns")]
public sealed class CampaignsController(IAdminCommerceService adminService) : AdminCommerceControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int page, CancellationToken cancellationToken) =>
        View(new AdminCampaignListViewModel(await adminService.GetCampaignsAsync(Math.Max(1, page), 50, cancellationToken)));

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] AdminCampaignInput input, CancellationToken cancellationToken)
    {
        try { return Ok(new CommerceMutationResponse(true, "Kampanya kaydedildi.", Data: new { id = await adminService.SaveCampaignAsync(AdminUserId, input, cancellationToken) })); }
        catch (CommerceRuleException exception) { return Conflict(new CommerceMutationResponse(false, exception.Message)); }
    }

    [HttpPost("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, [FromQuery] bool isActive, CancellationToken cancellationToken)
    {
        var result = await adminService.SetCampaignActiveAsync(AdminUserId, id, isActive, cancellationToken);
        return result.Succeeded ? Ok(new CommerceMutationResponse(true, result.Message)) : Conflict(new CommerceMutationResponse(false, result.Message));
    }
}

[Route("admin/coupons")]
public sealed class CouponsController(IAdminCommerceService adminService) : AdminCommerceControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int page, CancellationToken cancellationToken) =>
        View(new AdminCouponListViewModel(await adminService.GetCouponsAsync(Math.Max(1, page), 50, cancellationToken)));

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] AdminCouponInput input, CancellationToken cancellationToken)
    {
        try { return Ok(new CommerceMutationResponse(true, "Kupon kaydedildi.", Data: new { id = await adminService.SaveCouponAsync(AdminUserId, input, cancellationToken) })); }
        catch (CommerceRuleException exception) { return Conflict(new CommerceMutationResponse(false, exception.Message)); }
    }

    [HttpPost("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, [FromQuery] bool isActive, CancellationToken cancellationToken)
    {
        var result = await adminService.SetCouponActiveAsync(AdminUserId, id, isActive, cancellationToken);
        return result.Succeeded ? Ok(new CommerceMutationResponse(true, result.Message)) : Conflict(new CommerceMutationResponse(false, result.Message));
    }
}

[Route("admin/returns")]
public sealed class ReturnsController(IReturnService returnService) : AdminCommerceControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromServices] IAdminCommerceService adminService, [FromQuery] int page, CancellationToken cancellationToken) =>
        View(new AdminReturnListViewModel(await adminService.GetReturnsAsync(Math.Max(1, page), 50, cancellationToken)));

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, [FromQuery] ReturnStatus status, [FromQuery] bool restock, [FromForm] string response, CancellationToken cancellationToken)
    {
        var result = await returnService.DecideAsync(new ReturnDecision(id, AdminUserId, status, response, restock), cancellationToken);
        return result.Succeeded ? Ok(new CommerceMutationResponse(true, result.Message)) : Conflict(new CommerceMutationResponse(false, result.Message));
    }
}

[Route("admin/reviews")]
public sealed class ReviewsController(IAdminCommerceService adminService) : AdminCommerceControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int page, CancellationToken cancellationToken) =>
        View(new AdminReviewListViewModel(await adminService.GetReviewsAsync(Math.Max(1, page), 50, cancellationToken)));

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, [FromQuery] ReviewStatus status, [FromForm] string? response, CancellationToken cancellationToken)
    {
        var result = await adminService.ModerateReviewAsync(AdminUserId, id, status, response, cancellationToken);
        return result.Succeeded ? Ok(new CommerceMutationResponse(true, result.Message)) : Conflict(new CommerceMutationResponse(false, result.Message));
    }
}

[Route("admin/messages")]
public sealed class MessagesController(IAdminCommerceService adminService) : AdminCommerceControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int page, CancellationToken cancellationToken) =>
        View(new AdminMessageListViewModel(await adminService.GetContactMessagesAsync(Math.Max(1, page), 50, cancellationToken)));

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> Status(Guid id, [FromQuery] ContactMessageStatus status, CancellationToken cancellationToken)
    {
        var result = await adminService.UpdateContactStatusAsync(AdminUserId, id, status, cancellationToken);
        return result.Succeeded ? Ok(new CommerceMutationResponse(true, result.Message)) : NotFound();
    }
}

[Route("admin/reports")]
public sealed class ReportsController(IReportingService reportingService) : AdminCommerceControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken cancellationToken)
    {
        var filter = Range(from, to);
        return View(await reportingService.GetSalesAsync(filter, cancellationToken));
    }

    [HttpGet("sales.csv")]
    public async Task<IActionResult> Csv([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken cancellationToken) =>
        File(await reportingService.ExportSalesCsvAsync(Range(from, to), cancellationToken), "text/csv; charset=utf-8", "sales-report.csv");

    private static ReportFilter Range(DateTimeOffset? from, DateTimeOffset? to)
    {
        var end = to ?? DateTimeOffset.UtcNow;
        return new ReportFilter(from ?? end.AddDays(-30), end);
    }
}
