using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DigiPOSE.Models;

using System.Linq.Dynamic.Core;

namespace DigiPOSE.Areas.Warehouse.Controllers
{
    [Area("Warehouse")]
    [Authorize(Roles = "Super Admin, Administrator, Tenant Manager, Warehouse")]
    public class StockVoucherDetailsController : Controller
    {
        private readonly DigiPoseDbContext _context;
        public StockVoucherDetailsController(DigiPoseDbContext context) { _context = context; }

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

                var query = _context.StockVoucherDetails.Include(d => d.StockVoucher).Include(d => d.Product).AsQueryable();

                int totalRecords = query.Count();

                // Searching
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(m =>
                        m.VoucherId.ToString().Contains(searchValue) ||
                        (m.Product != null && m.Product.ProductName != null && m.Product.ProductName.Contains(searchValue)));
                }

                int filterRecords = query.Count();

                // Sorting
                if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortColumnDirection))
                {
                    query = query.OrderBy(sortColumn + " " + sortColumnDirection);
                }

                // Paging & Mapping
                var dataList = query.Skip(skip).Take(pageSize).Select(m => new {
                    VoucherDetailId = m.VoucherDetailId,
                    VoucherId = m.VoucherId,
                    ProductName = m.Product != null ? m.Product.ProductName : "",
                    Quantity = m.Quantity,
                    ActualPrice = m.ActualPrice
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
            var query = _context.StockVoucherDetails.Include(d => d.StockVoucher).Include(d => d.Product).AsQueryable();
            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(m =>
                    (m.Product != null && m.Product.ProductName != null && m.Product.ProductName.Contains(searchValue)) ||
                    (m.StockVoucher != null && m.StockVoucher.VoucherType != null && m.StockVoucher.VoucherType.Contains(searchValue)));
            }
            var list = await query.Select(m => new {
                m.VoucherDetailId,
                m.VoucherId,
                ProductName = m.Product != null ? m.Product.ProductName : "",
                m.Quantity,
                m.ActualPrice
            }).ToListAsync();

            var bytes = DigiPOSE.Services.CyberExcelExportService.ExportToExcel(list, "StockVoucherDetails", "Stock Voucher Line Items Export");
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"StockVoucherDetails_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.StockVoucherDetails.Include(d => d.StockVoucher).Include(d => d.Product).FirstOrDefaultAsync(m => m.VoucherDetailId == id);
            if (item == null) return NotFound();
            return PartialView("_DetailsPartial", item);
        }

        public IActionResult Create()
        {
            ViewBag.VoucherId = new SelectList(_context.StockVouchers.OrderByDescending(v => v.CreatedAt), "VoucherId", "VoucherType");
            ViewBag.ProductId = new SelectList(_context.Products.Where(p => p.IsActive), "ProductId", "ProductName");
            return PartialView("_CreateOrEditPartial", new StockVoucherDetail());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StockVoucherDetail model)
        {
            if (ModelState.IsValid) { _context.Add(model); await _context.SaveChangesAsync(); return Json(new { success = true, message = "Line item created." }); }
            ViewBag.VoucherId = new SelectList(_context.StockVouchers, "VoucherId", "VoucherType", model.VoucherId);
            ViewBag.ProductId = new SelectList(_context.Products.Where(p => p.IsActive), "ProductId", "ProductName", model.ProductId);
            return PartialView("_CreateOrEditPartial", model);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.StockVoucherDetails.FindAsync(id);
            if (item == null) return NotFound();
            ViewBag.VoucherId = new SelectList(_context.StockVouchers, "VoucherId", "VoucherType", item.VoucherId);
            ViewBag.ProductId = new SelectList(_context.Products.Where(p => p.IsActive), "ProductId", "ProductName", item.ProductId);
            return PartialView("_CreateOrEditPartial", item);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StockVoucherDetail model)
        {
            if (id != model.VoucherDetailId) return Json(new { success = false, message = "ID mismatch." });
            if (ModelState.IsValid)
            {
                try { _context.Update(model); await _context.SaveChangesAsync(); return Json(new { success = true, message = "Line item updated." }); }
                catch (DbUpdateConcurrencyException) { }
            }
            ViewBag.VoucherId = new SelectList(_context.StockVouchers, "VoucherId", "VoucherType", model.VoucherId);
            ViewBag.ProductId = new SelectList(_context.Products.Where(p => p.IsActive), "ProductId", "ProductName", model.ProductId);
            return PartialView("_CreateOrEditPartial", model);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.StockVoucherDetails.Include(d => d.StockVoucher).Include(d => d.Product).FirstOrDefaultAsync(m => m.VoucherDetailId == id);
            if (item == null) return NotFound();
            return PartialView("_DeletePartial", item);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.StockVoucherDetails.FindAsync(id);
            if (item != null) { _context.StockVoucherDetails.Remove(item); await _context.SaveChangesAsync(); }
            return Json(new { success = true, message = "Line item deleted." });
        }
    }
}
