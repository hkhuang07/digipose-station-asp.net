using DigiPOSE.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DigiPOSE.Areas.Catalog.Controllers
{
    [Area("Catalog")]
    [Authorize(Roles = "Super Admin, Administrator, Tenant Manager, Catalog")]
    public class HomeController : Controller
    {
        private readonly DigiPoseDbContext _context;

        public HomeController(DigiPoseDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.TotalProducts = await _context.Products.CountAsync();
            ViewBag.TotalCategories = await _context.Categories.CountAsync();
            ViewBag.TotalManufacturers = await _context.Manufacturers.CountAsync();
            ViewBag.TotalUnits = await _context.Units.CountAsync();

            var recentProducts = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Manufacturer)
                .Include(p => p.Unit)
                .OrderByDescending(p => p.ProductId)
                .Take(8)
                .ToListAsync();

            return View(recentProducts);
        }
    }
}
