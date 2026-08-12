using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DigiPOSE.Models;

using System.Linq.Dynamic.Core;

namespace DigiPOSE.Areas.Administrator.Controllers
{
    [Area("Administrator")]
    [Authorize(Roles = "Super Admin, Administrator, Tenant Manager")]
    public class ShiftsController : Controller
    {
        private readonly DigiPoseDbContext _context;
        public ShiftsController(DigiPoseDbContext context) { _context = context; }

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

                var query = _context.Shifts
                    .Include(s => s.User)
                    .Include(s => s.Counter)
                    .Include(s => s.Status)
                    .AsQueryable();

                int totalRecords = query.Count();

                // Searching
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(m =>
                        (m.User != null && m.User.UserName.Contains(searchValue)) ||
                        (m.Counter != null && m.Counter.CounterName.Contains(searchValue)) ||
                        (m.Status != null && m.Status.StatusName.Contains(searchValue)));
                }

                int filterRecords = query.Count();

                // Sorting
                if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortColumnDirection))
                {
                    query = query.OrderBy(sortColumn + " " + sortColumnDirection);
                }
                else
                {
                    query = query.OrderByDescending(s => s.StartTime);
                }

                // Paging & Mapping
                var dataList = query.Skip(skip).Take(pageSize).ToList().Select(m => new {
                    ShiftId = m.ShiftId,
                    UserName = m.User != null ? m.User.UserName : "",
                    CounterName = m.Counter != null ? m.Counter.CounterName : "",
                    StartTime = m.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    EndTime = m.EndTime.HasValue ? m.EndTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : "",
                    StartCash = m.StartCash,
                    EndCash = m.EndCash,
                    StatusName = m.Status != null ? m.Status.StatusName : ""
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
            var query = _context.Shifts
                .Include(s => s.User)
                .Include(s => s.Counter)
                .Include(s => s.Status)
                .AsQueryable();
            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(m =>
                    (m.User != null && m.User.UserName != null && m.User.UserName.Contains(searchValue)) ||
                    (m.Counter != null && m.Counter.CounterName != null && m.Counter.CounterName.Contains(searchValue)) ||
                    (m.Status != null && m.Status.StatusName != null && m.Status.StatusName.Contains(searchValue)));
            }
            var list = await query.Select(m => new {
                m.ShiftId,
                UserName = m.User != null ? m.User.UserName : "",
                CounterName = m.Counter != null ? m.Counter.CounterName : "",
                StartTime = m.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                EndTime = m.EndTime.HasValue ? m.EndTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : "",
                m.StartCash,
                m.EndCash,
                StatusName = m.Status != null ? m.Status.StatusName : ""
            }).ToListAsync();

            var bytes = DigiPOSE.Services.CyberExcelExportService.ExportToExcel(list, "Shifts", "Shift Management Export");
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Shifts_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Shifts.Include(s => s.User).Include(s => s.Counter).Include(s => s.Status).FirstOrDefaultAsync(m => m.ShiftId == id);
            if (item == null) return NotFound();
            return PartialView("_DetailsPartial", item);
        }

        public IActionResult Create()
        {
            LoadViewBags();
            var now = DateTime.Now;
            return PartialView("_CreateOrEditPartial", new Shift { 
                StartTime = new DateTime(now.Year, now.Month, now.Day, 7, 0, 0),
                EndTime = new DateTime(now.Year, now.Month, now.Day, 23, 0, 0)
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Shift model)
        {
            if (ModelState.IsValid) { _context.Add(model); await _context.SaveChangesAsync(); return Json(new { success = true, message = "Shift created." }); }
            LoadViewBags(model.UserId, model.CounterId, model.StatusId);
            return PartialView("_CreateOrEditPartial", model);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Shifts.FindAsync(id);
            if (item == null) return NotFound();
            LoadViewBags(item.UserId, item.CounterId, item.StatusId);
            return PartialView("_CreateOrEditPartial", item);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Shift model)
        {
            if (id != model.ShiftId) return Json(new { success = false, message = "ID mismatch." });
            if (ModelState.IsValid)
            {
                try { _context.Update(model); await _context.SaveChangesAsync(); return Json(new { success = true, message = "Shift updated." }); }
                catch (DbUpdateConcurrencyException) { }
            }
            LoadViewBags(model.UserId, model.CounterId, model.StatusId);
            return PartialView("_CreateOrEditPartial", model);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Shifts.Include(s => s.User).Include(s => s.Counter).Include(s => s.Status).FirstOrDefaultAsync(m => m.ShiftId == id);
            if (item == null) return NotFound();
            return PartialView("_DeletePartial", item);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.Shifts.FindAsync(id);
            if (item != null) { _context.Shifts.Remove(item); await _context.SaveChangesAsync(); }
            return Json(new { success = true, message = "Shift deleted." });
        }

        private void LoadViewBags(int? userId = null, int? counterId = null, int? statusId = null)
        {
            ViewBag.UserId = new SelectList(_context.Users.Where(u => u.IsActive), "UserId", "UserName", userId);
            ViewBag.CounterId = new SelectList(_context.Counters.Where(c => c.IsActive), "CounterId", "CounterName", counterId);
            ViewBag.StatusId = new SelectList(_context.ShiftStatuses, "StatusId", "StatusName", statusId);
        }
    }
}
