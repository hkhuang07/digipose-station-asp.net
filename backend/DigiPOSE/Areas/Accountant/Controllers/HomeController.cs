using DigiPOSE.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DigiPOSE.Areas.Accountant.Controllers
{
    [Area("Accountant")]
    [Authorize(Roles = "Super Admin, Administrator, Tenant Manager, Accountant")]
    public class HomeController : Controller
    {
        private readonly DigiPoseDbContext _context;

        public HomeController(DigiPoseDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;
            ViewBag.TodayRevenue = await _context.Orders.Where(o => o.CreatedAt >= today).SumAsync(o => (decimal?)o.TotalAmount) ?? 0;
            ViewBag.TodayOrdersCount = await _context.Orders.Where(o => o.CreatedAt >= today).CountAsync();
            ViewBag.TotalInvoices = await _context.Invoices.CountAsync();
            ViewBag.TotalRetailDocs = await _context.Retails.CountAsync();

            var recentOrders = await _context.Orders
                .Include(o => o.PaymentMethod)
                .Include(o => o.User)
                .Include(o => o.Customer)
                .OrderByDescending(o => o.CreatedAt)
                .Take(8)
                .ToListAsync();

            return View(recentOrders);
        }
    }
}
