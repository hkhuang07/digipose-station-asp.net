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
    public class ProductInventoriesController : Controller
    {
        private readonly DigiPoseDbContext _context;
        private readonly IInventoryRAMService _ramService;
        private readonly IInventoryLedgerService _ledgerService;

        public ProductInventoriesController(DigiPoseDbContext context, IInventoryRAMService ramService, IInventoryLedgerService ledgerService)
        {
            _context = context;
            _ramService = ramService;
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

                var query = _context.ProductInventories.Include(p => p.Tenant).Include(p => p.Product).AsQueryable();

                int totalRecords = query.Count();

                // Searching
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(m =>
                        (m.Product != null && m.Product.ProductName != null && m.Product.ProductName.Contains(searchValue)) ||
                        (m.Tenant != null && m.Tenant.TenantName != null && m.Tenant.TenantName.Contains(searchValue)));
                }

                int filterRecords = query.Count();

                // Sorting
                if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortColumnDirection))
                {
                    query = query.OrderBy(sortColumn + " " + sortColumnDirection);
                }

                // Paging & Mapping
                var dataList = query.Skip(skip).Take(pageSize).Select(m => new {
                    InventoryId = m.InventoryId,
                    ProductName = m.Product != null ? m.Product.ProductName : "",
                    TenantName = m.Tenant != null ? m.Tenant.TenantName : "",
                    StockQuantity = m.StockQuantity,
                    MinStockLevel = m.MinStockLevel
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
            var query = _context.ProductInventories.Include(p => p.Tenant).Include(p => p.Product).AsQueryable();
            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(m =>
                    (m.Product != null && m.Product.ProductName != null && m.Product.ProductName.Contains(searchValue)) ||
                    (m.Tenant != null && m.Tenant.TenantName != null && m.Tenant.TenantName.Contains(searchValue)));
            }
            var list = await query.Select(m => new {
                m.InventoryId,
                ProductName = m.Product != null ? m.Product.ProductName : "",
                TenantName = m.Tenant != null ? m.Tenant.TenantName : "",
                m.StockQuantity,
                m.MinStockLevel
            }).ToListAsync();

            var bytes = DigiPOSE.Services.CyberExcelExportService.ExportToExcel(list, "ProductInventories", "Product Inventory Levels Export");
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ProductInventories_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.ProductInventories.Include(p => p.Tenant).Include(p => p.Product).FirstOrDefaultAsync(m => m.InventoryId == id);
            if (item == null) return NotFound();
            return PartialView("_DetailsPartial", item);
        }

        public IActionResult Create()
        {
            ViewBag.TenantId = new SelectList(_context.Tenants.Where(b => b.IsActive), "TenantId", "TenantName");
            ViewBag.ProductId = new SelectList(_context.Products.Where(p => p.IsActive), "ProductId", "ProductName");
            return PartialView("_CreateOrEditPartial", new ProductInventory());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductInventory model)
        {
            if (ModelState.IsValid)
            {
                int? userId = null;
                if (int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out int uid)) userId = uid;

                var res = await _ledgerService.RecordTransactionAsync(
                    model.TenantId,
                    model.ProductId,
                    model.StockQuantity,
                    InventoryTxType.Restock,
                    0,
                    "INIT-OPENING-STOCK",
                    userId,
                    0,
                    "Initial opening stock setup via Administration Console");

                if (res.Success)
                    return Json(new { success = true, message = "Inventory record created and initial stock balance established." });
                
                return Json(new { success = false, message = res.Message });
            }
            ViewBag.TenantId = new SelectList(_context.Tenants.Where(b => b.IsActive), "TenantId", "TenantName", model.TenantId);
            ViewBag.ProductId = new SelectList(_context.Products.Where(p => p.IsActive), "ProductId", "ProductName", model.ProductId);
            return PartialView("_CreateOrEditPartial", model);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.ProductInventories.FindAsync(id);
            if (item == null) return NotFound();
            ViewBag.TenantId = new SelectList(_context.Tenants.Where(b => b.IsActive), "TenantId", "TenantName", item.TenantId);
            ViewBag.ProductId = new SelectList(_context.Products.Where(p => p.IsActive), "ProductId", "ProductName", item.ProductId);
            return PartialView("_CreateOrEditPartial", item);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductInventory model, string? mandatoryReason)
        {
            if (id != model.InventoryId) return Json(new { success = false, message = "ID mismatch." });
            if (ModelState.IsValid)
            {
                var currentInv = await _context.ProductInventories.AsNoTracking().FirstOrDefaultAsync(e => e.InventoryId == id);
                if (currentInv == null) return Json(new { success = false, message = "Record no longer exists." });

                int? userId = null;
                if (int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out int uid)) userId = uid;

                // If StockQuantity is being altered directly, enforce Emergency Override Protocol ("Bất Khả Khang")
                if (currentInv.StockQuantity != model.StockQuantity)
                {
                    if (!User.IsInRole("Super Admin") && !User.IsInRole("Chief Accountant") && !User.IsInRole("Administrator"))
                    {
                        return Json(new { success = false, message = ">>> [ACCESS_DENIED]: Direct inventory balance adjustment requires Super Admin or Chief Accountant authorization." });
                    }

                    var overrideRes = await _ledgerService.ExecuteEmergencyOverrideAsync(
                        id,
                        model.StockQuantity,
                        model.MinStockLevel,
                        userId ?? 0,
                        mandatoryReason ?? "");

                    if (!overrideRes.Success)
                    {
                        return Json(new { success = false, message = overrideRes.Message });
                    }

                    return Json(new { success = true, message = overrideRes.Message });
                }

                try 
                { 
                    currentInv.MinStockLevel = model.MinStockLevel;
                    _context.Update(currentInv); 
                    await _context.SaveChangesAsync(); 
                    return Json(new { success = true, message = "Min Stock Level updated successfully." }); 
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.ProductInventories.Any(e => e.InventoryId == id))
                        return Json(new { success = false, message = "Record no longer exists." });
                    return Json(new { success = false, message = "Concurrency conflict. Please reload and try again." });
                }
            }
            ViewBag.TenantId = new SelectList(_context.Tenants.Where(b => b.IsActive), "TenantId", "TenantName", model.TenantId);
            ViewBag.ProductId = new SelectList(_context.Products.Where(p => p.IsActive), "ProductId", "ProductName", model.ProductId);
            return PartialView("_CreateOrEditPartial", model);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.ProductInventories.Include(p => p.Tenant).Include(p => p.Product).FirstOrDefaultAsync(m => m.InventoryId == id);
            if (item == null) return NotFound();
            return PartialView("_DeletePartial", item);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.ProductInventories.FindAsync(id);
            if (item != null)
            {
                _context.ProductInventories.Remove(item);
                await _context.SaveChangesAsync();
                // >>> [O(1) CACHE PURGE]: Reset live RAM stock balance to 0 for deleted record
                _ramService.InitializeOrUpdateStock(item.TenantId, item.ProductId, 0);
            }
            return Json(new { success = true, message = "Inventory record deleted." });
        }
    }
}
