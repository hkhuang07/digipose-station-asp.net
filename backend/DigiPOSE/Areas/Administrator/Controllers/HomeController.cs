using DigiPOSE.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace DigiPOSE.Areas.Administrator.Controllers
{
    [Area("Administrator")]
    [Authorize(Roles = "Super Admin, Administrator, Tenant Manager")]
    public class HomeController : Controller
    {
        private readonly DigiPoseDbContext _context;

        public HomeController(DigiPoseDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = new DashboardViewModel();
            viewModel.Modules = new List<ModuleTelemetryInfo>();
            
            // Get all active System Modules
            var systemModules = await _context.SystemModules.Where(m => m.IsActive).OrderBy(m => m.SortOrder).ToListAsync();
            
            foreach (var sm in systemModules)
            {
                var mod = new ModuleTelemetryInfo
                {
                    ModuleId = $"MOD-{sm.ModuleId:D2}",
                    ModuleName = sm.ModuleName, // Standard Module Name from Database Column
                    Tables = new List<TableTelemetryInfo>()
                };

                // Map tables dynamically based on Module Name stored in DB
                if (sm.ModuleName == "System")
                {
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "Tenant", ControllerName = "Tenants", RecordCount = await _context.Tenants.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "Counter", ControllerName = "Counters", RecordCount = await _context.Counters.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "Permission", ControllerName = "Permissions", RecordCount = await _context.Permissions.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "PermissionRole", ControllerName = "PermissionRoles", RecordCount = await _context.PermissionRoles.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "Role", ControllerName = "Roles", RecordCount = await _context.Roles.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "Shift", ControllerName = "Shifts", RecordCount = await _context.Shifts.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "ShiftStatus", ControllerName = "ShiftStatuses", RecordCount = await _context.ShiftStatuses.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "SystemModule", ControllerName = "SystemModules", RecordCount = await _context.SystemModules.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "User", ControllerName = "Users", RecordCount = await _context.Users.CountAsync() });
                }
                else if (sm.ModuleName == "POS")
                {
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "CustomerType", ControllerName = "CustomerTypes", RecordCount = await _context.CustomerTypes.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "Customer", ControllerName = "Customers", RecordCount = await _context.Customers.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "Supplier", ControllerName = "Suppliers", RecordCount = await _context.Suppliers.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "Retail", ControllerName = "Retails", RecordCount = await _context.Retails.CountAsync() });
                }
                else if (sm.ModuleName == "Warehouse")
                {
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "ProductInventory", ControllerName = "ProductInventories", RecordCount = await _context.ProductInventories.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "StockVoucher", ControllerName = "StockVouchers", RecordCount = await _context.StockVouchers.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "StockVoucherDetail", ControllerName = "StockVoucherDetails", RecordCount = await _context.StockVoucherDetails.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "StockTransfer", ControllerName = "StockTransfers", RecordCount = await _context.StockTransfers.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "StockAudit", ControllerName = "StockAudits", RecordCount = await _context.StockAudits.CountAsync() });
                }
                else if (sm.ModuleName == "Catalog")
                {
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "Category", ControllerName = "Categories", RecordCount = await _context.Categories.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "Unit", ControllerName = "Units", RecordCount = await _context.Units.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "Manufacturer", ControllerName = "Manufacturers", RecordCount = await _context.Manufacturers.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "TaxType", ControllerName = "TaxTypes", RecordCount = await _context.TaxTypes.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "ProductType", ControllerName = "ProductTypes", RecordCount = await _context.ProductTypes.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "ItemNature", ControllerName = "ItemNatures", RecordCount = await _context.ItemNatures.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "Product", ControllerName = "Products", RecordCount = await _context.Products.CountAsync() });
                }
                else if (sm.ModuleName == "Finance")
                {
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "OrderStatus", ControllerName = "OrderStatuses", RecordCount = await _context.OrderStatuses.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "PaymentMethod", ControllerName = "PaymentMethods", RecordCount = await _context.PaymentMethods.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "Order", ControllerName = "Orders", RecordCount = await _context.Orders.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "OrderDetail", ControllerName = "OrderDetails", RecordCount = await _context.OrderDetails.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "InvoiceStatus", ControllerName = "InvoiceStatuses", RecordCount = await _context.InvoiceStatuses.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "InvoiceType", ControllerName = "InvoiceTypes", RecordCount = await _context.InvoiceTypes.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "Invoice", ControllerName = "Invoices", RecordCount = await _context.Invoices.CountAsync() });
                    mod.Tables.Add(new TableTelemetryInfo { TableName = "Subscription", ControllerName = "Subscriptions", RecordCount = await _context.Subscriptions.CountAsync() });
                }
                
                mod.TableCount = mod.Tables.Count;
                mod.TotalRecordCount = mod.Tables.Sum(t => t.RecordCount);
                
                if (mod.TableCount > 0)
                {
                    viewModel.Modules.Add(mod);
                }
            }

            viewModel.TotalTables = viewModel.Modules.Sum(m => m.TableCount);
            viewModel.TotalSystemRecords = viewModel.Modules.Sum(m => m.TotalRecordCount);

            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
