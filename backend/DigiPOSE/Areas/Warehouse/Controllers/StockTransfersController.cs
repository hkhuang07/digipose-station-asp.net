using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using DigiPOSE.Models;
using System.Linq.Dynamic.Core;

namespace DigiPOSE.Areas.Warehouse.Controllers
{
    [Area("Warehouse")]
    [Authorize(Roles = "Super Admin, Administrator, Tenant Manager, Warehouse")]
    public class StockTransfersController : Controller
    {
        private readonly DigiPoseDbContext _context;
        public StockTransfersController(DigiPoseDbContext context) { _context = context; }

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

                var query = _context.StockTransfers
                    .Include(t => t.SourceTenant)
                    .Include(t => t.DestinationTenant)
                    .Include(t => t.InitiatorUser)
                    .Include(t => t.ApproverUser)
                    .AsQueryable();

                int totalRecords = query.Count();

                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(t =>
                        (t.TransferCode != null && t.TransferCode.Contains(searchValue)) ||
                        (t.SourceTenant != null && t.SourceTenant.TenantName != null && t.SourceTenant.TenantName.Contains(searchValue)) ||
                        (t.DestinationTenant != null && t.DestinationTenant.TenantName != null && t.DestinationTenant.TenantName.Contains(searchValue)) ||
                        (t.Notes != null && t.Notes.Contains(searchValue)));
                }

                int filterRecords = query.Count();

                if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortColumnDirection))
                {
                    query = query.OrderBy(sortColumn + " " + sortColumnDirection);
                }
                else
                {
                    query = query.OrderByDescending(t => t.TransferId);
                }

                var dataList = query.Skip(skip).Take(pageSize).Select(t => new {
                    transferId = t.TransferId,
                    transferCode = t.TransferCode,
                    sourceTenant = t.SourceTenant != null ? t.SourceTenant.TenantName : "N/A",
                    destinationTenant = t.DestinationTenant != null ? t.DestinationTenant.TenantName : "N/A",
                    status = t.Status.ToString(),
                    initiatorName = t.InitiatorUser != null ? t.InitiatorUser.UserName : "N/A",
                    createdAt = t.CreatedAt.ToString("yyyy-MM-dd HH:mm")
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
            var query = _context.StockTransfers.Include(t => t.SourceTenant).Include(t => t.DestinationTenant).Include(t => t.InitiatorUser).AsQueryable();
            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(t =>
                    (t.TransferCode != null && t.TransferCode.Contains(searchValue)) ||
                    (t.SourceTenant != null && t.SourceTenant.TenantName != null && t.SourceTenant.TenantName.Contains(searchValue)) ||
                    (t.DestinationTenant != null && t.DestinationTenant.TenantName != null && t.DestinationTenant.TenantName.Contains(searchValue)));
            }
            var list = await query.Select(t => new {
                t.TransferId,
                t.TransferCode,
                SourceTenant = t.SourceTenant != null ? t.SourceTenant.TenantName : "N/A",
                DestinationTenant = t.DestinationTenant != null ? t.DestinationTenant.TenantName : "N/A",
                Status = t.Status.ToString(),
                Initiator = t.InitiatorUser != null ? t.InitiatorUser.UserName : "N/A",
                CreatedAt = t.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                t.Notes
            }).ToListAsync();

            var bytes = DigiPOSE.Services.CyberExcelExportService.ExportToExcel(list, "StockTransfers", "Stock Transfers Export");
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"StockTransfers_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.StockTransfers
                .Include(t => t.SourceTenant)
                .Include(t => t.DestinationTenant)
                .Include(t => t.InitiatorUser)
                .Include(t => t.ApproverUser)
                .FirstOrDefaultAsync(t => t.TransferId == id);
            if (item == null) return NotFound();
            return PartialView("_DetailsPartial", item);
        }

        private void PopulateSelectLists(StockTransfer? model = null)
        {
            ViewBag.SourceTenants = new SelectList(_context.Tenants, "TenantId", "TenantName", model?.SourceTenantId);
            ViewBag.DestinationTenants = new SelectList(_context.Tenants, "TenantId", "TenantName", model?.DestinationTenantId);
            ViewBag.Users = new SelectList(_context.Users, "UserId", "UserName", model?.InitiatorUserId);
        }

        public IActionResult Create()
        {
            PopulateSelectLists();
            return PartialView("_CreateOrEditPartial", new StockTransfer { TransferCode = $"TRF-{DateTime.Now:yyyyMMdd-HHmmss}" });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StockTransfer model)
        {
            if (ModelState.IsValid)
            {
                if (model.SourceTenantId == model.DestinationTenantId)
                {
                    ModelState.AddModelError("DestinationTenantId", "Destination tenant cannot be identical to Source tenant.");
                    PopulateSelectLists(model);
                    return PartialView("_CreateOrEditPartial", model);
                }
                _context.Add(model);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Stock Transfer initiated successfully." });
            }
            PopulateSelectLists(model);
            return PartialView("_CreateOrEditPartial", model);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.StockTransfers.FindAsync(id);
            if (item == null) return NotFound();
            PopulateSelectLists(item);
            return PartialView("_CreateOrEditPartial", item);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StockTransfer model)
        {
            if (id != model.TransferId) return Json(new { success = false, message = "ID mismatch." });
            
            if (ModelState.IsValid)
            {
                if (model.SourceTenantId == model.DestinationTenantId)
                {
                    ModelState.AddModelError("DestinationTenantId", "Destination tenant cannot be identical to Source tenant.");
                    PopulateSelectLists(model);
                    return PartialView("_CreateOrEditPartial", model);
                }
                try
                {
                    _context.Update(model);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Stock Transfer updated successfully." });
                }
                catch (DbUpdateConcurrencyException) { }
            }
            PopulateSelectLists(model);
            return PartialView("_CreateOrEditPartial", model);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.StockTransfers
                .Include(t => t.SourceTenant)
                .Include(t => t.DestinationTenant)
                .Include(t => t.InitiatorUser)
                .FirstOrDefaultAsync(t => t.TransferId == id);
            if (item == null) return NotFound();
            return PartialView("_DeletePartial", item);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.StockTransfers.FindAsync(id);
            if (item != null) { _context.StockTransfers.Remove(item); await _context.SaveChangesAsync(); }
            return Json(new { success = true, message = "Stock Transfer deleted successfully." });
        }
    }
}
