using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DigiPOSE.Models;

using DigiPOSE.Services;
using System.Linq.Dynamic.Core;

namespace DigiPOSE.Areas.Warehouse.Controllers
{
    [Area("Warehouse")]
    [Authorize(Roles = "Super Admin, Administrator, Tenant Manager, Warehouse")]
    public class StockVouchersController : Controller
    {
        private readonly DigiPoseDbContext _context;
        private readonly IInventoryLedgerService _ledgerService;

        public StockVouchersController(DigiPoseDbContext context, IInventoryLedgerService ledgerService) 
        { 
            _context = context; 
            _ledgerService = ledgerService;
        }

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

                var query = _context.StockVouchers
                    .Include(v => v.Tenant).Include(v => v.User).Include(v => v.Supplier)
                    .AsQueryable();

                int totalRecords = query.Count();

                // Searching
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(m =>
                        (m.VoucherType != null && m.VoucherType.Contains(searchValue)) ||
                        (m.Tenant != null && m.Tenant.TenantName != null && m.Tenant.TenantName.Contains(searchValue)) ||
                        (m.User != null && m.User.UserName != null && m.User.UserName.Contains(searchValue)) ||
                        (m.Supplier != null && m.Supplier.SupplierName != null && m.Supplier.SupplierName.Contains(searchValue)));
                }

                int filterRecords = query.Count();

                // Sorting
                if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortColumnDirection))
                {
                    query = query.OrderBy(sortColumn + " " + sortColumnDirection);
                }
                else
                {
                    query = query.OrderByDescending(v => v.CreatedAt);
                }

                // Paging & Mapping
                var dataList = query.Skip(skip).Take(pageSize).Select(m => new {
                    VoucherId = m.VoucherId,
                    VoucherType = m.VoucherType,
                    TenantName = m.Tenant != null ? m.Tenant.TenantName : "",
                    UserName = m.User != null ? m.User.UserName : "",
                    SupplierName = m.Supplier != null ? m.Supplier.SupplierName : "---",
                    TotalValue = m.TotalValue,
                    CreatedAt = m.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
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
            var query = _context.StockVouchers
                .Include(v => v.Tenant)
                .Include(v => v.User)
                .Include(v => v.Supplier)
                .AsQueryable();
            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(m =>
                    (m.VoucherType != null && m.VoucherType.Contains(searchValue)) ||
                    (m.Tenant != null && m.Tenant.TenantName != null && m.Tenant.TenantName.Contains(searchValue)) ||
                    (m.Supplier != null && m.Supplier.SupplierName != null && m.Supplier.SupplierName.Contains(searchValue)));
            }
            var list = await query.Select(m => new {
                m.VoucherId,
                m.VoucherType,
                TenantName = m.Tenant != null ? m.Tenant.TenantName : "",
                UserName = m.User != null ? m.User.UserName : "",
                SupplierName = m.Supplier != null ? m.Supplier.SupplierName : "---",
                m.TotalValue,
                CreatedAt = m.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            }).ToListAsync();

            var bytes = DigiPOSE.Services.CyberExcelExportService.ExportToExcel(list, "StockVouchers", "Stock Vouchers Ledger Export");
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"StockVouchers_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.StockVouchers.Include(v => v.Tenant).Include(v => v.User).Include(v => v.Supplier)
                .FirstOrDefaultAsync(m => m.VoucherId == id);
            if (item == null) return NotFound();
            return PartialView("_DetailsPartial", item);
        }

        public IActionResult Create() { LoadViewBags(); return PartialView("_CreateOrEditPartial", new StockVoucher { CreatedAt = DateTime.Now }); }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StockVoucher model)
        {
            if (ModelState.IsValid) 
            { 
                if (string.IsNullOrWhiteSpace(model.VoucherCode))
                {
                    model.VoucherCode = $"POV-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";
                }
                model.Status = VoucherStatus.Draft;
                _context.Add(model); 
                await _context.SaveChangesAsync(); 
                return Json(new { success = true, message = "Stock voucher created in Draft status." }); 
            }
            LoadViewBags(model.TenantId, model.UserId, model.SupplierId);
            return PartialView("_CreateOrEditPartial", model);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.StockVouchers.FindAsync(id);
            if (item == null) return NotFound();
            if (item.Status == VoucherStatus.Posted)
            {
                return BadRequest(">>> [IMMUTABLE_AUDIT_ERROR]: Posted inventory vouchers cannot be edited.");
            }
            LoadViewBags(item.TenantId, item.UserId, item.SupplierId);
            return PartialView("_CreateOrEditPartial", item);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StockVoucher model)
        {
            if (id != model.VoucherId) return Json(new { success = false, message = "ID mismatch." });
            var existing = await _context.StockVouchers.AsNoTracking().FirstOrDefaultAsync(v => v.VoucherId == id);
            if (existing != null && existing.Status == VoucherStatus.Posted)
            {
                return Json(new { success = false, message = ">>> [IMMUTABLE_AUDIT_ERROR]: Cannot edit an already posted stock voucher." });
            }
            if (ModelState.IsValid)
            {
                try { _context.Update(model); await _context.SaveChangesAsync(); return Json(new { success = true, message = "Stock voucher updated." }); }
                catch (DbUpdateConcurrencyException) { }
            }
            LoadViewBags(model.TenantId, model.UserId, model.SupplierId);
            return PartialView("_CreateOrEditPartial", model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> PostVoucher(int id)
        {
            int? userId = null;
            if (int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out int uid)) userId = uid;

            var res = await _ledgerService.PostVoucherAsync(id, userId ?? 0);
            return Json(new { success = res.Success, message = res.Message });
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.StockVouchers.Include(v => v.Tenant).Include(v => v.User).Include(v => v.Supplier)
                .FirstOrDefaultAsync(m => m.VoucherId == id);
            if (item == null) return NotFound();
            return PartialView("_DeletePartial", item);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.StockVouchers.FindAsync(id);
            if (item != null) 
            { 
                if (item.Status == VoucherStatus.Posted)
                {
                    return Json(new { success = false, message = ">>> [IMMUTABLE_AUDIT_ERROR]: Cannot delete an already posted inventory voucher." });
                }
                _context.StockVouchers.Remove(item); 
                await _context.SaveChangesAsync(); 
            }
            return Json(new { success = true, message = "Stock voucher deleted." });
        }

        private void LoadViewBags(int? tenantId = null, int? userId = null, int? supplierId = null)
        {
            ViewBag.TenantId = new SelectList(_context.Tenants.Where(b => b.IsActive), "TenantId", "TenantName", tenantId);
            ViewBag.UserId = new SelectList(_context.Users.Where(u => u.IsActive), "UserId", "UserName", userId);
            ViewBag.SupplierId = new SelectList(_context.Suppliers.Where(s => s.IsActive), "SupplierId", "SupplierName", supplierId);
        }
    }
}
