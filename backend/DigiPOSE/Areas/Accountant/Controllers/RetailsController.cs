using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigiPOSE.Models;
using System.Linq.Dynamic.Core;

namespace DigiPOSE.Areas.Accountant.Controllers
{
    [Area("Accountant")]
    [Authorize(Roles = "Super Admin, Administrator, Tenant Manager, Accountant")]
    public class RetailsController : Controller
    {
        private readonly DigiPoseDbContext _context;
        public RetailsController(DigiPoseDbContext context) { _context = context; }

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

                var query = _context.Retails
                    .Include(r => r.Tenant)
                    .Include(r => r.User)
                    .Include(r => r.PaymentMethod)
                    .AsQueryable();

                int totalRecords = query.Count();

                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(m =>
                        m.DocNo.Contains(searchValue) ||
                        (m.RetailNo != null && m.RetailNo.Contains(searchValue)) ||
                        (m.BuyerLegalName != null && m.BuyerLegalName.Contains(searchValue)) ||
                        (m.Tenant != null && m.Tenant.TenantName != null && m.Tenant.TenantName.Contains(searchValue)));
                }

                int filterRecords = query.Count();

                if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortColumnDirection))
                    query = query.OrderBy(sortColumn + " " + sortColumnDirection);
                else
                    query = query.OrderByDescending(r => r.EndDate);

                var dataList = query.Skip(skip).Take(pageSize).Select(m => new {
                    RetailId = m.RetailId,
                    DocNo = m.DocNo,
                    RetailNo = m.RetailNo ?? "",
                    DocType = m.DocType,
                    TenantName = m.Tenant != null ? m.Tenant.TenantName : "",
                    BuyerName = m.BuyerLegalName ?? "Walk-in",
                    PaymentMethod = m.PaymentMethod != null ? m.PaymentMethod.MethodName : "",
                    TotalAmount = m.TotalAmount,
                    EndDate = m.EndDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    IsEInvoice = m.IsEInvoiceReported
                }).ToList();

                return Json(new { draw = draw, recordsFiltered = filterRecords, recordsTotal = totalRecords, data = dataList });
            }
            catch (Exception ex)
            {
                return Json(new { error = "An error occurred: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(string? searchValue)
        {
            var query = _context.Retails
                .Include(r => r.Tenant).Include(r => r.User).Include(r => r.PaymentMethod)
                .AsQueryable();
            if (!string.IsNullOrEmpty(searchValue))
                query = query.Where(m => m.DocNo.Contains(searchValue) || (m.BuyerLegalName != null && m.BuyerLegalName.Contains(searchValue)));

            var list = await query.OrderByDescending(r => r.EndDate).Select(m => new {
                m.RetailId, m.DocNo, m.RetailNo, m.DocType,
                Tenant = m.Tenant != null ? m.Tenant.TenantName : "",
                Cashier = m.User != null ? m.User.UserName : "",
                BuyerName = m.BuyerLegalName ?? "Walk-in",
                m.BuyerTaxCode,
                Payment = m.PaymentMethod != null ? m.PaymentMethod.MethodName : "",
                m.TotalQuantity, m.GrossAmount, m.DiscountAmount, m.VatAmount,
                m.TotalAmount, m.TenderedAmount, m.ChangeAmount,
                Date = m.Date.ToString("yyyy-MM-dd"),
                EndDate = m.EndDate.ToString("yyyy-MM-dd HH:mm:ss"),
                m.IsEInvoiceReported, m.PrintNo
            }).ToListAsync();

            var bytes = DigiPOSE.Services.CyberExcelExportService.ExportToExcel(list, "Retails", "Retail Trade Documents Export");
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Retails_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
        }

        private void LoadViewBags(int? orderId = null, int? tenantId = null, int? counterId = null, int? shiftId = null, int? userId = null, int? customerId = null, int? paymentMethodId = null)
        {
            ViewBag.Orders = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Orders.Select(o => new { o.OrderId, DisplayText = $"Order #{o.OrderId} ({o.InvoiceNumber ?? "N/A"}) - {o.TotalAmount:N0} VND" }), "OrderId", "DisplayText", orderId);
            ViewBag.Tenants = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Tenants, "TenantId", "TenantName", tenantId);
            ViewBag.Counters = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Counters, "CounterId", "CounterName", counterId);
            ViewBag.Shifts = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Shifts, "ShiftId", "ShiftName", shiftId);
            ViewBag.Users = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Users, "UserId", "UserName", userId);
            ViewBag.Customers = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.Customers, "CustomerId", "FullName", customerId);
            ViewBag.PaymentMethods = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(_context.PaymentMethods, "PaymentMethodId", "MethodName", paymentMethodId);
        }

        public IActionResult Create()
        {
            LoadViewBags();
            var model = new Retail
            {
                DocNo = $"DOC-POS-01-SH01-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}",
                RetailNo = $"REC-01-{new Random().Next(1000, 9999)}",
                DocType = "POS_RETAIL",
                IdempotencyKey = Guid.NewGuid(),
                Date = DateTime.Now,
                EndDate = DateTime.Now,
                PrintNo = 1,
                TotalQuantity = 1
            };
            return PartialView("_CreateOrEditPartial", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Retail model)
        {
            ModelState.Remove(nameof(model.Order));
            ModelState.Remove(nameof(model.Tenant));
            ModelState.Remove(nameof(model.Counter));
            ModelState.Remove(nameof(model.Shift));
            ModelState.Remove(nameof(model.User));
            ModelState.Remove(nameof(model.Customer));
            ModelState.Remove(nameof(model.PaymentMethod));

            if (model.IdempotencyKey == Guid.Empty)
            {
                model.IdempotencyKey = Guid.NewGuid();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Retails.Add(model);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Retail accounting document created successfully." });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = "Database save failed: " + ex.Message });
                }
            }
            LoadViewBags(model.OrderId, model.TenantId, model.CounterId, model.ShiftId, model.UserId, model.CustomerId, model.PaymentMethodId);
            return PartialView("_CreateOrEditPartial", model);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Retails.FindAsync(id);
            if (item == null) return NotFound();
            LoadViewBags(item.OrderId, item.TenantId, item.CounterId, item.ShiftId, item.UserId, item.CustomerId, item.PaymentMethodId);
            return PartialView("_CreateOrEditPartial", item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Retail model)
        {
            if (id != model.RetailId) return Json(new { success = false, message = "ID mismatch anomaly." });

            ModelState.Remove(nameof(model.Order));
            ModelState.Remove(nameof(model.Tenant));
            ModelState.Remove(nameof(model.Counter));
            ModelState.Remove(nameof(model.Shift));
            ModelState.Remove(nameof(model.User));
            ModelState.Remove(nameof(model.Customer));
            ModelState.Remove(nameof(model.PaymentMethod));

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Retails.Update(model);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Retail accounting document updated successfully." });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = "Update execution failed: " + ex.Message });
                }
            }
            LoadViewBags(model.OrderId, model.TenantId, model.CounterId, model.ShiftId, model.UserId, model.CustomerId, model.PaymentMethodId);
            return PartialView("_CreateOrEditPartial", model);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Retails
                .Include(r => r.Order).ThenInclude(o => o!.OrderDetails!)
                .Include(r => r.Tenant).Include(r => r.Counter).Include(r => r.Shift)
                .Include(r => r.User).Include(r => r.Customer).Include(r => r.PaymentMethod)
                .FirstOrDefaultAsync(m => m.RetailId == id);
            if (item == null) return NotFound();
            return PartialView("_DetailsPartial", item);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Retails
                .Include(r => r.Tenant).Include(r => r.User)
                .FirstOrDefaultAsync(m => m.RetailId == id);
            if (item == null) return NotFound();
            return PartialView("_DeletePartial", item);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.Retails.FindAsync(id);
            if (item != null) { _context.Retails.Remove(item); await _context.SaveChangesAsync(); }
            return Json(new { success = true, message = "Retail document deleted." });
        }
    }
}
