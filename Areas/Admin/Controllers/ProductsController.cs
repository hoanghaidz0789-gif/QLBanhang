using final.Data;
using final.Models;
using final.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace final.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public sealed class ProductsController(
    ApplicationDbContext db,
    IWebHostEnvironment environment) : Controller
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private const string ProductImagePath = "/images/products/";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await db.Products.AsNoTracking()
            .OrderBy(product => product.Category)
            .ThenBy(product => product.Name)
            .ToListAsync(cancellationToken));

    [HttpGet]
    public IActionResult Create() => View(new AdminProductInputModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        AdminProductInputModel input, CancellationToken cancellationToken)
    {
        await ValidateImageAsync(input.ImageFile, nameof(input.ImageFile), cancellationToken);
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var product = new Product
        {
            Name = input.Name.Trim(),
            Description = input.Description.Trim(),
            Category = input.Category.Trim(),
            Price = input.Price,
            ImageUrl = input.ImageFile is null
                ? null
                : await SaveImageAsync(input.ImageFile, cancellationToken)
        };

        db.Products.Add(product);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            DeleteGeneratedImage(product.ImageUrl);
            throw;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var product = await db.Products.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        ViewData["CurrentImageUrl"] = product.ImageUrl;
        return View(ToInputModel(product));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id, AdminProductInputModel input, CancellationToken cancellationToken)
    {
        var product = await db.Products.SingleOrDefaultAsync(
            item => item.Id == id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        await ValidateImageAsync(input.ImageFile, nameof(input.ImageFile), cancellationToken);
        if (!ModelState.IsValid)
        {
            ViewData["CurrentImageUrl"] = product.ImageUrl;
            return View(input);
        }

        var oldImageUrl = product.ImageUrl;
        var newImageUrl = input.ImageFile is null
            ? null
            : await SaveImageAsync(input.ImageFile, cancellationToken);

        product.Name = input.Name.Trim();
        product.Description = input.Description.Trim();
        product.Category = input.Category.Trim();
        product.Price = input.Price;
        if (newImageUrl is not null)
        {
            product.ImageUrl = newImageUrl;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            DeleteGeneratedImage(newImageUrl);
            throw;
        }

        if (newImageUrl is not null)
        {
            DeleteGeneratedImage(oldImageUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var product = await db.Products.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return product is null ? NotFound() : View(product);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
    {
        var product = await db.Products.SingleOrDefaultAsync(
            item => item.Id == id, cancellationToken);
        if (product is null)
        {
            return NotFound();
        }

        if (await db.OrderItems.AnyAsync(item => item.ProductId == id, cancellationToken))
        {
            ModelState.AddModelError(string.Empty, "Không thể xóa sản phẩm đã có trong đơn hàng.");
            return View("Delete", product);
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync(cancellationToken);
        DeleteGeneratedImage(product.ImageUrl);
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateImageAsync(
        IFormFile? image, string fieldName, CancellationToken cancellationToken)
    {
        if (image is null)
        {
            return;
        }

        if (image.Length is <= 0 or > MaxImageBytes)
        {
            ModelState.AddModelError(fieldName, "Ảnh phải có dung lượng lớn hơn 0 và không quá 5 MB.");
            return;
        }

        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        var contentType = image.ContentType.ToLowerInvariant();
        var expectedContentType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null
        };

        if (expectedContentType is null ||
            !string.Equals(contentType, expectedContentType, StringComparison.Ordinal) ||
            !await HasMatchingSignatureAsync(image, extension, cancellationToken))
        {
            ModelState.AddModelError(fieldName, "Chỉ chấp nhận ảnh JPG, PNG hoặc WebP hợp lệ.");
        }
    }

    private static async Task<bool> HasMatchingSignatureAsync(
        IFormFile image, string extension, CancellationToken cancellationToken)
    {
        var header = new byte[12];
        await using var stream = image.OpenReadStream();
        var bytesRead = await stream.ReadAsync(header, cancellationToken);
        return extension switch
        {
            ".jpg" or ".jpeg" => bytesRead >= 3 &&
                header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff,
            ".png" => bytesRead >= 8 &&
                header.AsSpan(0, 8).SequenceEqual(
                    new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
            ".webp" => bytesRead >= 12 &&
                header.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
    }

    private async Task<string> SaveImageAsync(IFormFile image, CancellationToken cancellationToken)
    {
        var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(image.FileName).ToLowerInvariant()}";
        var directory = Path.Combine(environment.WebRootPath, "images", "products");
        Directory.CreateDirectory(directory);
        await using var stream = new FileStream(
            Path.Combine(directory, fileName), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await image.CopyToAsync(stream, cancellationToken);
        return ProductImagePath + fileName;
    }

    private void DeleteGeneratedImage(string? imageUrl)
    {
        if (imageUrl is null || !imageUrl.StartsWith(ProductImagePath, StringComparison.Ordinal))
        {
            return;
        }

        var fileName = imageUrl[ProductImagePath.Length..];
        var extension = Path.GetExtension(fileName);
        if (Path.GetFileName(fileName) != fileName ||
            !Guid.TryParseExact(Path.GetFileNameWithoutExtension(fileName), "N", out _) ||
            extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
        {
            return;
        }

        var path = Path.Combine(environment.WebRootPath, "images", "products", fileName);
        if (System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);
        }
    }

    private static AdminProductInputModel ToInputModel(Product product) => new()
    {
        Name = product.Name,
        Description = product.Description,
        Category = product.Category,
        Price = product.Price
    };
}
