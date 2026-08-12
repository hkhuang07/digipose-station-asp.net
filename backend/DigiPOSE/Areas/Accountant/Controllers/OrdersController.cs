using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DigiPOSE.Models;
using DigiPOSE.Services;
using DigiPOSE.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Linq.Dynamic.Core;

namespace DigiPOSE.Areas.Accountant.Controllers
{
    [Area("Accountant")]
    [Authorize(Roles = "Super Admin, Administrator, Tenant Manager, Accountant")]
    public class OrdersController : Controller
    {
        private readonly DigiPoseDbContext _context;
        private readonly IInventoryRAMService _inventoryRam;
        private readonly IHubContext<PosRealtimeHub> _hubContext;
        private readonly IInventoryLedgerService _ledgerService;

        public OrdersController(DigiPoseDbContext context, IInventoryRAMService inventoryRam, IHubContext<PosRealtimeHub> hubContext, IInventoryLedgerService ledgerService) 
        { 
            _context = context; 
            _inventoryRam = inventoryRam;
            _hubContext = hubContext;
            _ledgerService = ledgerService;
        }

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
                // >>> [DRAFT FILTER]: Frontend toggle — showDraft=true shows all, false hides drafts (StatusId=4)
                var showDraftStr = Request.Form["showDraft"].FirstOrDefault() ?? "true";
                bool showDraft = showDraftStr.ToLower() != "false";

                int pageSize = length != null ? Convert.ToInt32(length) : 0;
                int skip = start != null ? Convert.ToInt32(start) : 0;

                var query = _context.Orders
                    .Include(x => x.Shift)
                    .Include(x => x.User)
                    .Include(x => x.Customer)
                    .Include(x => x.PaymentMethod)
                    .Include(x => x.OrderStatus)
                    .AsQueryable();

                // Apply draft visibility filter
                if (!showDraft)
                    query = query.Where(m => m.StatusId != 1);

                // Count drafts for frontend chip badge (always from full set)
                int draftCount = await _context.Orders.CountAsync(o => o.StatusId == 1);

                int totalRecords = query.Count();

                // Searching
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(m =>
                        m.OrderId.ToString().Contains(searchValue) ||
                        (m.SnapshotCustomerName != null && m.SnapshotCustomerName.Contains(searchValue)) ||
                        (m.Customer != null && m.Customer.FullName != null && m.Customer.FullName.Contains(searchValue)) ||
                        (m.OrderStatus != null && m.OrderStatus.StatusName != null && m.OrderStatus.StatusName.Contains(searchValue)));
                }

                int filterRecords = query.Count();

                // Sorting
                if (!string.IsNullOrEmpty(sortColumn) && !string.IsNullOrEmpty(sortColumnDirection))
                {
                    query = query.OrderBy(sortColumn + " " + sortColumnDirection);
                }
                else
                {
                    // >>> [SORT PRIORITY]: Show drafts last (they are less important), completed orders first
                    query = query.OrderBy(m => m.StatusId == 1 ? 1 : 0).ThenByDescending(v => v.CreatedAt);
                }

                // Paging & Mapping
                var dataList = query.Skip(skip).Take(pageSize).Select(m => new {
                    OrderId = m.OrderId,
                    CustomerName = m.SnapshotCustomerName != null ? m.SnapshotCustomerName : (m.Customer != null ? m.Customer.FullName : "Walk-in"),
                    StatusName = m.OrderStatus != null ? m.OrderStatus.StatusName : "",
                    BadgeColor = m.OrderStatus != null ? (m.OrderStatus.BadgeColor ?? "#6c757d") : "#6c757d",
                    TotalAmount = m.TotalAmount,
                    CreatedAt = m.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    // >>> [DRAFT FLAG]: Frontend uses this to apply visual dimming + special badge
                    IsDraft = m.StatusId == 1
                }).ToList();

                return Json(new { draw = draw, recordsFiltered = filterRecords, recordsTotal = totalRecords, draftCount = draftCount, data = dataList });
            }
            catch (Exception ex)
            {
                return Json(new { error = "An error occurred while loading data. Error: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(string? searchValue)
        {
            var query = _context.Orders
                .Include(x => x.User)
                .Include(x => x.Customer)
                .Include(x => x.PaymentMethod)
                .Include(x => x.OrderStatus)
                .AsQueryable();
            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(m =>
                    (m.InvoiceNumber != null && m.InvoiceNumber.Contains(searchValue)) ||
                    (m.Customer != null && m.Customer.FullName != null && m.Customer.FullName.Contains(searchValue)) ||
                    (m.User != null && m.User.UserName != null && m.User.UserName.Contains(searchValue)));
            }
            var list = await query.Select(m => new {
                m.OrderId,
                InvoiceNumber = m.InvoiceNumber ?? $"ORD-{m.OrderId:D6}",
                Customer = m.SnapshotCustomerName != null ? m.SnapshotCustomerName : (m.Customer != null ? m.Customer.FullName : "Walk-in"),
                Staff = m.User != null ? m.User.UserName : "",
                Status = m.OrderStatus != null ? m.OrderStatus.StatusName : "",
                Payment = m.PaymentMethod != null ? m.PaymentMethod.MethodName : "",
                CreatedAt = m.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                m.GrossAmount,
                m.DiscountAmount,
                m.TaxAmount,
                m.TotalAmount
            }).ToListAsync();

            var bytes = DigiPOSE.Services.CyberExcelExportService.ExportToExcel(list, "Orders", "Sales Orders Ledger Export");
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Orders_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx");
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var item = await _context.Orders
                .Include(x => x.Shift)
                .Include(x => x.User)
                .Include(x => x.Customer)
                .Include(x => x.PaymentMethod)
                .Include(x => x.OrderStatus)
                .FirstOrDefaultAsync(m => m.OrderId == id);
            if (item == null) return NotFound();
            return PartialView("_DetailsPartial", item);
        }

        public IActionResult Create()
        {
            try
            {
                LoadViewBags();
                return PartialView("_CreateOrEditPartial", new Order { IdempotencyKey = Guid.NewGuid(), CreatedAt = DateTime.Now });
            }
            catch (Exception ex)
            {
                return Content($"<div class='alert alert-danger'>Error loading order creation form: {ex.Message}</div>", "text/html");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Order model)
        {
            try
            {
                model.CreatedAt = DateTime.Now;
                if (model.IdempotencyKey == Guid.Empty)
                {
                    model.IdempotencyKey = Guid.NewGuid();
                }

                // Remove navigation property validation constraints from form post
                ModelState.Remove(nameof(model.Shift));
                ModelState.Remove(nameof(model.User));
                ModelState.Remove(nameof(model.Customer));
                ModelState.Remove(nameof(model.OrderStatus));
                ModelState.Remove(nameof(model.PaymentMethod));
                ModelState.Remove(nameof(model.OrderDetails));
                ModelState.Remove(nameof(model.invoice));

                if (ModelState.IsValid)
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try {
                        _context.Add(model);
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync(); 
                        return Json(new { success = true, message = "Order created successfully." });
                    } catch (Exception dbEx) { 
                        await transaction.RollbackAsync(); 
                        var innerMsg = dbEx.InnerException != null ? dbEx.InnerException.Message : dbEx.Message;
                        return Json(new { success = false, message = $"Database transaction failed: {innerMsg}" }); 
                    }
                }

                LoadViewBags(model.TenantId, model.ShiftId, model.UserId, model.CustomerId, model.StatusId, model.PaymentMethodId);
                return PartialView("_CreateOrEditPartial", model);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Server error while creating order: {ex.Message}" });
            }
        }

        public async Task<IActionResult> Edit(int? id)
        {
            try
            {
                if (id == null) return NotFound();
                var item = await _context.Orders.FindAsync(id);
                if (item == null) return NotFound();
                LoadViewBags(item.TenantId, item.ShiftId, item.UserId, item.CustomerId, item.StatusId, item.PaymentMethodId);
                return PartialView("_CreateOrEditPartial", item);
            }
            catch (Exception ex)
            {
                return Content($"<div class='alert alert-danger'>Error loading order edit form: {ex.Message}</div>", "text/html");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Order model)
        {
            if (id != model.OrderId) return Json(new { success = false, message = "ID mismatch." });

            try
            {
                // Remove navigation property validation constraints from form post
                ModelState.Remove(nameof(model.Shift));
                ModelState.Remove(nameof(model.User));
                ModelState.Remove(nameof(model.Customer));
                ModelState.Remove(nameof(model.OrderStatus));
                ModelState.Remove(nameof(model.PaymentMethod));
                ModelState.Remove(nameof(model.OrderDetails));
                ModelState.Remove(nameof(model.invoice));

                if (ModelState.IsValid)
                {
                    // >>> [IMMUTABLE_FIELD_GUARD]: Fetch original record to preserve IdempotencyKey (unique index) and CreatedAt timestamp
                    var original = await _context.Orders.AsNoTracking()
                        .FirstOrDefaultAsync(o => o.OrderId == id);
                    if (original == null) return Json(new { success = false, message = "Order not found." });

                    // Preserve immutable fields that must NOT change after order creation
                    model.IdempotencyKey = original.IdempotencyKey;
                    model.CreatedAt = original.CreatedAt;

                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try {
                        _context.Update(model);
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync(); 
                        return Json(new { success = true, message = "Order updated successfully." });
                    } catch (Exception dbEx) { 
                        await transaction.RollbackAsync(); 
                        var innerMsg = dbEx.InnerException != null ? dbEx.InnerException.Message : dbEx.Message;
                        return Json(new { success = false, message = $"Database transaction failed: {innerMsg}" }); 
                    }
                }
                LoadViewBags(model.TenantId, model.ShiftId, model.UserId, model.CustomerId, model.StatusId, model.PaymentMethodId);
                return PartialView("_CreateOrEditPartial", model);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Server error while updating order: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.Orders
                .Include(o => o.OrderDetails!)
                .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(m => m.OrderId == id);

            if (item != null)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var restoredProducts = new List<int>();
                    if (item.OrderDetails != null && item.StatusId != 4) // Only restore inventory if order was active/completed, not a raw draft
                    {
                        foreach (var detail in item.OrderDetails)
                        {
                            if (detail.NatureId == 1) // Physical good -> restore stock to shelves via Enterprise DDD Ledger Service
                            {
                                int? userId = null;
                                if (int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out int uid)) userId = uid;

                                await _ledgerService.RecordTransactionAsync(
                                    item.TenantId,
                                    detail.ProductId,
                                    detail.Quantity, // Positive delta to put items back on shelves
                                    InventoryTxType.Return,
                                    item.OrderId,
                                    $"RET-ORD-{item.OrderId}",
                                    userId,
                                    detail.UnitPrice,
                                    $"Stock restored upon deleting order #{item.OrderId}");

                                restoredProducts.Add(detail.ProductId);
                            }
                        }
                    }

                    if (item.OrderDetails != null) _context.OrderDetails.RemoveRange(item.OrderDetails);
                    _context.Orders.Remove(item);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // >>> [REALTIME SIGNALR HUD BROADCAST]: Immediately notify POS terminals of restored physical inventory (<1ms)
                    if (restoredProducts.Any())
                    {
                        var liveBalances = await _inventoryRam.GetBulkStockAsync(item.TenantId, restoredProducts);
                        await _hubContext.Clients.Group($"Tenant_{item.TenantId}").SendAsync("OnStockChanged", liveBalances);
                    }

                    return Json(new { success = true, message = "Order deleted and stock restored successfully." });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "Failed to delete order: " + ex.Message });
                }
            }
            return Json(new { success = false, message = "Not found." });
        }

        private void LoadViewBags(int? val_TenantId = null, int? val_ShiftId = null, int? val_UserId = null, int? val_CustomerId = null, int? val_StatusId = null, int? val_PaymentMethodId = null)
        {
            try
            {
                if (!_context.OrderStatuses.Any())
                {
                    _context.OrderStatuses.AddRange(
                        new OrderStatus { StatusId = 1, StatusName = "Draft", Description = "Order created but not yet submitted" },
                        new OrderStatus { StatusId = 2, StatusName = "Pending", Description = "Order awaiting payment confirmation" },
                        new OrderStatus { StatusId = 3, StatusName = "Confirmed", Description = "Order has been confirmed and is being processed" },
                        new OrderStatus { StatusId = 4, StatusName = "Processing", Description = "Order is being picked, packed, or prepared for shipment" },
                        new OrderStatus { StatusId = 5, StatusName = "Shipped", Description = "Order has been shipped or handed to the carrier" },
                        new OrderStatus { StatusId = 6, StatusName = "In Transit", Description = "Order is in transit to the customer" },
                        new OrderStatus { StatusId = 7, StatusName = "Delivered", Description = "Order has been delivered to the customer" },
                        new OrderStatus { StatusId = 8, StatusName = "Completed", Description = "Order finalized — all payments and deliveries completed" },
                        new OrderStatus { StatusId = 12, StatusName = "Cancelled", Description = "Order has been cancelled before fulfillment" }
                    );
                    _context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(">>> [ORDER_STATUS_SEED_ERR]: " + ex.Message);
            }

            ViewBag.TenantId = new SelectList(_context.Tenants.AsNoTracking(), "TenantId", "TenantName", val_TenantId);
            ViewBag.ShiftId = new SelectList(_context.Shifts.AsNoTracking(), "ShiftId", "ShiftId", val_ShiftId);
            ViewBag.UserId = new SelectList(_context.Users.AsNoTracking(), "UserId", "UserName", val_UserId);
            ViewBag.CustomerId = new SelectList(_context.Customers.AsNoTracking(), "CustomerId", "FullName", val_CustomerId);
            ViewBag.StatusId = new SelectList(_context.OrderStatuses.AsNoTracking(), "StatusId", "StatusName", val_StatusId);
            ViewBag.PaymentMethodId = new SelectList(_context.PaymentMethods.AsNoTracking(), "PaymentMethodId", "MethodName", val_PaymentMethodId);
        }
    }
}
