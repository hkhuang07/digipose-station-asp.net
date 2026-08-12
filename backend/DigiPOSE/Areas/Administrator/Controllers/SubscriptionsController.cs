using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using DigiPOSE.Models;
using System.Linq.Dynamic.Core;

namespace DigiPOSE.Areas.Administrator.Controllers
{
    [Area("Administrator")]
    [Authorize(Roles = "Super Admin, Administrator, Tenant Manager")]
    public class SubscriptionsController : Controller
    {
        private readonly DigiPoseDbContext _context;
        public SubscriptionsController(DigiPoseDbContext context) { _context = context; }

        public async Task<IActionResult> Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index_LoadData()
        {
            try
            {
                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault();
                var sortColumn = Request.Form["columns[" + Request.Form["order[0][column]"].FirstOrDefault() + "][name]"].FirstOrDefault();
                var sortColumnDirection = Request.Form["order[0][dir]"].FirstOrDefault();
                var searchValue = Request.Form["search[value]"].FirstOrDefault();
                int pageSize = length != null ? Convert.ToInt32(length) : 0;
                int skip = start != null ? Convert.ToInt32(start) : 0;

                var query = _context.Subscriptions
                    .Include(s => s.Customer)
                    .Include(s => s.Product)
                    .Include(s => s.Order)
                    .AsQueryable();

                int totalRecords = query.Count();

                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(s =>
                        (s.LicenseKey != null && s.LicenseKey.Contains(searchValue)) ||
                        (s.Status != null && s.Status.Contains(searchValue)) ||
                        (s.Customer != null && s.Customer.FullName != null && s.Customer.FullName.Contains(searchValue)) ||
                        (s.Product != null && s.Product.ProductName != null && s.Product.ProductName.Contains(searchValue)));
                }

                int filterRecords = query.Count();

                if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortColumnDirection))
                {
                    query = query.OrderBy(sortColumn + " " + sortColumnDirection);
                }
                else
                {
                    query = query.OrderByDescending(s => s.SubscriptionId);
                }

                var dataList = query.Skip(skip).Take(pageSize).Select(s => new {
                    subscriptionId = s.SubscriptionId,
                    customerName = s.Customer != null ? s.Customer.FullName : "N/A",
                    productName = s.Product != null ? s.Product.ProductName : "N/A",
                    orderId = s.OrderId,
                    startDate = s.StartDate.ToString("yyyy-MM-dd"),
                    endDate = s.EndDate.ToString("yyyy-MM-dd"),
                    licenseKey = s.LicenseKey ?? "N/A",
                    status = s.Status
                }).ToList();

                return Json(new { draw = draw, recordsFiltered = filterRecords, recordsTotal = totalRecords, data = dataList });
            }
            catch (Exception ex)
            {
                return Json(new { error = "An error occurred while loading data. Error: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(string? searchValue)
        {
            var query = _context.Subscriptions.Include(s => s.Customer).Include(s => s.Product).AsQueryable();
            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(s =>
                    (s.LicenseKey != null && s.LicenseKey.Contains(searchValue)) ||
                    (s.Status != null && s.Status.Contains(searchValue)) ||
                    (s.Customer != null && s.Customer.FullName != null && s.Customer.FullName.Contains(searchValue)) ||
                    (s.Product != null && s.Product.ProductName != null && s.Product.ProductName.Contains(searchValue)));
            }
            var list = await query.Select(s => new {
                s.SubscriptionId,
                CustomerName = s.Customer != null ? s.Customer.FullName : "N/A",
                ProductName = s.Product != null ? s.Product.ProductName : "N/A",
                s.OrderId,
                StartDate = s.StartDate.ToString("yyyy-MM-dd"),
                EndDate = s.EndDate.ToString("yyyy-MM-dd"),
                s.LicenseKey,
                s.Status
            }).ToListAsync();

            var bytes = DigiPOSE.Services.CyberExcelExportService.ExportToExcel(list, "Subscriptions", "SaaS Subscriptions Export");
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Subscriptions_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Subscriptions
                .Include(s => s.Customer)
                .Include(s => s.Product)
                .Include(s => s.Order)
                .FirstOrDefaultAsync(s => s.SubscriptionId == id);
            if (item == null) return NotFound();
            return PartialView("_DetailsPartial", item);
        }

        private void PopulateSelectLists(Subscription? model = null)
        {
            ViewBag.Customers = new SelectList(_context.Customers, "CustomerId", "FullName", model?.CustomerId);
            ViewBag.Products = new SelectList(_context.Products, "ProductId", "ProductName", model?.ProductId);
            ViewBag.Orders = new SelectList(_context.Orders, "OrderId", "OrderId", model?.OrderId);
        }

        public IActionResult Create()
        {
            PopulateSelectLists();
            return PartialView("_CreateOrEditPartial", new Subscription { 
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1),
                LicenseKey = $"DIGI-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}-{DateTime.Now:MMdd}",
                Status = "ACTIVE"
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Subscription model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                model.UpdatedAt = DateTime.Now;
                _context.Add(model);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "SaaS Subscription registered successfully." });
            }
            PopulateSelectLists(model);
            return PartialView("_CreateOrEditPartial", model);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Subscriptions.FindAsync(id);
            if (item == null) return NotFound();
            PopulateSelectLists(item);
            return PartialView("_CreateOrEditPartial", item);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Subscription model)
        {
            if (id != model.SubscriptionId) return Json(new { success = false, message = "ID mismatch." });
            
            if (ModelState.IsValid)
            {
                try
                {
                    model.UpdatedAt = DateTime.Now;
                    _context.Update(model);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "SaaS Subscription updated successfully." });
                }
                catch (DbUpdateConcurrencyException) { }
            }
            PopulateSelectLists(model);
            return PartialView("_CreateOrEditPartial", model);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Subscriptions
                .Include(s => s.Customer)
                .Include(s => s.Product)
                .FirstOrDefaultAsync(s => s.SubscriptionId == id);
            if (item == null) return NotFound();
            return PartialView("_DeletePartial", item);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.Subscriptions.FindAsync(id);
            if (item != null) { _context.Subscriptions.Remove(item); await _context.SaveChangesAsync(); }
            return Json(new { success = true, message = "SaaS Subscription deleted successfully." });
        }
    }
}
