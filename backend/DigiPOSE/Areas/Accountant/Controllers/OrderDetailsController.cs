using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DigiPOSE.Models;
using System.Linq.Dynamic.Core;

namespace DigiPOSE.Areas.Accountant.Controllers
{
    [Area("Accountant")]
    [Authorize(Roles = "Super Admin, Administrator, Tenant Manager, Accountant")]
    public class OrderDetailsController : Controller
    {
        private readonly DigiPoseDbContext _context;

        public OrderDetailsController(DigiPoseDbContext context)
        {
            _context = context;
        }

        private async Task PopulateSelectListsAsync()
        {
            ViewBag.OrderId = new SelectList(await _context.Orders.ToListAsync(), "OrderId", "OrderId");
            ViewBag.ProductId = new SelectList(await _context.Products.Where(p => p.IsActive).ToListAsync(), "ProductId", "ProductName");
            ViewBag.NatureId = new SelectList(await _context.ItemNatures.ToListAsync(), "NatureId", "NatureName");
            ViewBag.TaxTypeId = new SelectList(await _context.TaxTypes.ToListAsync(), "TaxTypeId", "TaxName");
        }

        // GET: OrderDetails
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

                var query = _context.OrderDetails
                    .Include(od => od.Order)
                    .Include(od => od.Product)
                    .Include(od => od.ItemNature)
                    .Include(od => od.TaxType)
                    .AsQueryable();

                int totalRecords = query.Count();

                // Searching
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(m =>
                        m.OrderId.ToString().Contains(searchValue) ||
                        (m.ProductName != null && m.ProductName.Contains(searchValue)));
                }

                int filterRecords = query.Count();

                // Sorting
                if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortColumnDirection))
                {
                    query = query.OrderBy(sortColumn + " " + sortColumnDirection);
                }

                // Paging & Mapping
                var dataList = query.Skip(skip).Take(pageSize).Select(m => new {
                    OrderDetailId = m.OrderDetailId,
                    OrderId = m.OrderId,
                    ProductName = m.ProductName,
                    Quantity = m.Quantity,
                    UnitPrice = m.UnitPrice,
                    TotalAmount = m.TotalAmount,
                    IsFree = m.IsFree
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
            var query = _context.OrderDetails
                .Include(od => od.Product)
                .Include(od => od.ItemNature)
                .Include(od => od.TaxType)
                .AsQueryable();
            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(m =>
                    (m.ProductName != null && m.ProductName.Contains(searchValue)) ||
                    (m.Product != null && m.Product.ProductName != null && m.Product.ProductName.Contains(searchValue)));
            }
            var list = await query.Select(m => new {
                m.OrderDetailId,
                m.OrderId,
                ProductName = m.Product != null ? m.Product.ProductName : m.ProductName,
                m.Quantity,
                m.UnitPrice,
                m.DiscountAmount,
                m.TaxAmount,
                m.TotalAmount
            }).ToListAsync();

            var bytes = DigiPOSE.Services.CyberExcelExportService.ExportToExcel(list, "OrderDetails", "Order Line Items Export");
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"OrderDetails_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
        }

        // GET: OrderDetails/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var detail = await _context.OrderDetails
                .Include(od => od.Order)
                .Include(od => od.Product)
                .Include(od => od.ItemNature)
                .Include(od => od.TaxType)
                .FirstOrDefaultAsync(m => m.OrderDetailId == id);
            if (detail == null) return NotFound();

            return PartialView("_DetailsPartial", detail);
        }

        public async Task<IActionResult> Create()
        {
            await PopulateSelectListsAsync();
            return PartialView("_CreateOrEditPartial", new OrderDetail());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OrderDetail model)
        {
            if (ModelState.IsValid)
            {
                model.TotalAmount = (model.Quantity * model.UnitPrice) - model.DiscountAmount + model.TaxAmount;
                _context.Add(model);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Created successfully." });
            }
            await PopulateSelectListsAsync();
            return PartialView("_CreateOrEditPartial", model);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.OrderDetails.FindAsync(id);
            if (item == null) return NotFound();
            await PopulateSelectListsAsync();
            return PartialView("_CreateOrEditPartial", item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, OrderDetail model)
        {
            if (id != model.OrderDetailId) return Json(new { success = false, message = "ID mismatch." });

            if (ModelState.IsValid)
            {
                try
                {
                    model.TotalAmount = (model.Quantity * model.UnitPrice) - model.DiscountAmount + model.TaxAmount;
                    _context.Update(model);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Updated successfully." });
                }
                catch (DbUpdateConcurrencyException) { }
            }
            await PopulateSelectListsAsync();
            return PartialView("_CreateOrEditPartial", model);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.OrderDetails.FirstOrDefaultAsync(m => m.OrderDetailId == id);
            if (item == null) return NotFound();
            return PartialView("_DeletePartial", item);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.OrderDetails.FindAsync(id);
            if (item != null)
            {
                _context.OrderDetails.Remove(item);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Deleted successfully." });
            }
            return Json(new { success = false, message = "Not found." });
        }

        private bool OrderDetailExists(int id)
        {
            return _context.OrderDetails.Any(e => e.OrderDetailId == id);
        }
    }
}
