using AETKAHVE.Application.Commerce;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Domain.Common;
using AETKAHVE.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AETKAHVE.Infrastructure.Commerce;

public sealed class FavoriteService(AppDbContext dbContext, TimeProvider timeProvider) : IFavoriteService
{
    public async Task<bool> ToggleAsync(Guid userId, Guid productId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Products.AnyAsync(x => x.Id == productId && x.IsActive, cancellationToken)) throw new CommerceRuleException("Product is unavailable.");
        var favorite = await dbContext.Favorites.SingleOrDefaultAsync(x => x.UserId == userId && x.ProductId == productId, cancellationToken);
        if (favorite is null)
        {
            var now = timeProvider.GetUtcNow();
            dbContext.Favorites.Add(new Favorite { UserId = userId, ProductId = productId, CreatedAtUtc = now, UpdatedAtUtc = now });
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        dbContext.Favorites.Remove(favorite);
        await dbContext.SaveChangesAsync(cancellationToken);
        return false;
    }

    public async Task<PagedResult<ProductSummary>> GetAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var products = dbContext.Products.AsNoTracking()
            .Where(x => x.IsActive && dbContext.Favorites.Any(f => f.UserId == userId && f.ProductId == x.Id))
            .OrderBy(x => x.Name);
        var total = await products.CountAsync(cancellationToken);
        var items = await CatalogQueryService.ProjectSummaries(products)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<ProductSummary>(items.Select(x => x with { IsFavorite = true }).ToList(), page, pageSize, total);
    }
}

public sealed class AddressService(AppDbContext dbContext, TimeProvider timeProvider) : IAddressService
{
    public async Task<IReadOnlyList<AddressDetails>> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Addresses.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.IsDefaultShipping)
            .ThenBy(x => x.Title).Select(x => Map(x)).ToListAsync(cancellationToken);

    public async Task<AddressDetails> SaveAsync(Guid userId, Guid? addressId, AddressInput input, CancellationToken cancellationToken)
    {
        Validate(input);
        var address = addressId is null
            ? new Address { UserId = userId, CreatedAtUtc = timeProvider.GetUtcNow() }
            : await dbContext.Addresses.SingleOrDefaultAsync(x => x.Id == addressId && x.UserId == userId, cancellationToken)
                ?? throw new CommerceRuleException("Address was not found.");
        if (addressId is null) dbContext.Addresses.Add(address);

        if (input.IsDefaultShipping)
        {
            await dbContext.Addresses.Where(x => x.UserId == userId && x.Id != address.Id && x.IsDefaultShipping)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsDefaultShipping, false), cancellationToken);
        }
        if (input.IsDefaultBilling)
        {
            await dbContext.Addresses.Where(x => x.UserId == userId && x.Id != address.Id && x.IsDefaultBilling)
                .ExecuteUpdateAsync(x => x.SetProperty(y => y.IsDefaultBilling, false), cancellationToken);
        }

        address.Title = input.Title.Trim();
        address.FirstName = input.FirstName.Trim();
        address.LastName = input.LastName.Trim();
        address.PhoneNumber = input.PhoneNumber.Trim();
        address.Country = input.Country.Trim();
        address.City = input.City.Trim();
        address.District = input.District.Trim();
        address.Neighborhood = input.Neighborhood?.Trim();
        address.PostalCode = input.PostalCode?.Trim();
        address.AddressLine = input.AddressLine.Trim();
        address.IsDefaultShipping = input.IsDefaultShipping;
        address.IsDefaultBilling = input.IsDefaultBilling;
        address.UpdatedAtUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(address);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid addressId, CancellationToken cancellationToken)
    {
        var address = await dbContext.Addresses.SingleOrDefaultAsync(x => x.Id == addressId && x.UserId == userId, cancellationToken);
        if (address is null) return false;
        address.DeletedAtUtc = timeProvider.GetUtcNow();
        address.UpdatedAtUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static AddressDetails Map(Address x) => new(x.Id, x.Title, x.FirstName, x.LastName, x.PhoneNumber, x.Country, x.City, x.District, x.Neighborhood, x.PostalCode, x.AddressLine, x.IsDefaultShipping, x.IsDefaultBilling);

    private static void Validate(AddressInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || string.IsNullOrWhiteSpace(input.FirstName) || string.IsNullOrWhiteSpace(input.LastName) ||
            string.IsNullOrWhiteSpace(input.PhoneNumber) || string.IsNullOrWhiteSpace(input.Country) || string.IsNullOrWhiteSpace(input.City) ||
            string.IsNullOrWhiteSpace(input.District) || string.IsNullOrWhiteSpace(input.AddressLine)) throw new CommerceRuleException("Address fields are incomplete.");
    }
}
