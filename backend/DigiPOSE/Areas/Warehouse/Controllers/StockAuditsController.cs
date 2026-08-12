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
    public class StockAuditsController : Controller
    {
        private readonly DigiPoseDbContext _context;
        public StockAuditsController(DigiPoseDbContext context) { _context = context; }

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

                var query = _context.StockAudits
                    .Include(a => a.Tenant)
                    .Include(a => a.AuditorUser)
                    .Include(a => a.ApproverUser)
                    .AsQueryable();

                int totalRecords = query.Count();

                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(a =>
                        (a.AuditCode != null && a.AuditCode.Contains(searchValue)) ||
                        (a.Tenant != null && a.Tenant.TenantName != null && a.Tenant.TenantName.Contains(searchValue)) ||
                        (a.Notes != null && a.Notes.Contains(searchValue)));
                }

                int filterRecords = query.Count();

                if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortColumnDirection))
                {
                    query = query.OrderBy(sortColumn + " " + sortColumnDirection);
                }
                else
                {
                    query = query.OrderByDescending(a => a.AuditId);
                }

                var dataList = query.Skip(skip).Take(pageSize).Select(a => new {
                    auditId = a.AuditId,
                    auditCode = a.AuditCode,
                    tenantName = a.Tenant != null ? a.Tenant.TenantName : "N/A",
                    auditDate = a.AuditDate.ToString("yyyy-MM-dd"),
                    status = a.Status.ToString(),
                    auditorName = a.AuditorUser != null ? a.AuditorUser.UserName : "N/A",
                    notes = a.Notes
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
            var query = _context.StockAudits.Include(a => a.Tenant).Include(a => a.AuditorUser).AsQueryable();
            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(a =>
                    (a.AuditCode != null && a.AuditCode.Contains(searchValue)) ||
                    (a.Tenant != null && a.Tenant.TenantName != null && a.Tenant.TenantName.Contains(searchValue)));
            }
            var list = await query.Select(a => new {
                a.AuditId,
                a.AuditCode,
                TenantName = a.Tenant != null ? a.Tenant.TenantName : "N/A",
                AuditDate = a.AuditDate.ToString("yyyy-MM-dd"),
                Status = a.Status.ToString(),
                AuditorName = a.AuditorUser != null ? a.AuditorUser.UserName : "N/A",
                a.Notes
            }).ToListAsync();

            var bytes = DigiPOSE.Services.CyberExcelExportService.ExportToExcel(list, "StockAudits", "Stock Audits Export");
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"StockAudits_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.StockAudits
                .Include(a => a.Tenant)
                .Include(a => a.AuditorUser)
                .Include(a => a.ApproverUser)
                .FirstOrDefaultAsync(a => a.AuditId == id);
            if (item == null) return NotFound();
            return PartialView("_DetailsPartial", item);
        }

        private void PopulateSelectLists(StockAudit? model = null)
        {
            ViewBag.Tenants = new SelectList(_context.Tenants, "TenantId", "TenantName", model?.TenantId);
            ViewBag.Users = new SelectList(_context.Users, "UserId", "UserName", model?.AuditorUserId);
        }

        public IActionResult Create()
        {
            PopulateSelectLists();
            return PartialView("_CreateOrEditPartial", new StockAudit { AuditCode = $"AUD-{DateTime.Now:yyyyMMdd-HHmmss}" });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StockAudit model)
        {
            if (ModelState.IsValid)
            {
                _context.Add(model);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Stock Audit created successfully." });
            }
            PopulateSelectLists(model);
            return PartialView("_CreateOrEditPartial", model);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.StockAudits.FindAsync(id);
            if (item == null) return NotFound();
            PopulateSelectLists(item);
            return PartialView("_CreateOrEditPartial", item);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StockAudit model)
        {
            if (id != model.AuditId) return Json(new { success = false, message = "ID mismatch." });
            
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(model);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Stock Audit updated successfully." });
                }
                catch (DbUpdateConcurrencyException) { }
            }
            PopulateSelectLists(model);
            return PartialView("_CreateOrEditPartial", model);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.StockAudits
                .Include(a => a.Tenant)
                .Include(a => a.AuditorUser)
                .FirstOrDefaultAsync(a => a.AuditId == id);
            if (item == null) return NotFound();
            return PartialView("_DeletePartial", item);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.StockAudits.FindAsync(id);
            if (item != null) { _context.StockAudits.Remove(item); await _context.SaveChangesAsync(); }
            return Json(new { success = true, message = "Stock Audit deleted successfully." });
        }
    }
}
