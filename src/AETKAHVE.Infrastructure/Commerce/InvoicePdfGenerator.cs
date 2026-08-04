using System.Globalization;
using AETKAHVE.Application.Commerce;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace AETKAHVE.Infrastructure.Commerce;

public sealed class InvoicePdfGenerator : IInvoicePdfGenerator
{
    static InvoicePdfGenerator()
    {
        if (OperatingSystem.IsWindows()) GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }

    public Task<byte[]> GenerateAsync(InvoiceDocument document, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var pdf = new PdfDocument();
        pdf.Info.Title = $"{document.BrandName} - {document.InvoiceNumber}";
        var page = pdf.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        var graphics = XGraphics.FromPdfPage(page);
        var titleFont = new XFont("Arial", 18, XFontStyleEx.Bold);
        var headingFont = new XFont("Arial", 10, XFontStyleEx.Bold);
        var textFont = new XFont("Arial", 9, XFontStyleEx.Regular);
        var formatter = new XTextFormatter(graphics);
        var culture = CultureInfo.GetCultureInfo("tr-TR");
        try
        {
            graphics.DrawString(document.BrandName, titleFont, XBrushes.Black, new XRect(40, 36, page.Width.Point - 80, 30), XStringFormats.TopLeft);
            graphics.DrawString($"Fatura: {document.InvoiceNumber}", headingFont, XBrushes.Black, 40, 82);
            graphics.DrawString($"Sipariş: {document.OrderNumber}", textFont, XBrushes.Black, 40, 100);
            graphics.DrawString($"Tarih: {document.InvoiceDateUtc:yyyy-MM-dd HH:mm} UTC", textFont, XBrushes.Black, 40, 116);
            graphics.DrawString($"Müşteri: {document.CustomerName}", textFont, XBrushes.Black, 40, 138);
            formatter.DrawString(document.BillingAddress, textFont, XBrushes.Black, new XRect(40, 152, page.Width.Point - 80, 50));

            var y = 216d;
            DrawColumns(graphics, headingFont, y);
            y += 18;
            foreach (var line in document.Lines)
            {
                if (y > page.Height.Point - 160)
                {
                    graphics.Dispose();
                    page = pdf.AddPage();
                    page.Size = PdfSharp.PageSize.A4;
                    graphics = XGraphics.FromPdfPage(page);
                    formatter = new XTextFormatter(graphics);
                    y = 50;
                    DrawColumns(graphics, headingFont, y);
                    y += 18;
                }
                graphics.DrawString($"{line.Name} ({line.Sku})", textFont, XBrushes.Black, 40, y);
                graphics.DrawString(line.Quantity.ToString(culture), textFont, XBrushes.Black, 300, y);
                graphics.DrawString(line.UnitPrice.ToString("N2", culture), textFont, XBrushes.Black, 350, y);
                graphics.DrawString(line.Total.ToString("N2", culture), textFont, XBrushes.Black, 470, y);
                y += 17;
            }

            if (y > page.Height.Point - 125)
            {
                graphics.Dispose();
                page = pdf.AddPage();
                page.Size = PdfSharp.PageSize.A4;
                graphics = XGraphics.FromPdfPage(page);
                formatter = new XTextFormatter(graphics);
                y = 50;
            }

            y += 16;
            DrawTotal(graphics, textFont, "Ara toplam", document.Subtotal, document.Currency, culture, y); y += 16;
            DrawTotal(graphics, textFont, "İndirim", -document.DiscountTotal, document.Currency, culture, y); y += 16;
            DrawTotal(graphics, textFont, "Vergi", document.TaxTotal, document.Currency, culture, y); y += 16;
            DrawTotal(graphics, textFont, "Kargo", document.ShippingTotal, document.Currency, culture, y); y += 20;
            DrawTotal(graphics, headingFont, "Genel toplam", document.GrandTotal, document.Currency, culture, y);
        }
        finally
        {
            graphics.Dispose();
        }

        using var stream = new MemoryStream();
        pdf.Save(stream, false);
        return Task.FromResult(stream.ToArray());
    }

    private static void DrawTotal(XGraphics graphics, XFont font, string label, decimal value, string currency, CultureInfo culture, double y)
    {
        graphics.DrawString(label, font, XBrushes.Black, 350, y);
        graphics.DrawString($"{value.ToString("N2", culture)} {currency}", font, XBrushes.Black, 470, y);
    }

    private static void DrawColumns(XGraphics graphics, XFont headingFont, double y)
    {
        graphics.DrawString("Ürün", headingFont, XBrushes.Black, 40, y);
        graphics.DrawString("Adet", headingFont, XBrushes.Black, 300, y);
        graphics.DrawString("Birim", headingFont, XBrushes.Black, 350, y);
        graphics.DrawString("Toplam", headingFont, XBrushes.Black, 470, y);
    }
}
