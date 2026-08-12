using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DigiPOSE.Models;

using System.Linq.Dynamic.Core;

namespace DigiPOSE.Areas.Accountant.Controllers
{
    [Area("Accountant")]
    [Authorize(Roles = "Super Admin, Administrator, Tenant Manager, Accountant")]
    public class InvoicesController : Controller
    {
        private readonly DigiPoseDbContext _context;
        public InvoicesController(DigiPoseDbContext context) { _context = context; }

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

                var query = _context.Invoices
                    .Include(x => x.Order)
                    .Include(x => x.InvoiceStatus)
                    .Include(x => x.InvoiceType)
                    .AsQueryable();

                int totalRecords = query.Count();

                // Searching
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(m =>
                        (m.InvoiceNo != null && m.InvoiceNo.Contains(searchValue)) ||
                        (m.Form != null && m.Form.Contains(searchValue)) ||
                        (m.Series != null && m.Series.Contains(searchValue)) ||
                        (m.InvoiceType != null && m.InvoiceType.TypeName != null && m.InvoiceType.TypeName.Contains(searchValue)) ||
                        (m.InvoiceStatus != null && m.InvoiceStatus.StatusName != null && m.InvoiceStatus.StatusName.Contains(searchValue)));
                }

                int filterRecords = query.Count();

                // Sorting
                if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortColumnDirection))
                {
                    query = query.OrderBy(sortColumn + " " + sortColumnDirection);
                }
                else
                {
                    query = query.OrderByDescending(v => v.Date);
                }

                // Paging & Mapping
                var dataList = query.Skip(skip).Take(pageSize).Select(m => new {
                    InvoiceId = m.InvoiceId,
                    InvoiceNo = m.InvoiceNo,
                    Form = m.Form,
                    Series = m.Series,
                    TypeName = m.InvoiceType != null ? m.InvoiceType.TypeName : "",
                    StatusName = m.InvoiceStatus != null ? m.InvoiceStatus.StatusName : "",
                    Date = m.Date.ToString("yyyy-MM-dd")
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
            var query = _context.Invoices
                .Include(x => x.Order)
                .Include(x => x.InvoiceStatus)
                .Include(x => x.InvoiceType)
                .AsQueryable();
            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(m =>
                    (m.InvoiceNo != null && m.InvoiceNo.Contains(searchValue)) ||
                    (m.Form != null && m.Form.Contains(searchValue)) ||
                    (m.Series != null && m.Series.Contains(searchValue)) ||
                    (m.InvoiceType != null && m.InvoiceType.TypeName != null && m.InvoiceType.TypeName.Contains(searchValue)) ||
                    (m.InvoiceStatus != null && m.InvoiceStatus.StatusName != null && m.InvoiceStatus.StatusName.Contains(searchValue)));
            }
            var list = await query.Select(m => new {
                m.InvoiceId,
                m.InvoiceNo,
                m.Form,
                m.Series,
                TypeName = m.InvoiceType != null ? m.InvoiceType.TypeName : "",
                StatusName = m.InvoiceStatus != null ? m.InvoiceStatus.StatusName : "",
                Date = m.Date.ToString("yyyy-MM-dd")
            }).ToListAsync();

            var bytes = DigiPOSE.Services.CyberExcelExportService.ExportToExcel(list, "Invoices", "Financial Invoices Ledger Export");
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Invoices_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Invoices
                .Include(x => x.Order)
                .Include(x => x.InvoiceStatus)
                .Include(x => x.InvoiceType)
                .FirstOrDefaultAsync(m => m.InvoiceId == id);
            if (item == null) return NotFound();
            return PartialView("_DetailsPartial", item);
        }

        public IActionResult Create()
        {
            LoadViewBags();
            return PartialView("_CreateOrEditPartial", new Invoice());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Invoice model)
        {

            if (ModelState.IsValid)
            {
                _context.Add(model);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Created successfully." });
            }
            LoadViewBags(model.OrderId, model.InvoiceStatusId, model.InvoiceTypeId);
            return PartialView("_CreateOrEditPartial", model);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Invoices.FindAsync(id);
            if (item == null) return NotFound();
            LoadViewBags(item.OrderId, item.InvoiceStatusId, item.InvoiceTypeId);
            return PartialView("_CreateOrEditPartial", item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Invoice model)
        {
            if (id != model.InvoiceId) return Json(new { success = false, message = "ID mismatch." });

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(model);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Updated successfully." });
                }
                catch (DbUpdateConcurrencyException) { }
            }
            LoadViewBags(model.OrderId, model.InvoiceStatusId, model.InvoiceTypeId);
            return PartialView("_CreateOrEditPartial", model);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.Invoices.FindAsync(id);
            if (item != null)
            {
                _context.Remove(item);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Deleted successfully." });
            }
            return Json(new { success = false, message = "Not found." });
        }

        private void LoadViewBags(int? val_OrderId = null, int? val_InvoiceStatusId = null, int? val_InvoiceTypeId = null)
        {
            ViewBag.OrderId = new SelectList(_context.Orders, "OrderId", "OrderId", val_OrderId);
            ViewBag.InvoiceStatusId = new SelectList(_context.InvoiceStatuses, "InvoiceStatusId", "InvoiceStatusName", val_InvoiceStatusId);
            ViewBag.InvoiceTypeId = new SelectList(_context.InvoiceTypes, "InvoiceTypeId", "InvoiceTypeName", val_InvoiceTypeId);
        }
    }
}
