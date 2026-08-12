using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigiPOSE.Models;
using DigiPOSE.Web.Helpers;
using System.Linq.Dynamic.Core;

namespace DigiPOSE.Areas.Catalog.Controllers
{
    [Area("Catalog")]
    [Authorize(Roles = "Super Admin, Administrator, Tenant Manager, Catalog")]
    public class ProductsController : Controller
    {
        private readonly DigiPoseDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ProductsController(DigiPoseDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            return View();
        }

        public async Task<IActionResult> Index_LoadData()
        {
            try{
                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault();
                var sortColumn = Request.Form["columns[" + Request.Form["order[0][column]"].FirstOrDefault() + "][name]"].FirstOrDefault();
                var sortColumnDirection = Request.Form["order[0][dir]"].FirstOrDefault();
                var searchValue = Request.Form["search[value]"].FirstOrDefault();
                int pageSize = length != null ? int.Parse(length) : 0;
                int skip = start != null ? int.Parse(start) : 0;
                
                var query = _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.ProductType)
                    .Include(p => p.Unit)
                    .Include(p => p.Manufacturer)
                    .Include(p => p.TaxType)
                    .AsQueryable();
                
                int totalRecords = query.Count();

                // Tìm kiếm (Searching)
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(p => 
                        (p.ProductName != null && p.ProductName.Contains(searchValue)) ||
                        (p.SKU != null && p.SKU.Contains(searchValue)) ||
                        (p.Category != null && p.Category.CategoryName.Contains(searchValue)) ||
                        (p.Unit != null && p.Unit.UnitName.Contains(searchValue)) ||
                        (p.Manufacturer != null && p.Manufacturer.ManufacturerName.Contains(searchValue)) ||
                        (p.ProductType != null && p.ProductType.TypeName.Contains(searchValue)) ||
                        (p.TaxType != null && p.TaxType.TaxName.Contains(searchValue)));
                }
                int filterRecords = query.Count();

                // Sắp xếp (Sorting) sử dụng System.Linq.Dynamic.Core
                if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortColumnDirection))
                {
                    query = query.OrderBy(sortColumn + " " + sortColumnDirection);
                }

                // Phân trang (Paging) và Map dữ liệu trả về
                var dataList = query.Skip(skip).Take(pageSize).Select(p => new {
                    ProductId = p.ProductId,
                    ImageUrl = p.ImageUrl,
                    SKU = p.SKU,
                    Slug = p.Slug,
                    ProductName = p.ProductName,
                    CategoryName = p.Category != null ? p.Category.CategoryName : "",
                    ProductTypeName = p.ProductType != null ? p.ProductType.TypeName : "",
                    UnitName = p.Unit != null ? p.Unit.UnitName : "",
                    ManufacturerName = p.Manufacturer != null ? p.Manufacturer.ManufacturerName : "",
                    TaxTypeName = p.TaxType != null ? p.TaxType.TaxName : "",
                    CostPrice = p.CostPrice,
                    BasePrice = p.BasePrice,
                    Barcode = p.Barcode,
                    //MinStockLevel = p.MinStockLevel,
                    //MaxStockLevel = p.MaxStockLevel,
                    Description = p.Description,
                    RowVersion = p.RowVersion,
                    IsActive = p.IsActive
                }).ToList();

                // Format Json Data
                return Json(new { draw = draw, recordsFiltered = filterRecords, recordsTotal = totalRecords, data = dataList });
            }
            catch(Exception ex){
                return Json(new { error = "An error occurred while loading product data. Please try again. Error: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(string? searchValue)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Manufacturer)
                .Include(p => p.ProductType)
                .Include(p => p.Unit)
                .Include(p => p.TaxType)
                .AsQueryable();
            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(m =>
                    (m.ProductName != null && m.ProductName.Contains(searchValue)) ||
                    (m.Barcode != null && m.Barcode.Contains(searchValue)) ||
                    (m.Category != null && m.Category.CategoryName != null && m.Category.CategoryName.Contains(searchValue)) ||
                    (m.Manufacturer != null && m.Manufacturer.ManufacturerName != null && m.Manufacturer.ManufacturerName.Contains(searchValue)));
            }
            var list = await query.Select(p => new {
                p.ProductId,
                p.ProductName,
                Category = p.Category != null ? p.Category.CategoryName : "",
                Type = p.ProductType != null ? p.ProductType.TypeName : "",
                Unit = p.Unit != null ? p.Unit.UnitName : "",
                Manufacturer = p.Manufacturer != null ? p.Manufacturer.ManufacturerName : "",
                p.CostPrice,
                p.BasePrice,
                p.Barcode,
                p.IsActive
            }).ToListAsync();

            var bytes = DigiPOSE.Services.CyberExcelExportService.ExportToExcel(list, "Products", "Product Catalog Export");
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Products_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Products
                .Include(p => p.Category).Include(p => p.ProductType)
                .Include(p => p.Unit).Include(p => p.Manufacturer).Include(p => p.TaxType)
                .Include(p => p.ItemNature)
                .FirstOrDefaultAsync(m => m.ProductId == id);
            if (item == null) return NotFound();
            return PartialView("_DetailsPartial", item);
        }

        public IActionResult Create()
        {
            var model = new Product { ItemNatureId = 1 };
            PopulateDropdowns(model);
            return PartialView("_CreateOrEditPartial", model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product model)
        {
            if (model.ItemNatureId <= 0)
                model.ItemNatureId = 1;

            if (string.IsNullOrWhiteSpace(model.Slug))
                model.Slug = SlugHelper.GenerateSlug(model.ProductName);
            
            // Auto-populate Min and Max stock levels
            //model.MinStockLevel = 0;
            //model.MaxStockLevel = 999999;

            ModelState.Remove("Slug");
            ModelState.Remove("RowVersion");

            if (!ModelState.IsValid)
            {
                PopulateDropdowns(model);
                return PartialView("_CreateOrEditPartial", model);
            }

            if(model.ImageUpload != null && model.ImageUpload.Length > 0)
            {
                string uploadsFolder = Path.Combine(_env.WebRootPath,"uploads","products");
                if(!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);
                
                string fileExtension = Path.GetExtension(model.ImageUpload.FileName).ToLower();
                string fileName = $"{SlugHelper.GenerateSlug(model.ProductName)}-{Guid.NewGuid().ToString("N")[..6]}{fileExtension}";
                string physicalPath = Path.Combine(uploadsFolder,fileName);

                using (var stream = new FileStream(physicalPath,FileMode.Create))
                {
                    await model.ImageUpload.CopyToAsync(stream);
                }
                model.ImageUrl = fileName;
            }
            
            _context.Add(model);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Product created successfully." });
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Products.FindAsync(id);
            if (item == null) return NotFound();
            PopulateDropdowns(item);
            return PartialView("_CreateOrEditPartial", item);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product model)
        {
            if (id != model.ProductId) 
                return Json(new { success = false, message = "ID mismatch." });

            if (string.IsNullOrWhiteSpace(model.Slug))
                model.Slug = SlugHelper.GenerateSlug(model.ProductName);
            
            
            ModelState.Remove("Slug");
            ModelState.Remove("RowVersion");

            if (!ModelState.IsValid)
            {
                PopulateDropdowns(model);
                return PartialView("_CreateOrEditPartial", model);
            }

            try
            {
                 if(model.ImageUpload != null && model.ImageUpload.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_env.WebRootPath,"uploads","products");
                    if(!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);
                    
                    string fileExtension = Path.GetExtension(model.ImageUpload.FileName).ToLower();
                    string fileName = $"{SlugHelper.GenerateSlug(model.ProductName)}-{Guid.NewGuid().ToString("N")[..6]}{fileExtension}";
                    string physicalPath = Path.Combine(uploadsFolder,fileName);

                    using (var stream = new FileStream(physicalPath,FileMode.Create))
                    {
                        await model.ImageUpload.CopyToAsync(stream);
                    }
                    //delete old image
                    if(!string.IsNullOrEmpty(model.ImageUrl))
                    {
                        string oldPhysicalPath = Path.Combine(uploadsFolder, model.ImageUrl);
                        if(System.IO.File.Exists(oldPhysicalPath))
                        {
                            System.IO.File.Delete(oldPhysicalPath);
                        }
                    }
                    model.ImageUrl = fileName;
                }

                _context.Update(model);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Product updated successfully." });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Json(new { success = false, message = "Concurrency error — please reload and try again." });
            }
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Products
                .Include(p => p.Category).Include(p => p.Unit)
                .FirstOrDefaultAsync(m => m.ProductId == id);
            if (item == null) return NotFound();
            return PartialView("_DeletePartial", item);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.Products.FindAsync(id);
            if (item == null) 
                return Json(new { success = false, message = "Record not found." });
            
            if(!string.IsNullOrEmpty(item.ImageUrl)){
                string oldPhysicalPath = Path.Combine(_env.WebRootPath, "uploads", "products", item.ImageUrl);
                if(System.IO.File.Exists(oldPhysicalPath))
                {
                    System.IO.File.Delete(oldPhysicalPath);
                }
            }
            
            _context.Products.Remove(item);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Product permanently deleted." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var item = await _context.Products.FindAsync(id);
            if (item == null) return Json(new { success = false });
            item.IsActive = !item.IsActive;
            await _context.SaveChangesAsync();
            return Json(new { success = true, isActive = item.IsActive, message = item.IsActive ? "Activated." : "Deactivated." });
        }

        private void PopulateDropdowns(Product? model = null)
        {
            ViewBag.CategoryId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Categories, "CategoryId", "CategoryName", model?.CategoryId);
            ViewBag.ProductTypeId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.ProductTypes.Where(x => x.IsActive), "ProductTypeId", "TypeName", model?.ProductTypeId);
            ViewBag.ItemNatureId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.ItemNatures, "NatureId", "NatureName", model?.ItemNatureId ?? 1);
            ViewBag.UnitId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Units, "UnitId", "UnitName", model?.UnitId);
            ViewBag.ManufacturerId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Manufacturers.Where(x => x.IsActive), "ManufacturerId", "ManufacturerName", model?.ManufacturerId);
            ViewBag.TaxTypeId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.TaxTypes.Where(x => x.IsActive), "TaxTypeId", "TaxName", model?.TaxTypeId);
        }
    }
}
