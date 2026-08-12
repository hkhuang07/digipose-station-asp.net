using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DigiPOSE.Models;

using System.Linq.Dynamic.Core;

namespace DigiPOSE.Areas.Accountant.Controllers
{
    [Area("Accountant")]
    [Authorize(Roles = "Super Admin, Administrator, Tenant Manager, Accountant")]
    public class InvoiceStatusesController : Controller
    {
        private readonly DigiPoseDbContext _context;
        public InvoiceStatusesController(DigiPoseDbContext context) { _context = context; }

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

                var query = _context.InvoiceStatuses.AsQueryable();

                int totalRecords = query.Count();

                // Searching
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(m =>
                        (m.StatusName != null && m.StatusName.Contains(searchValue)) ||
                        (m.Description != null && m.Description.Contains(searchValue)));
                }

                int filterRecords = query.Count();

                // Sorting
                if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortColumnDirection))
                {
                    query = query.OrderBy(sortColumn + " " + sortColumnDirection);
                }

                // Paging & Mapping
                var dataList = query.Skip(skip).Take(pageSize).Select(m => new {
                    InvoiceStatusId = m.InvoiceStatusId,
                    StatusName = m.StatusName,
                    Description = m.Description
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
            var query = _context.InvoiceStatuses.AsQueryable();
            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(m =>
                    (m.StatusName != null && m.StatusName.Contains(searchValue)) ||
                    (m.Description != null && m.Description.Contains(searchValue)));
            }
            var list = await query.Select(m => new {
                m.InvoiceStatusId,
                m.StatusName,
                m.Description
            }).ToListAsync();

            var bytes = DigiPOSE.Services.CyberExcelExportService.ExportToExcel(list, "InvoiceStatuses", "Invoice Statuses Export");
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"InvoiceStatuses_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.InvoiceStatuses.FirstOrDefaultAsync(m => m.InvoiceStatusId == id);
            if (item == null) return NotFound();
            return PartialView("_DetailsPartial", item);
        }

        public IActionResult Create()
        {

            return PartialView("_CreateOrEditPartial", new InvoiceStatus());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(InvoiceStatus model)
        {
            if (ModelState.IsValid)
            {
                _context.Add(model);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "InvoiceStatus created successfully." });
            }

            return PartialView("_CreateOrEditPartial", model);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.InvoiceStatuses.FindAsync(id);
            if (item == null) return NotFound();

            return PartialView("_CreateOrEditPartial", item);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, InvoiceStatus model)
        {
            if (id != model.InvoiceStatusId) return Json(new { success = false, message = "ID mismatch." });
            
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(model);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "InvoiceStatus updated successfully." });
                }
                catch (DbUpdateConcurrencyException) { }
            }

            return PartialView("_CreateOrEditPartial", model);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.InvoiceStatuses.FirstOrDefaultAsync(m => m.InvoiceStatusId == id);
            if (item == null) return NotFound();
            return PartialView("_DeletePartial", item);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.InvoiceStatuses.FindAsync(id);
            if (item != null) { _context.InvoiceStatuses.Remove(item); await _context.SaveChangesAsync(); }
            return Json(new { success = true, message = "InvoiceStatus deleted successfully." });
        }
    }
}
