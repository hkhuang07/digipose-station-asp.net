using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigiPOSE.Models;

using System.Linq.Dynamic.Core;

namespace DigiPOSE.Areas.Administrator.Controllers
{
    [Area("Administrator")]
    [Authorize(Roles = "Super Admin, Administrator, Tenant Manager, Accountant, POS Operator")]
    public class CustomersController : Controller
    {
        private readonly DigiPoseDbContext _context;
        public CustomersController(DigiPoseDbContext context) { _context = context; }

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

                var query = _context.Customers.Include(c => c.CustomeType).AsQueryable();

                int totalRecords = query.Count();

                // Searching
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(m =>
                        (m.FullName != null && m.FullName.Contains(searchValue)) ||
                        (m.CompanyName != null && m.CompanyName.Contains(searchValue)) ||
                        (m.PhoneNumber != null && m.PhoneNumber.Contains(searchValue)) ||
                        (m.Email != null && m.Email.Contains(searchValue)) ||
                        (m.CustomeType != null && m.CustomeType.TypeName.Contains(searchValue)));
                }

                int filterRecords = query.Count();

                // Sorting
                if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortColumnDirection))
                {
                    query = query.OrderBy(sortColumn + " " + sortColumnDirection);
                }

                // Paging & Mapping
                var dataList = query.Skip(skip).Take(pageSize).Select(m => new {
                    CustomerId = m.CustomerId,
                    FullName = m.FullName,
                    PhoneNumber = m.PhoneNumber,
                    Email = m.Email,
                    RewardPoints = m.RewardPoints,
                    DebtBalance = m.DebtBalance,
                    CustomeTypeName = m.CustomeType != null ? m.CustomeType.TypeName : "",
                    IsActive = m.IsActive
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
            var query = _context.Customers.Include(c => c.CustomeType).AsQueryable();
            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(m =>
                    (m.FullName != null && m.FullName.Contains(searchValue)) ||
                    (m.CompanyName != null && m.CompanyName.Contains(searchValue)) ||
                    (m.PhoneNumber != null && m.PhoneNumber.Contains(searchValue)) ||
                    (m.Email != null && m.Email.Contains(searchValue)) ||
                    (m.CustomeType != null && m.CustomeType.TypeName.Contains(searchValue)));
            }
            var list = await query.Select(m => new {
                m.CustomerId,
                m.FullName,
                m.PhoneNumber,
                m.Email,
                m.RewardPoints,
                m.DebtBalance,
                CustomeTypeName = m.CustomeType != null ? m.CustomeType.TypeName : "",
                IsActive = m.IsActive
            }).ToListAsync();

            var bytes = DigiPOSE.Services.CyberExcelExportService.ExportToExcel(list, "Customers", "Customer Registry Export");
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Customers_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Customers.Include(c => c.CustomeType).FirstOrDefaultAsync(m => m.CustomerId == id);
            if (item == null) return NotFound();
            return PartialView("_DetailsPartial", item);
        }

        public IActionResult Create()
        {
            ViewBag.CustomeTypeId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.CustomerTypes, "CustomeTypeId", "TypeName");
            return PartialView("_CreateOrEditPartial", new Customer());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.CustomeTypeId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.CustomerTypes, "CustomeTypeId", "TypeName", model.CustomeTypeId);
                return PartialView("_CreateOrEditPartial", model);
            }
            _context.Add(model);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Customer created successfully." });
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Customers.FindAsync(id);
            if (item == null) return NotFound();
            ViewBag.CustomeTypeId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.CustomerTypes, "CustomeTypeId", "TypeName", item.CustomeTypeId);
            return PartialView("_CreateOrEditPartial", item);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Customer model)
        {
            if (id != model.CustomerId) return Json(new { success = false, message = "ID mismatch." });
            if (!ModelState.IsValid)
            {
                ViewBag.CustomeTypeId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.CustomerTypes, "CustomeTypeId", "TypeName", model.CustomeTypeId);
                return PartialView("_CreateOrEditPartial", model);
            }
            try { _context.Update(model); await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return Json(new { success = false, message = "Concurrency error." }); }
            return Json(new { success = true, message = "Customer updated successfully." });
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Customers.Include(c => c.CustomeType).FirstOrDefaultAsync(m => m.CustomerId == id);
            if (item == null) return NotFound();
            return PartialView("_DeletePartial", item);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.Customers.FindAsync(id);
            if (item == null) return Json(new { success = false, message = "Record not found." });
            _context.Customers.Remove(item);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Customer permanently deleted." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var item = await _context.Customers.FindAsync(id);
            if (item == null) return Json(new { success = false });
            item.IsActive = !item.IsActive;
            await _context.SaveChangesAsync();
            return Json(new { success = true, isActive = item.IsActive, message = item.IsActive ? "Activated." : "Deactivated." });
        }
    }
}
