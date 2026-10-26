using final.Data;
using final.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace final.Tests.Data;

public sealed class DbInitializerTests
{
    [Fact]
    public async Task SeedsFiveProductsOnlyOnce()
    {
        await using var factory = new TestWebApplicationFactory();
        _ = factory.CreateClient();
        var configuration = factory.Services.GetRequiredService<IConfiguration>();

        await DbInitializer.InitializeAsync(factory.Services, configuration, CancellationToken.None);
        await DbInitializer.InitializeAsync(factory.Services, configuration, CancellationToken.None);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(5, await db.Products.CountAsync());
    }

    [Fact]
    public async Task AddsConfiguredImageToExistingProductWithoutOverwritingExistingImage()
    {
        await using var factory = new TestWebApplicationFactory();
        _ = factory.CreateClient();
        var configuration = factory.Services.GetRequiredService<IConfiguration>();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var product = await db.Products.SingleAsync(item => item.Name == "Banphim");
            product.ImageUrl = null;
            await db.SaveChangesAsync();
        }

        await DbInitializer.InitializeAsync(factory.Services, configuration, CancellationToken.None);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var product = await db.Products.SingleAsync(item => item.Name == "Banphim");
            Assert.Equal("/images/products/pim.webp", product.ImageUrl);
            product.ImageUrl = "/images/products/custom.webp";
            await db.SaveChangesAsync();
        }

        await DbInitializer.InitializeAsync(factory.Services, configuration, CancellationToken.None);

        await using var finalScope = factory.Services.CreateAsyncScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(
            "/images/products/custom.webp",
            (await finalDb.Products.SingleAsync(product => product.Name == "Banphim")).ImageUrl);
    }

    [Fact]
    public async Task DoesNotCreateAdminWhenCredentialsAreMissing()
    {
        await using var factory = new TestWebApplicationFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.Empty(await users.Users.ToListAsync());
    }
}
