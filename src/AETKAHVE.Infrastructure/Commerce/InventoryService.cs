using AETKAHVE.Application.Commerce;
using AETKAHVE.Domain.Common;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AETKAHVE.Infrastructure.Commerce;

public sealed class InventoryService(AppDbContext dbContext, TimeProvider timeProvider) : IInventoryService
{
    public Task DeductForOrderAsync(Order order, Guid? actorUserId, CancellationToken cancellationToken) =>
        ApplyAsync(order, -1, StockMovementType.Sale, actorUserId, cancellationToken);

    public Task RestoreForOrderAsync(Order order, StockMovementType movementType, Guid? actorUserId, CancellationToken cancellationToken) =>
        ApplyAsync(order, 1, movementType, actorUserId, cancellationToken);

    private async Task ApplyAsync(Order order, int direction, StockMovementType movementType, Guid? actorUserId, CancellationToken cancellationToken)
    {
        foreach (var item in order.Items)
        {
            var exists = await dbContext.StockMovements.AnyAsync(x => x.ReferenceType == nameof(Order) && x.ReferenceId == order.Id &&
                x.ProductId == item.ProductId && x.ProductVariantId == item.ProductVariantId && x.MovementType == movementType, cancellationToken);
            if (exists) continue;

            var product = await dbContext.Products.IgnoreQueryFilters().SingleAsync(x => x.Id == item.ProductId, cancellationToken);
            int previous;
            int next;
            if (item.ProductVariantId is not null)
            {
                var variant = await dbContext.ProductVariants.IgnoreQueryFilters().SingleAsync(x => x.Id == item.ProductVariantId, cancellationToken);
                previous = variant.StockQuantity;
                variant.AdjustStock(direction * item.Quantity);
                next = variant.StockQuantity;
            }
            else
            {
                previous = product.StockQuantity;
                product.AdjustStock(direction * item.Quantity);
                next = product.StockQuantity;
            }

            var now = timeProvider.GetUtcNow();
            dbContext.StockMovements.Add(new StockMovement
            {
                ProductId = item.ProductId,
                ProductVariantId = item.ProductVariantId,
                MovementType = movementType,
                Quantity = direction * item.Quantity,
                PreviousStock = previous,
                NewStock = next,
                ReferenceType = nameof(Order),
                ReferenceId = order.Id,
                Description = $"Order {order.OrderNumber} inventory movement.",
                CreatedByUserId = actorUserId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
        }
    }
}
