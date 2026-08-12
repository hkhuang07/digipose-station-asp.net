using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigiPOSE.Models;
using DigiPOSE.Web.Helpers;

using System.Linq.Dynamic.Core;

namespace DigiPOSE.Areas.Catalog.Controllers
{
    [Area("Catalog")]
    [Authorize(Roles = "Super Admin, Administrator, Tenant Manager, Catalog")]
    public class CategoriesController : Controller
    {
        private readonly DigiPoseDbContext _context;

        public CategoriesController(DigiPoseDbContext context)
        {
            _context = context;
        }

        // GET: Categories
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

                var query = _context.Categories.AsQueryable();

                int totalRecords = query.Count();

                // Searching
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(m =>
                        (m.CategoryName != null && m.CategoryName.Contains(searchValue)) ||
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
                    CategoryId = m.CategoryId,
                    CategoryName = m.CategoryName,
                    Slug = m.Slug,
                    Description = m.Description,
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
            var query = _context.Categories.AsQueryable();
            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(m =>
                    (m.CategoryName != null && m.CategoryName.Contains(searchValue)) ||
                    (m.Slug != null && m.Slug.Contains(searchValue)));
            }
            var list = await query.Select(m => new {
                m.CategoryId,
                m.CategoryName,
                m.Slug,
                m.Description,
                IsActive = m.IsActive
            }).ToListAsync();

            var bytes = DigiPOSE.Services.CyberExcelExportService.ExportToExcel(list, "Categories", "Category Directory Export");
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Categories_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
        }

        // GET: Categories/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var category = await _context.Categories.FirstOrDefaultAsync(m => m.CategoryId == id);
            if (category == null) return NotFound();

            return PartialView("_DetailsPartial", category);
        }

        // GET: Categories/Create
        public IActionResult Create()
        {
            return PartialView("_CreatePartial", new Category());
        }

        // POST: Categories/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CategoryId,CategoryName,Slug,Description")] Category category)
        {
            if (string.IsNullOrWhiteSpace(category.Slug) && !string.IsNullOrWhiteSpace(category.CategoryName))
            {
                category.Slug = SlugHelper.GenerateSlug(category.CategoryName);
            }

            ModelState.Remove("Slug");

            if (!ModelState.IsValid)
            {
                // Return Partial View HTML with inline validation errors
                return PartialView("_CreatePartial", category);
            }

            category.Description ??= string.Empty;
            category.Slug ??= string.Empty;

            _context.Add(category);
            await _context.SaveChangesAsync();

            return Json(
                new { 
                        success = true, 
                        message = "Category created successfully." 
                    }
                );
        }

        // GET: Categories/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            return PartialView("_EditPartial", category);
        }

        // POST: Categories/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("CategoryId,CategoryName,Slug,Description")] Category category)
        {
            if (id != category.CategoryId)
            {
                return Json(new { success = false, message = "Category ID mismatch." });
            }

            if (string.IsNullOrWhiteSpace(category.Slug) && !string.IsNullOrWhiteSpace(category.CategoryName))
            {
                category.Slug = SlugHelper.GenerateSlug(category.CategoryName);
            }

            ModelState.Remove("Slug");

            if (!ModelState.IsValid)
            {
                // Return Partial View HTML with inline validation errors
                return PartialView("_EditPartial", category);
            }

            category.Description ??= string.Empty;
            category.Slug ??= string.Empty;

            try
            {
                _context.Update(category);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CategoryExists(category.CategoryId))
                {
                    return Json(new { success = false, message = "Category record no longer exists." });
                }
                throw;
            }

            return Json(new { success = true, message = "Category updated successfully." });
        }

        // GET: Categories/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var category = await _context.Categories.FirstOrDefaultAsync(m => m.CategoryId == id);
            if (category == null) return NotFound();

            return PartialView("_DeletePartial", category);
        }

        // POST: Categories/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category != null)
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true, message = "Category deleted successfully." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var item = await _context.Categories.FindAsync(id);
            if (item == null) return Json(new { success = false });
            item.IsActive = !item.IsActive;
            await _context.SaveChangesAsync();
            return Json(new { success = true, isActive = item.IsActive, message = item.IsActive ? "Activated." : "Deactivated." });
        }

        private bool CategoryExists(int id)
        {
            return _context.Categories.Any(e => e.CategoryId == id);
        }
    }

}
