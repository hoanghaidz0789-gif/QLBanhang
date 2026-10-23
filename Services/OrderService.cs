using final.Data;
using final.Models;
using final.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace final.Services;

public sealed class OrderService(ApplicationDbContext db)
{
    private const decimal MaxOrderTotal = 9_999_999_999_999_999.99m;

    public async Task<(Order? Order, string? ErrorMessage)> PlaceOrderAsync(
        PlaceOrderInputModel input, CancellationToken cancellationToken)
    {
        var productIds = input.Items.Select(item => item.ProductId).Distinct().ToArray();
        var products = await db.Products.AsNoTracking()
            .Where(product => productIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        if (products.Count != productIds.Length)
        {
            return (null, "Sản phẩm trong giỏ hàng không còn hợp lệ. Vui lòng chọn lại.");
        }

        var order = new Order
        {
            CustomerName = input.CustomerName.Trim(),
            Phone = input.Phone.Trim(),
            DeliveryAddress = input.DeliveryAddress.Trim(),
            Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            ConfirmationToken = Guid.NewGuid(),
            Items = input.Items.Select(item =>
            {
                var product = products[item.ProductId];
                return new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price
                };
            }).ToList()
        };
        decimal total = 0;
        foreach (var item in order.Items)
        {
            var lineTotal = item.UnitPrice * item.Quantity;
            if (lineTotal > MaxOrderTotal - total)
            {
                return (null, "Tổng đơn hàng vượt quá giới hạn cho phép.");
            }

            total += lineTotal;
        }

        order.TotalAmount = total;

        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        return (order, null);
    }
}
