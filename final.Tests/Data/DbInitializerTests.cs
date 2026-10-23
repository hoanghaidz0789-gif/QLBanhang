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
    public async Task SeesFourProductsOnlyOnced()
    {
        await using var factory = new TestWebApplicationFactory();
        _ = factory.CreateClient();
        var configuration = factory.Services.GetRequiredService<IConfiguration>();

        await DbInitializer.InitializeAsync(factory.Services, configuration, CancellationToken.None);
        await DbInitializer.InitializeAsync(factory.Services, configuration, CancellationToken.None);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(4, await db.Products.CountAsync());
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
