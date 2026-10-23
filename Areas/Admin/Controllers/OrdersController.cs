using final.Data;
using final.Models;
using final.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace final.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public sealed class OrdersController(ApplicationDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? status, CancellationToken cancellationToken)
    {
        IQueryable<Order> orders = db.Orders.AsNoTracking().Include(order => order.Items);
        if (Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsedStatus) &&
            Enum.IsDefined(parsedStatus))
        {
            orders = orders.Where(order => order.Status == parsedStatus);
        }

        ViewData["StatusFilter"] = status;
        return View(await orders.OrderByDescending(order => order.CreatedAtUtc)
            .ThenByDescending(order => order.Id)
            .ToListAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var order = await GetOrderAsync(id, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        ViewData["Order"] = order;
        return View(new EditOrderInputModel
        {
            CustomerName = order.CustomerName,
            Phone = order.Phone,
            DeliveryAddress = order.DeliveryAddress,
            Note = order.Note,
            Status = order.Status
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id, EditOrderInputModel input, CancellationToken cancellationToken)
    {
        var order = await db.Orders.Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            ViewData["Order"] = order;
            return View(input);
        }

        order.CustomerName = input.CustomerName.Trim();
        order.Phone = input.Phone.Trim();
        order.DeliveryAddress = input.DeliveryAddress.Trim();
        order.Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        order.Status = input.Status;
        await db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var order = await GetOrderAsync(id, cancellationToken);
        return order is null ? NotFound() : View(order);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
    {
        var order = await db.Orders.FindAsync([id], cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        db.Orders.Remove(order);
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    private Task<Order?> GetOrderAsync(int id, CancellationToken cancellationToken) =>
        db.Orders.AsNoTracking().Include(order => order.Items)
            .SingleOrDefaultAsync(order => order.Id == id, cancellationToken);
}
