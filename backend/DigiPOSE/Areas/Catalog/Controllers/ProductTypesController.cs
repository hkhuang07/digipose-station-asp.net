using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigiPOSE.Models;

using System.Linq.Dynamic.Core;

namespace DigiPOSE.Areas.Catalog.Controllers
{
    [Area("Catalog")]
    [Authorize(Roles = "Super Admin, Administrator, Tenant Manager, Catalog")]
    public class ProductTypesController : Controller
    {
        private readonly DigiPoseDbContext _context;
        public ProductTypesController(DigiPoseDbContext context) { _context = context; }

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

                var query = _context.ProductTypes.AsQueryable();

                int totalRecords = query.Count();

                // Searching
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(m => m.TypeName != null && m.TypeName.Contains(searchValue));
                }

                int filterRecords = query.Count();

                // Sorting
                if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortColumnDirection))
                {
                    query = query.OrderBy(sortColumn + " " + sortColumnDirection);
                }

                // Paging & Mapping
                var dataList = query.Skip(skip).Take(pageSize).Select(m => new {
                    ProductTypeId = m.ProductTypeId,
                    TypeName = m.TypeName,
                    IsInventoryTracked = m.IsInventoryTracked,
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
            var query = _context.ProductTypes.AsQueryable();
            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(m =>
                    (m.TypeName != null && m.TypeName.Contains(searchValue)));
            }
            var list = await query.Select(m => new {
                m.ProductTypeId,
                m.TypeName,
                m.IsInventoryTracked,
                m.IsActive
            }).ToListAsync();

            var bytes = DigiPOSE.Services.CyberExcelExportService.ExportToExcel(list, "ProductTypes", "Product Types Registry Export");
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ProductTypes_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.ProductTypes.FirstOrDefaultAsync(m => m.ProductTypeId == id);
            if (item == null) return NotFound();
            return PartialView("_DetailsPartial", item);
        }

        public IActionResult Create()
            => PartialView("_CreateOrEditPartial", new ProductType());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductType model)
        {
            if (!ModelState.IsValid) return PartialView("_CreateOrEditPartial", model);
            _context.Add(model);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Product type created successfully." });
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.ProductTypes.FindAsync(id);
            if (item == null) return NotFound();
            return PartialView("_CreateOrEditPartial", item);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductType model)
        {
            if (id != model.ProductTypeId) return Json(new { success = false, message = "ID mismatch." });
            if (!ModelState.IsValid) return PartialView("_CreateOrEditPartial", model);
            try { _context.Update(model); await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { return Json(new { success = false, message = "Concurrency error." }); }
            return Json(new { success = true, message = "Product type updated successfully." });
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.ProductTypes.FirstOrDefaultAsync(m => m.ProductTypeId == id);
            if (item == null) return NotFound();
            return PartialView("_DeletePartial", item);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.ProductTypes.FindAsync(id);
            if (item == null) return Json(new { success = false, message = "Record not found." });
            _context.ProductTypes.Remove(item);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Product type permanently deleted." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var item = await _context.ProductTypes.FindAsync(id);
            if (item == null) return Json(new { success = false });
            item.IsActive = !item.IsActive;
            await _context.SaveChangesAsync();
            return Json(new { success = true, isActive = item.IsActive, message = item.IsActive ? "Activated." : "Deactivated." });
        }
    }
}
