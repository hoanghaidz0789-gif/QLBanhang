using System.Net;
using System.Text.RegularExpressions;
using final.Data;
using final.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace final.Tests.Orders;

public sealed class OrderAuthorizationTests
{
    [Fact]
    public async Task AnonymousUserIsRedirectedAwayFromOrderManager()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Admin/Orders");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/AdminAccount/Login", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignedInNonAdminIsDeniedOrderManagerAccess()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });
        await CreateUserAsync(factory, isAdmin: false);
        await LoginAsync(client, "shopper@example.test");

        var response = await client.GetAsync("/Admin/Orders");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("AccessDenied", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminCanListAndUpdateCustomerAndStatusWithoutChangingPurchaseSnapshot()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });
        await CreateUserAsync(factory, isAdmin: true);
        await LoginAsync(client, "shop-admin@example.test");
        var orderId = await CreateOrderAsync(factory);

        var listResponse = await client.GetAsync("/Admin/Orders");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Contains("Nguyen Van An", await listResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var editPage = await client.GetAsync($"/Admin/Orders/Edit/{orderId}");
        var editHtml = await editPage.Content.ReadAsStringAsync();
        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = GetAntiforgeryToken(editHtml),
            ["CustomerName"] = "Tran Minh Anh",
            ["Phone"] = "0912345678",
            ["DeliveryAddress"] = "2 Le Loi, Da Nang",
            ["Note"] = "Giao buổi sáng",
            ["Status"] = nameof(OrderStatus.Shipping)
        };

        var editResponse = await client.PostAsync(
            $"/Admin/Orders/Edit/{orderId}", new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.Redirect, editResponse.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var updated = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Orders.Include(order => order.Items).SingleAsync(order => order.Id == orderId);
        Assert.Equal("Tran Minh Anh", updated.CustomerName);
        Assert.Equal("0912345678", updated.Phone);
        Assert.Equal(OrderStatus.Shipping, updated.Status);
        Assert.Equal(22_990_000m, updated.Items.Single().UnitPrice);
        Assert.Equal(22_990_000m, updated.TotalAmount);
    }

    [Fact]
    public async Task AdminMustConfirmDeleteAndCannotAccessDeletedOrder()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });
        await CreateUserAsync(factory, isAdmin: true);
        await LoginAsync(client, "shop-admin@example.test");
        var orderId = await CreateOrderAsync(factory);
        using var deletePage = await client.GetAsync($"/Admin/Orders/Delete/{orderId}");
        var deleteHtml = await deletePage.Content.ReadAsStringAsync();

        var response = await client.PostAsync(
            $"/Admin/Orders/Delete/{orderId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = GetAntiforgeryToken(deleteHtml)
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Orders.AnyAsync(order => order.Id == orderId));
    }

    private static async Task CreateUserAsync(TestWebApplicationFactory factory, bool isAdmin)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var users = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var username = isAdmin ? "shop-admin@example.test" : "shopper@example.test";
        var user = new ApplicationUser { UserName = username, Email = username };
        var createResult = await users.CreateAsync(user, "Shop!Test12345");
        Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Description)));

        if (isAdmin)
        {
            var roles = provider.GetRequiredService<RoleManager<IdentityRole>>();
            var roleResult = await roles.CreateAsync(new IdentityRole("Admin"));
            Assert.True(roleResult.Succeeded, string.Join("; ", roleResult.Errors.Select(error => error.Description)));
            var assignment = await users.AddToRoleAsync(user, "Admin");
            Assert.True(assignment.Succeeded, string.Join("; ", assignment.Errors.Select(error => error.Description)));
        }
    }

    private static async Task LoginAsync(HttpClient client, string username)
    {
        using var loginPage = await client.GetAsync("/AdminAccount/Login?ReturnUrl=%2FAdmin%2FOrders");
        var html = await loginPage.Content.ReadAsStringAsync();
        var response = await client.PostAsync(
            "/AdminAccount/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = GetAntiforgeryToken(html),
                ["Username"] = username,
                ["Password"] = "Shop!Test12345",
                ["ReturnUrl"] = "/Admin/Orders"
            }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Admin/Orders", response.Headers.Location!.OriginalString);
    }

    private static string GetAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        Assert.True(match.Success, "The page did not render an antiforgery token.");
        return match.Groups[1].Value;
    }

    private static async Task<int> CreateOrderAsync(TestWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await db.Products.OrderBy(item => item.Id).FirstAsync();
        var order = new Order
        {
            CustomerName = "Nguyen Van An",
            Phone = "0901234567",
            DeliveryAddress = "1 Nguyen Hue, Ho Chi Minh City",
            CreatedAtUtc = DateTime.UtcNow,
            ConfirmationToken = Guid.NewGuid(),
            TotalAmount = product.Price,
            Items =
            [
                new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Quantity = 1,
                    UnitPrice = product.Price
                }
            ]
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }
}
