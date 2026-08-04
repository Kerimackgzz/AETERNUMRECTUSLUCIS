using AETKAHVE.Application.Commerce;
using AETKAHVE.Domain.Common;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Infrastructure.Options;
using AETKAHVE.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.Commerce;

public sealed class ReturnService(
    AppDbContext dbContext,
    IEnumerable<IPaymentGateway> paymentGateways,
    INotificationQueue notificationQueue,
    IOptions<CommerceOptions> options,
    TimeProvider timeProvider) : IReturnService
{
    private readonly CommerceOptions _options = options.Value;

    public async Task<PagedResult<ReturnSummary>> GetForUserAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var source = dbContext.ReturnRequests.AsNoTracking().Where(x => x.UserId == userId);
        var query = dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true
            ? source.OrderByDescending(x => x.Id)
            : source.OrderByDescending(x => x.RequestedAtUtc);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ReturnSummary(x.Id, x.OrderId, x.Order.OrderNumber, x.Status, x.RefundAmount, x.RequestedAtUtc))
            .ToListAsync(cancellationToken);
        return new PagedResult<ReturnSummary>(items, page, pageSize, total);
    }

    public async Task<Guid> CreateAsync(ReturnCreateRequest request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0) throw new CommerceRuleException("At least one return item is required.");
        var order = await dbContext.Orders.Include(x => x.Items).Include(x => x.StatusHistory)
            .SingleOrDefaultAsync(x => x.Id == request.OrderId && x.UserId == request.UserId, cancellationToken)
            ?? throw new CommerceRuleException("Order was not found.");
        if (order.Status is not (OrderStatus.Delivered or OrderStatus.ReturnRequested) || order.DeliveredAtUtc is null)
            throw new CommerceRuleException("Only delivered orders can be returned.");
        if (order.DeliveredAtUtc.Value.AddDays(_options.ReturnWindowDays) < timeProvider.GetUtcNow())
            throw new CommerceRuleException("Return window has expired.");

        var existingQuantities = await dbContext.ReturnItems.AsNoTracking()
            .Where(x => x.ReturnRequest.OrderId == order.Id && x.ReturnRequest.Status != ReturnStatus.Rejected && x.ReturnRequest.Status != ReturnStatus.Cancelled)
            .GroupBy(x => x.OrderItemId).Select(x => new { OrderItemId = x.Key, Quantity = x.Sum(i => i.Quantity) })
            .ToDictionaryAsync(x => x.OrderItemId, x => x.Quantity, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var entity = new ReturnRequest
        {
            Order = order, UserId = request.UserId, Reason = Required(request.Reason, 250), Description = Truncate(request.Description, 2000),
            RequestedAtUtc = now, CreatedAtUtc = now, UpdatedAtUtc = now,
        };
        foreach (var input in request.Items)
        {
            var item = order.Items.SingleOrDefault(x => x.Id == input.OrderItemId) ?? throw new CommerceRuleException("Return item is not part of the order.");
            var already = existingQuantities.GetValueOrDefault(item.Id);
            if (input.Quantity < 1 || input.Quantity + already > item.Quantity) throw new CommerceRuleException("Return quantity exceeds purchased quantity.");
            entity.Items.Add(new ReturnItem
            {
                OrderItem = item, Quantity = input.Quantity, Reason = Required(input.Reason, 500), Condition = input.Condition,
                ImageStorageKey = Truncate(input.ImageStorageKey, 512), CreatedAtUtc = now, UpdatedAtUtc = now,
            });
            entity.RefundAmount += Math.Round(item.LineTotal / item.Quantity * input.Quantity, 2, MidpointRounding.AwayFromZero);
        }
        dbContext.ReturnRequests.Add(entity);
        if (order.Status == OrderStatus.Delivered)
            dbContext.OrderStatusHistory.Add(order.TransitionTo(OrderStatus.ReturnRequested, request.UserId, now, "Return requested."));
        await notificationQueue.EnqueueOrderAsync(order, "ReturnRequested", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<ServiceResult> DecideAsync(ReturnDecision decision, CancellationToken cancellationToken)
    {
        var request = await dbContext.ReturnRequests.Include(x => x.Items).ThenInclude(x => x.OrderItem)
            .Include(x => x.Order).ThenInclude(x => x.Items).Include(x => x.Order).ThenInclude(x => x.Payments)
            .SingleOrDefaultAsync(x => x.Id == decision.ReturnRequestId, cancellationToken);
        if (request is null) return ServiceResult.Failure("Return request was not found.");
        if (!CanTransition(request.Status, decision.NewStatus)) return ServiceResult.Failure("Return status transition is invalid.");

        RefundResult? refundResult = null;
        Payment? payment = null;
        if (decision.NewStatus == ReturnStatus.Completed)
        {
            payment = request.Order.Payments.OrderByDescending(x => x.CreatedAtUtc).FirstOrDefault(x => x.Status is PaymentStatus.Succeeded or PaymentStatus.Refunded);
            if (payment is null) return ServiceResult.Failure("Successful payment was not found.");
            var gateway = paymentGateways.Single(x => x.ProviderName.Equals(payment.Provider, StringComparison.OrdinalIgnoreCase));
            refundResult = await gateway.RefundAsync(new RefundRequest(payment.Id, payment.TransactionId ?? string.Empty, request.RefundAmount, payment.Currency, $"return-{request.Id:N}"), cancellationToken);
            if (!refundResult.Succeeded)
            {
                request.Status = ReturnStatus.RefundPending;
                request.AdminResponse = Truncate(decision.Response, 2000);
                request.UpdatedAtUtc = timeProvider.GetUtcNow();
                await dbContext.SaveChangesAsync(cancellationToken);
                return ServiceResult.Failure("Refund failed and remains pending for retry.");
            }
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        request.Status = decision.NewStatus;
        request.AdminResponse = Truncate(decision.Response, 2000);
        request.ReviewedByUserId = decision.AdminUserId;
        request.ReviewedAtUtc = now;
        request.RestockApproved = decision.Restock;
        request.UpdatedAtUtc = now;

        if (decision.NewStatus == ReturnStatus.ProductReceived && decision.Restock)
            await RestockAsync(request, decision.AdminUserId, cancellationToken);

        if (decision.NewStatus == ReturnStatus.Completed && payment is not null)
        {
            dbContext.Refunds.Add(new Refund
            {
                PaymentId = payment.Id, ReturnRequestId = request.Id, Amount = request.RefundAmount, Status = RefundStatus.Succeeded,
                ProviderReference = refundResult!.ProviderReference, CompletedAtUtc = now, CreatedAtUtc = now, UpdatedAtUtc = now,
            });
            var refundedTotal = await dbContext.Refunds.Where(x => x.PaymentId == payment.Id && x.Status == RefundStatus.Succeeded).SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
            if (refundedTotal + request.RefundAmount >= payment.Amount)
            {
                payment.Status = PaymentStatus.Refunded;
                request.Order.PaymentStatus = PaymentStatus.Refunded;
                if (request.Order.Status == OrderStatus.ReturnRequested)
                    dbContext.OrderStatusHistory.Add(request.Order.TransitionTo(OrderStatus.Returned, decision.AdminUserId, now, "Returned products received."));
                if (request.Order.Status == OrderStatus.Returned)
                    dbContext.OrderStatusHistory.Add(request.Order.TransitionTo(OrderStatus.Refunded, decision.AdminUserId, now, "Return refunded."));
            }
        }

        await notificationQueue.EnqueueOrderAsync(request.Order, $"Return{decision.NewStatus}", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult.Success("Return request was updated.");
    }

    private async Task RestockAsync(ReturnRequest request, Guid actorUserId, CancellationToken cancellationToken)
    {
        foreach (var item in request.Items)
        {
            var exists = await dbContext.StockMovements.AnyAsync(x => x.ReferenceType == nameof(ReturnRequest) && x.ReferenceId == request.Id &&
                x.ProductId == item.OrderItem.ProductId && x.ProductVariantId == item.OrderItem.ProductVariantId && x.MovementType == StockMovementType.Return, cancellationToken);
            if (exists) continue;
            var product = await dbContext.Products.IgnoreQueryFilters().SingleAsync(x => x.Id == item.OrderItem.ProductId, cancellationToken);
            int previous;
            int next;
            if (item.OrderItem.ProductVariantId is not null)
            {
                var variant = await dbContext.ProductVariants.IgnoreQueryFilters().SingleAsync(x => x.Id == item.OrderItem.ProductVariantId, cancellationToken);
                previous = variant.StockQuantity; variant.AdjustStock(item.Quantity); next = variant.StockQuantity;
            }
            else { previous = product.StockQuantity; product.AdjustStock(item.Quantity); next = product.StockQuantity; }
            var now = timeProvider.GetUtcNow();
            dbContext.StockMovements.Add(new StockMovement
            {
                ProductId = item.OrderItem.ProductId, ProductVariantId = item.OrderItem.ProductVariantId, MovementType = StockMovementType.Return,
                Quantity = item.Quantity, PreviousStock = previous, NewStock = next, ReferenceType = nameof(ReturnRequest), ReferenceId = request.Id,
                Description = "Approved returned inventory.", CreatedByUserId = actorUserId, CreatedAtUtc = now, UpdatedAtUtc = now,
            });
        }
    }

    private static bool CanTransition(ReturnStatus current, ReturnStatus next) => (current, next) switch
    {
        (ReturnStatus.Pending, ReturnStatus.UnderReview or ReturnStatus.Approved or ReturnStatus.Rejected or ReturnStatus.Cancelled) => true,
        (ReturnStatus.UnderReview, ReturnStatus.Approved or ReturnStatus.Rejected) => true,
        (ReturnStatus.Approved, ReturnStatus.AwaitingProduct or ReturnStatus.ProductReceived) => true,
        (ReturnStatus.AwaitingProduct, ReturnStatus.ProductReceived) => true,
        (ReturnStatus.ProductReceived, ReturnStatus.RefundPending or ReturnStatus.Completed) => true,
        (ReturnStatus.RefundPending, ReturnStatus.Completed) => true,
        _ => false,
    };

    private static string Required(string value, int max) => string.IsNullOrWhiteSpace(value) ? throw new CommerceRuleException("Required value is missing.") : value.Trim()[..Math.Min(max, value.Trim().Length)];
    private static string? Truncate(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(max, value.Trim().Length)];
}

public sealed class ReviewService(AppDbContext dbContext, TimeProvider timeProvider) : IReviewService
{
    public async Task<PagedResult<ReviewSummary>> GetForUserAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var source = dbContext.Reviews.AsNoTracking().Where(x => x.UserId == userId);
        var query = dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true
            ? source.OrderByDescending(x => x.Id)
            : source.OrderByDescending(x => x.CreatedAtUtc);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ReviewSummary(x.Id, x.Product.Name, x.Rating, x.Comment, x.Status, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return new PagedResult<ReviewSummary>(items, page, pageSize, total);
    }

    public async Task<Guid> CreateOrUpdateAsync(ReviewInput input, CancellationToken cancellationToken)
    {
        if (input.Rating is < 1 or > 5 || string.IsNullOrWhiteSpace(input.Comment)) throw new CommerceRuleException("Review rating or comment is invalid.");
        var item = await dbContext.OrderItems.Include(x => x.Order)
            .SingleOrDefaultAsync(x => x.Id == input.OrderItemId && x.Order.UserId == input.UserId && x.Order.Status == OrderStatus.Delivered, cancellationToken)
            ?? throw new CommerceRuleException("Only delivered purchases can be reviewed.");
        var review = await dbContext.Reviews.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.UserId == input.UserId && x.OrderItemId == input.OrderItemId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (review is not null) throw new CommerceRuleException("This purchase item has already been reviewed.");
        review = new Review { UserId = input.UserId, ProductId = item.ProductId, OrderItemId = item.Id, CreatedAtUtc = now };
        dbContext.Reviews.Add(review);
        review.Rating = input.Rating;
        review.Comment = input.Comment.Trim()[..Math.Min(3000, input.Comment.Trim().Length)];
        review.Status = ReviewStatus.Pending;
        review.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return review.Id;
    }

    public async Task<ServiceResult> DeleteAsync(Guid userId, Guid reviewId, CancellationToken cancellationToken)
    {
        var review = await dbContext.Reviews.SingleOrDefaultAsync(x => x.Id == reviewId && x.UserId == userId, cancellationToken);
        if (review is null) return ServiceResult.Failure("Review was not found.");
        review.DeletedAtUtc = timeProvider.GetUtcNow(); review.UpdatedAtUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success("Review was deleted.");
    }
}

public sealed class ContactService(AppDbContext dbContext, TimeProvider timeProvider) : IContactService
{
    public async Task<Guid> SubmitAsync(string fullName, string email, string? phone, string subject, string message, bool privacyAccepted, CancellationToken cancellationToken)
    {
        if (!privacyAccepted || string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || !email.Contains('@') || string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(message))
            throw new CommerceRuleException("Contact form is invalid.");
        var now = timeProvider.GetUtcNow();
        var entity = new ContactMessage
        {
            FullName = fullName.Trim()[..Math.Min(200, fullName.Trim().Length)], Email = email.Trim()[..Math.Min(320, email.Trim().Length)],
            PhoneNumber = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim()[..Math.Min(30, phone.Trim().Length)],
            Subject = subject.Trim()[..Math.Min(200, subject.Trim().Length)], Message = message.Trim()[..Math.Min(5000, message.Trim().Length)],
            PrivacyAccepted = true, CreatedAtUtc = now, UpdatedAtUtc = now,
        };
        dbContext.ContactMessages.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}
