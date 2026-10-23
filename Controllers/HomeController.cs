using final.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using final.Data;

namespace final.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;

        public HomeController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            ViewData["Categories"] = await _db.Products.AsNoTracking()
                .Select(product => product.Category)
                .Distinct()
                .OrderBy(category => category)
                .ToListAsync(cancellationToken);

            var products = await _db.Products.AsNoTracking()
                .OrderBy(product => product.Name)
                .ToListAsync(cancellationToken);
            return View(products);
        }

        [HttpGet]
        public async Task<IActionResult> Filter(string? category, CancellationToken cancellationToken)
        {
            var products = _db.Products.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(category) && category != "all")
            {
                products = products.Where(product => product.Category == category);
            }

            return PartialView("_ProductCards", await products.OrderBy(product => product.Name)
                .ToListAsync(cancellationToken));
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
