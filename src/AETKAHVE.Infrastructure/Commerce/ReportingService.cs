using System.Globalization;
using System.Text;
using AETKAHVE.Application.Commerce;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AETKAHVE.Infrastructure.Commerce;

public sealed class ReportingService(AppDbContext dbContext) : IReportingService
{
    public async Task<SalesReport> GetSalesAsync(ReportFilter filter, CancellationToken cancellationToken)
    {
        if (filter.ToUtc <= filter.FromUtc) throw new ArgumentException("Report range is invalid.", nameof(filter));
        decimal gross;
        decimal discounts;
        decimal tax;
        decimal shipping;
        decimal collected;
        int count;
        decimal refunds;
        if (dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            var orders = (await dbContext.Orders.AsNoTracking().Where(x => x.PaidAtUtc != null).ToListAsync(cancellationToken))
                .Where(x => x.PaidAtUtc >= filter.FromUtc && x.PaidAtUtc < filter.ToUtc).ToList();
            gross = orders.Sum(x => x.Subtotal);
            discounts = orders.Sum(x => x.DiscountTotal);
            tax = orders.Sum(x => x.TaxTotal);
            shipping = orders.Sum(x => x.ShippingTotal);
            collected = orders.Sum(x => x.GrandTotal);
            count = orders.Count;
            refunds = (await dbContext.Refunds.AsNoTracking().Where(x => x.Status == RefundStatus.Succeeded && x.CompletedAtUtc != null).ToListAsync(cancellationToken))
                .Where(x => x.CompletedAtUtc >= filter.FromUtc && x.CompletedAtUtc < filter.ToUtc).Sum(x => x.Amount);
        }
        else
        {
            var orders = dbContext.Orders.AsNoTracking().Where(x => x.PaidAtUtc != null && x.PaidAtUtc >= filter.FromUtc && x.PaidAtUtc < filter.ToUtc);
            gross = await orders.SumAsync(x => (decimal?)x.Subtotal, cancellationToken) ?? 0;
            discounts = await orders.SumAsync(x => (decimal?)x.DiscountTotal, cancellationToken) ?? 0;
            tax = await orders.SumAsync(x => (decimal?)x.TaxTotal, cancellationToken) ?? 0;
            shipping = await orders.SumAsync(x => (decimal?)x.ShippingTotal, cancellationToken) ?? 0;
            collected = await orders.SumAsync(x => (decimal?)x.GrandTotal, cancellationToken) ?? 0;
            count = await orders.CountAsync(cancellationToken);
            refunds = await dbContext.Refunds.AsNoTracking().Where(x => x.Status == RefundStatus.Succeeded && x.CompletedAtUtc >= filter.FromUtc && x.CompletedAtUtc < filter.ToUtc)
                .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0;
        }

        return new SalesReport(gross, discounts, tax, shipping, refunds,
            Math.Max(0, gross - discounts + tax + shipping - refunds), count,
            count == 0 ? 0 : Math.Round(collected / count, 2));
    }

    public async Task<byte[]> ExportSalesCsvAsync(ReportFilter filter, CancellationToken cancellationToken)
    {
        var report = await GetSalesAsync(filter, cancellationToken);
        var builder = new StringBuilder("\uFEFFMetric,Value\r\n");
        Add(builder, "GrossRevenue", report.GrossRevenue); Add(builder, "DiscountTotal", report.DiscountTotal);
        Add(builder, "TaxTotal", report.TaxTotal); Add(builder, "ShippingRevenue", report.ShippingRevenue);
        Add(builder, "RefundTotal", report.RefundTotal); Add(builder, "NetRevenue", report.NetRevenue);
        Add(builder, "OrderCount", report.OrderCount); Add(builder, "AverageOrderValue", report.AverageOrderValue);
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static void Add(StringBuilder builder, string label, decimal value) => builder.Append(Escape(label)).Append(',').Append(value.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
    private static string Escape(string value)
    {
        var safe = value.Length > 0 && "=+-@\t\r".Contains(value[0]) ? "'" + value : value;
        return '"' + safe.Replace("\"", "\"\"") + '"';
    }
}
