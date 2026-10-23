using System.Globalization;
using System.Text.RegularExpressions;
using final.Data;
using final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace final.Tests;

public sealed class CatalogTests
{
    [Fact]
    public async Task HomePageDisplaysSeededProducts()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/");

        Assert.Contains("iPhone 16", html, StringComparison.Ordinal);
        Assert.Contains("MacBook Air", html, StringComparison.Ordinal);
        Assert.Contains("Galaxy S25", html, StringComparison.Ordinal);
        Assert.Contains("Sony WH-1000XM5", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CatalogRendersUploadedProductImage()
    {
        await using var factory = new TestWebApplicationFactory();
        const string imageUrl = "/images/products/catalog-test.jpg";
        const string productName = "Catalog image test product";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Products.Add(new Product
            {
                Name = productName,
                Description = "Image display test",
                Category = "Test",
                Price = 100_000m,
                ImageUrl = imageUrl
            });
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/");

        Assert.Contains($"src=\"{imageUrl}\"", html, StringComparison.Ordinal);
        Assert.Contains($"alt=\"{productName}\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StoreLayoutUsesN1736ShopBrand()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/");

        Assert.Contains("N1736 Shop", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Tech Haven", html, StringComparison.Ordinal);
        Assert.DoesNotContain("tech<span class=\"brand-accent\">haven</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CategoryFilterReturnsOnlyMatchingProducts()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        var category = Uri.EscapeDataString("Điện thoại");

        var html = await client.GetStringAsync($"/Home/Filter?category={category}");

        Assert.Contains("iPhone 16", html, StringComparison.Ordinal);
        Assert.Contains("Galaxy S25", html, StringComparison.Ordinal);
        Assert.DoesNotContain("MacBook Air", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownCategoryReturnsAnEmptyCatalog()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/Home/Filter?category=unknown-category");

        Assert.Contains("Chưa có sản phẩm", html, StringComparison.Ordinal);
        Assert.DoesNotContain("iPhone 16", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Orders/Checkout")]
    public async Task ProductPricesInDataAttributesUseInvariantNumberFormat(string path)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
        try
        {
            await using var factory = new TestWebApplicationFactory();
            using var client = factory.CreateClient();

            var html = await client.GetStringAsync(path);

            var prices = Regex.Matches(html, "data-product-price=\"([^\"]+)\"")
                .Select(match => match.Groups[1].Value)
                .ToArray();

            Assert.NotEmpty(prices);
            Assert.All(prices, price => Assert.True(
                decimal.TryParse(price, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _),
                $"'{price}' is not a JavaScript-compatible invariant number."));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
