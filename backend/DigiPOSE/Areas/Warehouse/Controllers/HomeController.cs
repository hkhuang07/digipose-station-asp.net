using DigiPOSE.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DigiPOSE.Areas.Warehouse.Controllers
{
    [Area("Warehouse")]
    [Authorize(Roles = "Super Admin, Administrator, Tenant Manager, Warehouse")]
    public class HomeController : Controller
    {
        private readonly DigiPoseDbContext _context;

        public HomeController(DigiPoseDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.TotalInventoryItems = await _context.ProductInventories.SumAsync(i => (decimal?)i.StockQuantity) ?? 0;
            ViewBag.TotalStockVouchers = await _context.StockVouchers.CountAsync();
            ViewBag.TotalStockTransfers = await _context.StockTransfers.CountAsync();
            ViewBag.TotalSuppliers = await _context.Suppliers.CountAsync();

            var recentVouchers = await _context.StockVouchers
                .Include(v => v.Supplier)
                .OrderByDescending(v => v.CreatedAt)
                .Take(8)
                .ToListAsync();

            return View(recentVouchers);
        }
    }
}
