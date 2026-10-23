using final.Data;
using final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace final.Tests.Data;

public sealed class ProductImageMetadataTests
{
    [Fact]
    public async Task ProductImageUrlCanBeNullOrStored()
    {
        await using var factory = new TestWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var imageProperty = db.Model.FindEntityType(typeof(Product))?.FindProperty("ImageUrl");
        Assert.NotNull(imageProperty);

        var withoutImage = new Product
        {
            Name = "No image",
            Category = "Test",
            Description = string.Empty,
            Price = 100
        };
        var withImage = new Product
        {
            Name = "With image",
            Category = "Test",
            Description = string.Empty,
            Price = 200
        };
        db.Products.AddRange(withoutImage, withImage);
        db.Entry(withImage).Property<string?>("ImageUrl").CurrentValue = "/images/products/test.webp";
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        Assert.Null(await db.Products.Where(product => product.Id == withoutImage.Id)
            .Select(product => EF.Property<string?>(product, "ImageUrl")).SingleAsync());
        Assert.Equal("/images/products/test.webp",
            await db.Products.Where(product => product.Id == withImage.Id)
                .Select(product => EF.Property<string?>(product, "ImageUrl")).SingleAsync());
    }
}
