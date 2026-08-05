using AETKAHVE.Application.Commerce;
using AETKAHVE.Application.Security;
using AETKAHVE.Domain.Common;
using AETKAHVE.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AETKAHVE.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
[Route("account/addresses")]
public sealed class AddressesController(IAddressService addressService) : CommerceControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(new AddressListViewModel(await addressService.GetAsync(RequiredUserId, cancellationToken)));

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] AddressInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(new CommerceMutationResponse(false, "Adres doğrulanamadı."));
        try
        {
            var result = await addressService.SaveAsync(RequiredUserId, input.Id, new AddressInput(input.Title, input.FirstName, input.LastName,
                input.PhoneNumber, input.Country, input.City, input.District, input.Neighborhood, input.PostalCode, input.AddressLine,
                input.IsDefaultShipping, input.IsDefaultBilling), cancellationToken);
            return Ok(new CommerceMutationResponse(true, "Adres kaydedildi.", Data: result));
        }
        catch (CommerceRuleException exception)
        {
            return Conflict(new CommerceMutationResponse(false, exception.Message));
        }
    }

    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await addressService.DeleteAsync(RequiredUserId, id, cancellationToken)
            ? Ok(new CommerceMutationResponse(true, "Adres silindi.")) : NotFound();
}

[Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
[Route("account/orders")]
public sealed class OrdersController(IOrderService orderService) : CommerceControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int page, CancellationToken cancellationToken) =>
        View(new OrderListViewModel(await orderService.GetForUserAsync(RequiredUserId, Math.Max(1, page), 20, cancellationToken)));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken)
    {
        var order = await orderService.GetForUserAsync(RequiredUserId, id, cancellationToken);
        return order is null ? NotFound() : View(new OrderDetailViewModel(order));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await orderService.CancelAsync(RequiredUserId, id, cancellationToken);
        return result.Succeeded ? Ok(new CommerceMutationResponse(true, result.Message)) : Conflict(new CommerceMutationResponse(false, result.Message));
    }
}

[Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
[Route("account/invoices")]
public sealed class InvoicesController(IOrderService orderService) : CommerceControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int page, CancellationToken cancellationToken) =>
        View(new InvoiceListViewModel(await orderService.GetInvoicesForUserAsync(RequiredUserId, Math.Max(1, page), 20, cancellationToken)));

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await orderService.OpenInvoiceAsync(RequiredUserId, id, cancellationToken);
        return invoice is null ? NotFound() : File(invoice.Content, "application/pdf", invoice.FileName);
    }
}

[Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
[Route("account/returns")]
public sealed class ReturnsController(IReturnService returnService) : CommerceControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int page, CancellationToken cancellationToken) =>
        View(new ReturnListViewModel(await returnService.GetForUserAsync(RequiredUserId, Math.Max(1, page), 20, cancellationToken)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ReturnInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(new CommerceMutationResponse(false, "İade talebi doğrulanamadı."));
        try
        {
            var id = await returnService.CreateAsync(new ReturnCreateRequest(RequiredUserId, input.OrderId, input.Reason, input.Description,
                input.Items.Select(x => new ReturnItemInput(x.OrderItemId, x.Quantity, x.Reason, x.Condition, x.ImageStorageKey)).ToList()), cancellationToken);
            return Ok(new CommerceMutationResponse(true, "İade talebi oluşturuldu.", Data: new { id }));
        }
        catch (CommerceRuleException exception) { return Conflict(new CommerceMutationResponse(false, exception.Message)); }
    }
}

[Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
[Route("account/reviews")]
public sealed class ReviewsController(IReviewService reviewService) : CommerceControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int page, CancellationToken cancellationToken) =>
        View(new ReviewListViewModel(await reviewService.GetForUserAsync(RequiredUserId, Math.Max(1, page), 20, cancellationToken)));

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] ReviewInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(new CommerceMutationResponse(false, "Yorum doğrulanamadı."));
        try
        {
            var id = await reviewService.CreateOrUpdateAsync(new ReviewInput(RequiredUserId, input.OrderItemId, input.Rating, input.Comment), cancellationToken);
            return Ok(new CommerceMutationResponse(true, "Yorum incelemeye gönderildi.", Data: new { id }));
        }
        catch (CommerceRuleException exception) { return Conflict(new CommerceMutationResponse(false, exception.Message)); }
    }

    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await reviewService.DeleteAsync(RequiredUserId, id, cancellationToken);
        return result.Succeeded ? Ok(new CommerceMutationResponse(true, result.Message)) : NotFound();
    }
}

[Route("contact")]
public sealed class ContactController(IContactService contactService) : Controller
{
    [HttpGet]
    public IActionResult Index() => View(new ContactInputModel());

    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] ContactInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(new CommerceMutationResponse(false, "İletişim formu doğrulanamadı."));
        try
        {
            var id = await contactService.SubmitAsync(input.FullName, input.Email, input.PhoneNumber, input.Subject, input.Message, input.PrivacyAccepted, cancellationToken);
            return Ok(new CommerceMutationResponse(true, "Mesajınız alındı.", Data: new { id }));
        }
        catch (CommerceRuleException exception) { return BadRequest(new CommerceMutationResponse(false, exception.Message)); }
    }
}
