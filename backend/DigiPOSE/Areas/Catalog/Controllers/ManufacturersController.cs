using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigiPOSE.Models;
using DigiPOSE.Web.Helpers;

using System.Linq.Dynamic.Core;

namespace DigiPOSE.Areas.Catalog.Controllers
{
    [Area("Catalog")]
    [Authorize(Roles = "Super Admin, Administrator, Tenant Manager, Catalog")]
    public class ManufacturersController : Controller
    {
        private readonly DigiPoseDbContext _context;
        public ManufacturersController(DigiPoseDbContext context) { _context = context; }

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

                var query = _context.Manufacturers.AsQueryable();

                int totalRecords = query.Count();

                // Searching
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(m =>
                        (m.ManufacturerName != null && m.ManufacturerName.Contains(searchValue)) ||
                        (m.Slug != null && m.Slug.Contains(searchValue)));
                }

                int filterRecords = query.Count();

                // Sorting
                if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortColumnDirection))
                {
                    query = query.OrderBy(sortColumn + " " + sortColumnDirection);
                }

                // Paging & Mapping
                var dataList = query.Skip(skip).Take(pageSize).Select(m => new {
                    ManufacturerId = m.ManufacturerId,
                    ManufacturerName = m.ManufacturerName,
                    Slug = m.Slug,
                    ImageUrl = m.ImageUrl,
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
            var query = _context.Manufacturers.AsQueryable();
            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(m =>
                    (m.ManufacturerName != null && m.ManufacturerName.Contains(searchValue)) ||
                    (m.Slug != null && m.Slug.Contains(searchValue)));
            }
            var list = await query.Select(m => new {
                m.ManufacturerId,
                m.ManufacturerName,
                m.Slug,
                m.ImageUrl,
                IsActive = m.IsActive
            }).ToListAsync();

            var bytes = DigiPOSE.Services.CyberExcelExportService.ExportToExcel(list, "Manufacturers", "Manufacturer Registry Export");
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Manufacturers_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Manufacturers.FirstOrDefaultAsync(m => m.ManufacturerId == id);
            if (item == null) return NotFound();
            return PartialView("_DetailsPartial", item);
        }

        public IActionResult Create()
            => PartialView("_CreateOrEditPartial", new Manufacturer());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Manufacturer model)
        {
            if (string.IsNullOrWhiteSpace(model.Slug))
                model.Slug = SlugHelper.GenerateSlug(model.ManufacturerName);
            ModelState.Remove("Slug");

            if (!ModelState.IsValid)
                return PartialView("_CreateOrEditPartial", model);

            _context.Add(model);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Manufacturer created successfully." });
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Manufacturers.FindAsync(id);
            if (item == null) return NotFound();
            return PartialView("_CreateOrEditPartial", item);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Manufacturer model)
        {
            if (id != model.ManufacturerId) return Json(new { success = false, message = "ID mismatch." });

            if (string.IsNullOrWhiteSpace(model.Slug))
                model.Slug = SlugHelper.GenerateSlug(model.ManufacturerName);
            ModelState.Remove("Slug");

            if (!ModelState.IsValid)
                return PartialView("_CreateOrEditPartial", model);

            try
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Manufacturer updated successfully." });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Json(new { success = false, message = "Concurrency error." });
            }
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Manufacturers.FirstOrDefaultAsync(m => m.ManufacturerId == id);
            if (item == null) return NotFound();
            return PartialView("_DeletePartial", item);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.Manufacturers.FindAsync(id);
            if (item == null) return Json(new { success = false, message = "Record not found." });
            _context.Manufacturers.Remove(item);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Manufacturer permanently deleted." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var item = await _context.Manufacturers.FindAsync(id);
            if (item == null) return Json(new { success = false });
            item.IsActive = !item.IsActive;
            await _context.SaveChangesAsync();
            return Json(new { success = true, isActive = item.IsActive, message = item.IsActive ? "Activated." : "Deactivated." });
        }
    }
}
