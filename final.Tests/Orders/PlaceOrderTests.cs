using System.Net;
using System.Text.RegularExpressions;
using final.Data;
using final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace final.Tests.Orders;

public sealed class PlaceOrderTests
{
    [Fact]
    public async Task PlaceOrderUsesServerPriceAndShowsCustomerConfirmation()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        var result = await SubmitOrder(client, new Dictionary<string, string>
        {
            ["CustomerName"] = "Nguyen Van An",
            ["Phone"] = "0901234567",
            ["DeliveryAddress"] = "1 Nguyen Hue, Ho Chi Minh City",
            ["Items[0].ProductId"] = "1",
            ["Items[0].Quantity"] = "2",
            ["Items[0].Price"] = "1"
        });

        Assert.Equal(HttpStatusCode.OK, result.Response.StatusCode);
        var html = WebUtility.HtmlDecode(await result.Response.Content.ReadAsStringAsync());
        Assert.Contains("Nguyen Van An", html, StringComparison.Ordinal);
        Assert.Contains("0901234567", html, StringComparison.Ordinal);
        Assert.Contains("1 Nguyen Hue", html, StringComparison.Ordinal);
        Assert.Contains("45.980.000", html, StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var order = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Orders.Include(item => item.Items).SingleAsync();
        Assert.Equal(45_980_000m, order.TotalAmount);
        Assert.Equal(22_990_000m, Assert.Single(order.Items).UnitPrice);
    }

    [Theory]
    [InlineData("CustomerName", "", "Họ và tên")]
    [InlineData("Phone", "not-a-phone", "Số điện thoại")]
    [InlineData("DeliveryAddress", "", "Địa chỉ nhận hàng")]
    public async Task InvalidRequiredCustomerFieldDoesNotSaveOrder(
        string field, string value, string errorMessage)
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        var fields = ValidFields();
        fields[field] = value;
        var result = await SubmitOrder(client, fields);
        var html = WebUtility.HtmlDecode(await result.Response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, result.Response.StatusCode);
        Assert.True(html.Contains(errorMessage, StringComparison.Ordinal),
            $"The validation response did not include '{errorMessage}': {html}");
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Orders.CountAsync());
    }

    [Fact]
    public async Task EmptyCartDoesNotSaveOrder()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        var result = await SubmitOrder(client, ValidFields());

        Assert.Equal(HttpStatusCode.OK, result.Response.StatusCode);
        Assert.Contains("Chọn ít nhất một sản phẩm",
            WebUtility.HtmlDecode(await result.Response.Content.ReadAsStringAsync()), StringComparison.Ordinal);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Orders.CountAsync());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task NonPositiveQuantityDoesNotSaveOrder(string quantity)
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        var fields = ValidFields();
        fields["Items[0].ProductId"] = "1";
        fields["Items[0].Quantity"] = quantity;

        var result = await SubmitOrder(client, fields);

        Assert.Equal(HttpStatusCode.OK, result.Response.StatusCode);
        Assert.Contains("Số lượng", WebUtility.HtmlDecode(await result.Response.Content.ReadAsStringAsync()), StringComparison.Ordinal);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Orders.CountAsync());
    }

    [Fact]
    public async Task UnknownProductDoesNotSaveOrder()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        var fields = ValidFields();
        fields["Items[0].ProductId"] = "99999";
        fields["Items[0].Quantity"] = "1";

        var result = await SubmitOrder(client, fields);

        Assert.Equal(HttpStatusCode.OK, result.Response.StatusCode);
        Assert.Contains("Sản phẩm trong giỏ hàng không còn hợp lệ",
            WebUtility.HtmlDecode(await result.Response.Content.ReadAsStringAsync()), StringComparison.Ordinal);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Orders.CountAsync());
    }

    [Fact]
    public async Task OrderTotalAboveDatabaseLimitReturnsValidationErrorWithoutSaving()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        var fields = ValidFieldsWithOneProduct();
        fields["Items[0].Quantity"] = int.MaxValue.ToString();

        var result = await SubmitOrder(client, fields);
        var html = WebUtility.HtmlDecode(await result.Response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, result.Response.StatusCode);
        Assert.Contains("Tổng đơn hàng vượt quá giới hạn cho phép",
            html, StringComparison.Ordinal);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Orders.CountAsync());
    }

    [Fact]
    public async Task OrderIdAloneCannotAccessOrderConfirmation()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });
        await SubmitOrder(client, ValidFieldsWithOneProduct());
        int orderId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            orderId = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Orders.Select(order => order.Id).SingleAsync();
        }

        var response = await client.GetAsync($"/Orders/Confirmation/{orderId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static Dictionary<string, string> ValidFields() => new()
    {
        ["CustomerName"] = "Nguyen Van An",
        ["Phone"] = "0901234567",
        ["DeliveryAddress"] = "1 Nguyen Hue, Ho Chi Minh City",
        ["Note"] = ""
    };

    private static Dictionary<string, string> ValidFieldsWithOneProduct()
    {
        var fields = ValidFields();
        fields["Items[0].ProductId"] = "1";
        fields["Items[0].Quantity"] = "1";
        return fields;
    }

    private static async Task<(HttpResponseMessage Response, string ConfirmationUrl)> SubmitOrder(
        HttpClient client, Dictionary<string, string> fields)
    {
        using var checkoutResponse = await client.GetAsync("/Orders/Checkout");
        var checkoutHtml = await checkoutResponse.Content.ReadAsStringAsync();
        var token = Regex.Match(
            checkoutHtml,
            "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token), $"The checkout page did not render an antiforgery token: {checkoutHtml}");
        var form = new Dictionary<string, string>(fields)
        {
            ["__RequestVerificationToken"] = token
        };

        var response = await client.PostAsync("/Orders/Checkout", new FormUrlEncodedContent(form));
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new InvalidOperationException(
                $"Checkout rejected the request: {await response.Content.ReadAsStringAsync()}");
        }

        return (response, response.RequestMessage?.RequestUri?.ToString() ?? "");
    }
}
