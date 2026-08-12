using DigiPOSE.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace DigiPOSE.Controllers
{
    public class HomeController : Controller
    {
        private readonly DigiPoseDbContext _context;

        public HomeController(DigiPoseDbContext context)
        {
            _context = context;
        }

        // Public Landing Page & Corporate Storefront Portal
        [AllowAnonymous]
        public IActionResult Index()
        {
            // Cho phép cả khách chưa đăng nhập lẫn Quản trị viên/Khách hàng đã đăng nhập trải nghiệm trang chủ Storefront & B2B Portal
            return View();
        }

        // GET: /Home/Introduce (Giới thiệu giải pháp ERP & Hệ sinh thái POS)
        [AllowAnonymous]
        public IActionResult Introduce()
        {
            return View();
        }

        // GET: /Home/Product (Danh mục thiết bị và báo giá Gói dịch vụ POS DIGITAL)
        [AllowAnonymous]
        public IActionResult Product()
        {
            return View();
        }

        // GET: /Home/Contact (Cổng liên hệ Hỗ trợ Kỹ thuật & NOC 24/7)
        [AllowAnonymous]
        public IActionResult Contact()
        {
            return View();
        }

        // GET: /Home/Careers or /Home/TuyenDung (Trang tuyển dụng nhân sự chiến lược)
        [AllowAnonymous]
        [Route("Home/Careers")]
        [Route("Home/TuyenDung")]
        [Route("TuyenDung")]
        public IActionResult Careers()
        {
            return View();
        }

        // Protected Router for authenticated users (Called post-login or via Dashboard button)
        [Authorize]
        public IActionResult DashboardRouter()
        {
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            return userRole switch
            {
                "Super Admin" => RedirectToAction("Index", "Home", new { Area = "Administrator" }),
                "Administrator" => RedirectToAction("Index", "Home", new { Area = "Administrator" }),
                "Tenant Manager" => RedirectToAction("Index", "Home", new { Area = "Administrator" }),
                "POS Operator" => RedirectToAction("Index", "POS", new { Area = "" }), 
                "Warehouse" => RedirectToAction("Index", "Home", new { Area = "Warehouse" }), 
                "Catalog" => RedirectToAction("Index", "Home", new { Area = "Catalog" }), 
                "Accountant" => RedirectToAction("Index", "Home", new { Area = "Accountant" }), 
                "Pending Approval" => RedirectToAction("Index", "Profile", new { Area = "" }), 
                "User" => RedirectToAction("Index", "Storefront", new { Area = "" }), 
                _ => RedirectToAction("Index", "Storefront", new { Area = "" })
            };
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
