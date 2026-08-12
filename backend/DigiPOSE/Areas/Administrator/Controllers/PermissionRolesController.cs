using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DigiPOSE.Models;
using System.Linq.Dynamic.Core;

namespace DigiPOSE.Areas.Administrator.Controllers
{
    [Area("Administrator")]
    [Authorize(Roles = "Super Admin, Administrator")]
    public class PermissionRolesController : Controller
    {
        private readonly DigiPoseDbContext _context;
        public PermissionRolesController(DigiPoseDbContext context) { _context = context; }

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

                var query = _context.PermissionRoles.Include(pr => pr.Role).Include(pr => pr.Permission).ThenInclude(p => p!.Module).AsQueryable();

                int totalRecords = query.Count();

                // Searching
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(m =>
                        (m.Role != null && m.Role.RoleName != null && m.Role.RoleName.Contains(searchValue)) ||
                        (m.Permission != null && m.Permission.PermissionName != null && m.Permission.PermissionName.Contains(searchValue)) ||
                        (m.Permission != null && m.Permission.Module != null && m.Permission.Module.ModuleName != null && m.Permission.Module.ModuleName.Contains(searchValue)));
                }

                int filterRecords = query.Count();

                // Sorting
                if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortColumnDirection))
                {
                    query = query.OrderBy(sortColumn + " " + sortColumnDirection);
                }
                else
                {
                    // Default sort by Role then Permission
                    query = query.OrderBy(pr => pr.RoleId).ThenBy(pr => pr.PermissionId);
                }

                // Paging & Mapping
                var dataList = query.Skip(skip).Take(pageSize).Select(m => new {
                    RoleId = m.RoleId,
                    PermissionId = m.PermissionId,
                    RoleName = m.Role != null ? m.Role.RoleName : "",
                    PermissionName = m.Permission != null ? m.Permission.PermissionName : "",
                    SystemModule = m.Permission != null && m.Permission.Module != null ? m.Permission.Module.ModuleName : "GLOBAL"
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
            var query = _context.PermissionRoles
                .Include(pr => pr.Role)
                .Include(pr => pr.Permission).ThenInclude(p => p!.Module)
                .AsQueryable();
            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(m =>
                    (m.Role != null && m.Role.RoleName != null && m.Role.RoleName.Contains(searchValue)) ||
                    (m.Permission != null && m.Permission.PermissionName != null && m.Permission.PermissionName.Contains(searchValue)));
            }
            var list = await query.Select(m => new {
                RoleName = m.Role != null ? m.Role.RoleName : "",
                PermissionName = m.Permission != null ? m.Permission.PermissionName : "",
                SystemModule = m.Permission != null && m.Permission.Module != null ? m.Permission.Module.ModuleName : "GLOBAL"
            }).ToListAsync();

            var bytes = DigiPOSE.Services.CyberExcelExportService.ExportToExcel(list, "PermissionRoles", "Permission-Role Matrix Export");
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"PermissionRoles_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
        }

        public IActionResult Create()
        {
            ViewBag.RoleId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Roles, "RoleId", "RoleName");
            ViewBag.PermissionId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Permissions, "PermissionId", "PermissionName");
            return PartialView("_CreateOrEditPartial", new PermissionRole());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PermissionRole model)
        {
            if (ModelState.IsValid)
            {
                // Check if already exists
                var exists = await _context.PermissionRoles.AnyAsync(pr => pr.RoleId == model.RoleId && pr.PermissionId == model.PermissionId);
                if (exists)
                {
                    return Json(new { success = false, message = "This Permission is already assigned to this Role." });
                }

                _context.Add(model);
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Permission assigned to Role successfully." });
            }
            ViewBag.RoleId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Roles, "RoleId", "RoleName", model.RoleId);
            ViewBag.PermissionId = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Permissions, "PermissionId", "PermissionName", model.PermissionId);
            return PartialView("_CreateOrEditPartial", model);
        }

        public async Task<IActionResult> Delete(int? roleId, int? permissionId)
        {
            if (roleId == null || permissionId == null) return NotFound();
            var item = await _context.PermissionRoles.Include(pr => pr.Role).Include(pr => pr.Permission)
                                     .FirstOrDefaultAsync(m => m.RoleId == roleId && m.PermissionId == permissionId);
            if (item == null) return NotFound();
            return PartialView("_DeletePartial", item);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int roleId, int permissionId)
        {
            var item = await _context.PermissionRoles.FindAsync(roleId, permissionId);
            if (item != null) { _context.PermissionRoles.Remove(item); await _context.SaveChangesAsync(); }
            return Json(new { success = true, message = "Permission unassigned successfully." });
        }
    }
}
