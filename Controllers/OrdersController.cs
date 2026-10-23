using final.Data;
using final.Models;
using final.Services;
using final.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace final.Controllers;

public sealed class OrdersController(ApplicationDbContext db, OrderService orderService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Checkout(CancellationToken cancellationToken)
    {
        await SetProductsAsync(cancellationToken);
        return View(new PlaceOrderInputModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(
        PlaceOrderInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await SetProductsAsync(cancellationToken);
            return View(input);
        }

        var (order, errorMessage) = await orderService.PlaceOrderAsync(input, cancellationToken);
        if (order is null)
        {
            ModelState.AddModelError(nameof(input.Items), errorMessage
                ?? "Không thể đặt đơn hàng này. Vui lòng thử lại.");
            await SetProductsAsync(cancellationToken);
            return View(input);
        }

        return RedirectToAction(
            nameof(Confirmation),
            new { id = order.Id, token = order.ConfirmationToken });
    }

    [HttpGet]
    public async Task<IActionResult> Confirmation(
        int id, Guid token, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(item => item.Items)
            .SingleOrDefaultAsync(
                item => item.Id == id && item.ConfirmationToken == token,
                cancellationToken);
        return order is null ? NotFound() : View(order);
    }

    private async Task SetProductsAsync(CancellationToken cancellationToken)
    {
        ViewData["Products"] = await db.Products.AsNoTracking()
            .OrderBy(product => product.Name)
            .ToListAsync(cancellationToken);
    }
}
