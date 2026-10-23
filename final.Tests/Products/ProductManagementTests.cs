using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using final.Data;
using final.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace final.Tests.Products;

public sealed class ProductManagementTests
{
    private const int MaxImageBytes = 5 * 1024 * 1024;

    [Fact]
    public async Task AnonymousUsersCannotAccessProductManagementRoutes()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = CreateClient(factory);
        var loginHtml = await client.GetStringAsync("/AdminAccount/Login");
        var token = GetAntiforgeryToken(loginHtml);

        foreach (var path in ProductGetPaths)
        {
            using var response = await client.GetAsync(path);
            AssertLoginRedirect(response);
        }

        foreach (var path in ProductPostPaths)
        {
            using var response = await client.PostAsync(path, Form(token));
            AssertLoginRedirect(response);
        }
    }

    [Fact]
    public async Task NonAdminUsersCannotAccessProductManagementRoutes()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = CreateClient(factory);
        var token = await CreateUserAndLoginAsync(factory, client, isAdmin: false);

        foreach (var path in ProductGetPaths)
        {
            using var response = await client.GetAsync(path);
            AssertAccessDeniedRedirect(response);
        }

        foreach (var path in ProductPostPaths)
        {
            using var response = await client.PostAsync(path, Form(token));
            AssertAccessDeniedRedirect(response);
        }
    }

    [Fact]
    public async Task AdminCanCreateProductAndUploadJpegImage()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = CreateClient(factory);
        await CreateUserAndLoginAsync(factory, client, isAdmin: true);
        using var createPage = await client.GetAsync("/Admin/Products/Create");
        Assert.Equal(HttpStatusCode.OK, createPage.StatusCode);
        var html = await createPage.Content.ReadAsStringAsync();
        Assert.Contains("enctype=\"multipart/form-data\"", html, StringComparison.Ordinal);
        Assert.Contains("type=\"file\"", html, StringComparison.Ordinal);
        var form = ProductForm(GetAntiforgeryToken(html), "Sample phone", 3_499_000m);
        AddImage(form, "phone.jpg", "image/jpeg", JpegHeader);

        string? imageUrl = null;
        try
        {
            using var response = await client.PostAsync("/Admin/Products/Create", form);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Admin/Products", response.Headers.Location!.OriginalString, StringComparison.Ordinal);

            await using var scope = factory.Services.CreateAsyncScope();
            var product = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Products.SingleAsync(item => item.Name == "Sample phone");
            imageUrl = product.ImageUrl;
            Assert.Equal("Điện thoại", product.Category);
            Assert.Equal(3_499_000m, product.Price);
            Assert.Matches("^/images/products/[0-9a-f]{32}\\.jpg$", imageUrl);
            var file = ImageFilePath(scope.ServiceProvider, imageUrl!);
            Assert.True(File.Exists(file));
            Assert.Equal(JpegHeader, await File.ReadAllBytesAsync(file));
            using var imageResponse = await client.GetAsync(imageUrl);
            Assert.Equal(HttpStatusCode.OK, imageResponse.StatusCode);
            Assert.Equal(JpegHeader, await imageResponse.Content.ReadAsByteArrayAsync());
        }
        finally
        {
            if (imageUrl is not null)
            {
                await DeleteImageAsync(factory, imageUrl);
            }
        }
    }

    [Fact]
    public async Task AdminNavigationLinksToProductManagement()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = CreateClient(factory);
        await CreateUserAndLoginAsync(factory, client, isAdmin: true);

        var html = await client.GetStringAsync("/");

        Assert.Contains("href=\"/Admin/Products\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AdminProductListRendersWorkingManagementLinksAndSharedLayout()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = CreateClient(factory);
        await CreateUserAndLoginAsync(factory, client, isAdmin: true);
        var productId = await CreateProductAsync(factory);

        var html = await client.GetStringAsync("/Admin/Products");

        Assert.Contains("class=\"site-header\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/css/admin.css", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/Admin/Products/Create\"", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Admin/Products/Edit/{productId}\"", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Admin/Products/Delete/{productId}\"", html, StringComparison.Ordinal);

        foreach (var path in new[]
        {
            "/Admin/Products/Create",
            $"/Admin/Products/Edit/{productId}",
            $"/Admin/Products/Delete/{productId}"
        })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        using var stylesheet = await client.GetAsync("/css/admin.css");
        Assert.Equal(HttpStatusCode.OK, stylesheet.StatusCode);
    }

    [Theory]
    [InlineData("picture.png", "image/png", "png")]
    [InlineData("picture.webp", "image/webp", "webp")]
    public async Task AdminCanUploadSupportedPngAndWebpImages(
        string fileName, string contentType, string imageType)
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = CreateClient(factory);
        await CreateUserAndLoginAsync(factory, client, isAdmin: true);
        using var createPage = await client.GetAsync("/Admin/Products/Create");
        var form = ProductForm(
            GetAntiforgeryToken(await createPage.Content.ReadAsStringAsync()),
            $"Product {imageType}", 1_000_000m);
        AddImage(form, fileName, contentType, ValidImageHeader(imageType));

        string? imageUrl = null;
        try
        {
            using var response = await client.PostAsync("/Admin/Products/Create", form);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            await using var scope = factory.Services.CreateAsyncScope();
            imageUrl = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Products.Where(item => item.Name == $"Product {imageType}")
                .Select(item => item.ImageUrl).SingleAsync();
            Assert.EndsWith($".{imageType}", imageUrl, StringComparison.Ordinal);
        }
        finally
        {
            if (imageUrl is not null)
            {
                await DeleteImageAsync(factory, imageUrl);
            }
        }
    }

    [Theory]
    [InlineData("not-an-image.txt", "text/plain", "not an image")]
    [InlineData("wrong-type.png", "image/png", "not an image")]
    public async Task UnsupportedOrMismatchedImageIsRejectedWithoutSavingProduct(
        string fileName, string contentType, string content)
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = CreateClient(factory);
        await CreateUserAndLoginAsync(factory, client, isAdmin: true);
        using var createPage = await client.GetAsync("/Admin/Products/Create");
        var form = ProductForm(
            GetAntiforgeryToken(await createPage.Content.ReadAsStringAsync()),
            "Invalid image product", 100_000m);
        AddImage(form, fileName, contentType, System.Text.Encoding.UTF8.GetBytes(content));

        using var response = await client.PostAsync("/Admin/Products/Create", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("JPG, PNG hoặc WebP", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Products.AnyAsync(item => item.Name == "Invalid image product"));
    }

    [Fact]
    public async Task ImageOverFiveMegabytesIsRejectedWithoutSavingProduct()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = CreateClient(factory);
        await CreateUserAndLoginAsync(factory, client, isAdmin: true);
        using var createPage = await client.GetAsync("/Admin/Products/Create");
        var form = ProductForm(
            GetAntiforgeryToken(await createPage.Content.ReadAsStringAsync()),
            "Large image product", 100_000m);
        AddImage(form, "large.jpg", "image/jpeg", new byte[MaxImageBytes + 1]);

        using var response = await client.PostAsync("/Admin/Products/Create", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("5 MB", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Products.AnyAsync(item => item.Name == "Large image product"));
    }

    [Fact]
    public async Task InvalidEditKeepsCurrentImageAndValidEditKeepsPurchaseSnapshot()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = CreateClient(factory);
        await CreateUserAndLoginAsync(factory, client, isAdmin: true);
        var productId = await CreateProductWithImageAsync(factory, JpegHeader);
        var orderId = await CreateOrderAsync(factory, productId);
        var originalImageUrl = await GetImageUrlAsync(factory, productId);
        string? replacementImageUrl = null;

        try
        {
            using (var editPage = await client.GetAsync($"/Admin/Products/Edit/{productId}"))
            {
                var form = ProductForm(
                    GetAntiforgeryToken(await editPage.Content.ReadAsStringAsync()), string.Empty, 4_000_000m);
                using var response = await client.PostAsync($"/Admin/Products/Edit/{productId}", form);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            Assert.Equal(originalImageUrl, await GetImageUrlAsync(factory, productId));

            using (var editPage = await client.GetAsync($"/Admin/Products/Edit/{productId}"))
            {
                var form = ProductForm(
                    GetAntiforgeryToken(await editPage.Content.ReadAsStringAsync()), "Updated product", 4_000_000m);
                AddImage(form, "replacement.png", "image/png", ValidImageHeader("png"));
                using var response = await client.PostAsync($"/Admin/Products/Edit/{productId}", form);
                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            }

            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var order = await db.Orders.Include(item => item.Items).SingleAsync(item => item.Id == orderId);
            Assert.Equal("Updated product", (await db.Products.SingleAsync(item => item.Id == productId)).Name);
            replacementImageUrl = await db.Products.Where(item => item.Id == productId)
                .Select(item => item.ImageUrl).SingleAsync();
            Assert.NotEqual(originalImageUrl, replacementImageUrl);
            Assert.EndsWith(".png", replacementImageUrl, StringComparison.Ordinal);
            Assert.False(File.Exists(ImageFilePath(scope.ServiceProvider, originalImageUrl!)));
            Assert.True(File.Exists(ImageFilePath(scope.ServiceProvider, replacementImageUrl!)));
            Assert.Equal(2_990_000m, order.Items.Single().UnitPrice);
            Assert.Equal(2_990_000m, order.TotalAmount);
        }
        finally
        {
            if (originalImageUrl is not null)
            {
                await DeleteImageAsync(factory, originalImageUrl);
            }
            if (replacementImageUrl is not null)
            {
                await DeleteImageAsync(factory, replacementImageUrl);
            }
        }
    }

    [Fact]
    public async Task ProductUsedByOrderCannotBeDeleted()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = CreateClient(factory);
        await CreateUserAndLoginAsync(factory, client, isAdmin: true);
        var productId = await CreateProductAsync(factory);
        await CreateOrderAsync(factory, productId);
        using var deletePage = await client.GetAsync($"/Admin/Products/Delete/{productId}");
        var form = Form(GetAntiforgeryToken(await deletePage.Content.ReadAsStringAsync()));

        using var response = await client.PostAsync($"/Admin/Products/Delete/{productId}", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("đã có trong đơn hàng", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Products.AnyAsync(item => item.Id == productId));
    }

    [Fact]
    public async Task UnusedProductCanBeDeletedAfterConfirmation()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = CreateClient(factory);
        await CreateUserAndLoginAsync(factory, client, isAdmin: true);
        var productId = await CreateProductWithImageAsync(factory, JpegHeader);
        var imageUrl = await GetImageUrlAsync(factory, productId);

        using var deletePage = await client.GetAsync($"/Admin/Products/Delete/{productId}");
        var form = Form(GetAntiforgeryToken(await deletePage.Content.ReadAsStringAsync()));
        using var response = await client.PostAsync($"/Admin/Products/Delete/{productId}", form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Products.AnyAsync(item => item.Id == productId));
        Assert.False(File.Exists(ImageFilePath(scope.ServiceProvider, imageUrl!)));
    }

    private static readonly string[] ProductGetPaths =
    [
        "/Admin/Products",
        "/Admin/Products/Create",
        "/Admin/Products/Edit/1",
        "/Admin/Products/Delete/1"
    ];

    private static readonly string[] ProductPostPaths =
    [
        "/Admin/Products/Create",
        "/Admin/Products/Edit/1",
        "/Admin/Products/Delete/1"
    ];

    private static readonly byte[] JpegHeader = [0xff, 0xd8, 0xff, 0xe0, 0x00, 0x10];

    private static byte[] ValidImageHeader(string type) => type switch
    {
        "png" => [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a],
        "webp" => [0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50],
        _ => JpegHeader
    };

    private static HttpClient CreateClient(TestWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

    private static async Task<string> CreateUserAndLoginAsync(
        TestWebApplicationFactory factory, HttpClient client, bool isAdmin)
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = isAdmin ? "product-admin@example.test" : "product-shopper@example.test",
                Email = isAdmin ? "product-admin@example.test" : "product-shopper@example.test"
            };
            var result = await users.CreateAsync(user, "Shop!Test12345");
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));

            if (isAdmin)
            {
                var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var roleResult = await roles.CreateAsync(new IdentityRole("Admin"));
                Assert.True(roleResult.Succeeded,
                    string.Join("; ", roleResult.Errors.Select(error => error.Description)));
                var assignment = await users.AddToRoleAsync(user, "Admin");
                Assert.True(assignment.Succeeded,
                    string.Join("; ", assignment.Errors.Select(error => error.Description)));
            }
        }

        using var loginPage = await client.GetAsync("/AdminAccount/Login?ReturnUrl=%2FAdmin%2FProducts");
        var html = await loginPage.Content.ReadAsStringAsync();
        var token = GetAntiforgeryToken(html);
        using var response = await client.PostAsync(
            "/AdminAccount/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Username"] = isAdmin ? "product-admin@example.test" : "product-shopper@example.test",
                ["Password"] = "Shop!Test12345",
                ["ReturnUrl"] = "/Admin/Products"
            }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return token;
    }

    private static void AssertLoginRedirect(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/AdminAccount/Login", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    private static void AssertAccessDeniedRedirect(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("AccessDenied", response.Headers.Location!.OriginalString, StringComparison.Ordinal);
    }

    private static string GetAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        Assert.True(match.Success, "The page did not render an antiforgery token.");
        return match.Groups[1].Value;
    }

    private static FormUrlEncodedContent Form(string token) =>
        new(new Dictionary<string, string> { ["__RequestVerificationToken"] = token });

    private static MultipartFormDataContent ProductForm(string token, string name, decimal price)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(token), "__RequestVerificationToken");
        form.Add(new StringContent(name), "Name");
        form.Add(new StringContent("Sản phẩm công nghệ kiểm thử"), "Description");
        form.Add(new StringContent("Điện thoại"), "Category");
        form.Add(new StringContent(price.ToString(System.Globalization.CultureInfo.InvariantCulture)), "Price");
        return form;
    }

    private static void AddImage(
        MultipartFormDataContent form, string fileName, string contentType, byte[] bytes)
    {
        var image = new ByteArrayContent(bytes);
        image.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(image, "ImageFile", fileName);
    }

    private static async Task<int> CreateProductAsync(TestWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = new Product
        {
            Name = $"Test product {Guid.NewGuid():N}",
            Description = "Product for order testing",
            Category = "Test",
            Price = 2_990_000m
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private static async Task<int> CreateProductWithImageAsync(
        TestWebApplicationFactory factory, byte[] imageBytes)
    {
        var fileName = $"{Guid.NewGuid():N}.jpg";
        string webRoot;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            webRoot = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>().WebRootPath;
        }

        var path = Path.Combine(webRoot, "images", "products", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, imageBytes);

        await using var dbScope = factory.Services.CreateAsyncScope();
        var db = dbScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = new Product
        {
            Name = $"Test product {Guid.NewGuid():N}",
            Description = "Product for order testing",
            Category = "Test",
            Price = 2_990_000m,
            ImageUrl = $"/images/products/{fileName}"
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private static async Task<int> CreateOrderAsync(TestWebApplicationFactory factory, int productId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await db.Products.SingleAsync(item => item.Id == productId);
        var order = new Order
        {
            CustomerName = "Test customer",
            Phone = "0901234567",
            DeliveryAddress = "Test address",
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

    private static async Task<string?> GetImageUrlAsync(TestWebApplicationFactory factory, int productId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Products.Where(item => item.Id == productId)
            .Select(item => item.ImageUrl).SingleAsync();
    }

    private static string ImageFilePath(IServiceProvider services, string imageUrl)
    {
        var webRoot = services.GetRequiredService<IWebHostEnvironment>().WebRootPath;
        return Path.Combine(webRoot, imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
    }

    private static async Task DeleteImageAsync(TestWebApplicationFactory factory, string imageUrl)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var path = ImageFilePath(scope.ServiceProvider, imageUrl);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
