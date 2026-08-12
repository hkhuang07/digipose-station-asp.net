global using Microsoft.AspNetCore.Mvc;
global using Microsoft.AspNetCore.Authorization;
using DigiPOSE.Models;
using DigiPOSE.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using DigiPOSE.Services;
using DigiPOSE.Services.Background;
using DigiPOSE.Hubs;
using System.Threading.Channels;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});
 
// Sử dụng DbContextPooling để tái sử dụng DbContext Instance, giảm tối đa chi phí cấp phát bộ nhớ (GC Pressure) khi xử lý hàng ngàn request/giây
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? builder.Configuration.GetConnectionString("DigiPoseConnection") 
    ?? builder.Configuration.GetConnectionString("DigiPoseDbContext");

builder.Services.AddDbContextPool<DigiPoseDbContext>(options => 
    options.UseSqlServer(connectionString));

// >>> [LEAN_LAN_ARCHITECTURE_SERVICES]: Singleton Lazy-Loading RAM Stock Engine, VAT Balancing Engine & Background Resilient Queue
builder.Services.AddSingleton<IInventoryRAMService, InventoryRAMService>();
builder.Services.AddSingleton<IVatBalancingEngine, VatBalancingEngine>();
builder.Services.AddScoped<IInventoryLedgerService, InventoryLedgerService>();
builder.Services.AddSingleton(Channel.CreateUnbounded<JobQueueItem>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }));
builder.Services.AddHostedService<ResilientInvoiceWorker>();
builder.Services.AddMemoryCache();
builder.Services.AddSignalR();
// >>> [RESILIENT_GIS_BFF_LAYER]: Register Polly v8 Standard Resilience Pipeline for offline-first GIS caching
builder.Services.AddHttpClient<IGisResilienceService, GisResilienceService>()
    .AddStandardResilienceHandler();

// >>> [ZERO_TRUST_BOT_DEFENSE]: Register Cloudflare Turnstile Settings and Resilient Exponential Backoff HttpClient
builder.Services.Configure<TurnstileSettings>(builder.Configuration.GetSection(TurnstileSettings.SectionName));
builder.Services.AddHttpClient<ICloudflareTurnstileService, CloudflareTurnstileService>()
    .AddStandardResilienceHandler();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "DigiPOSE.Auth";
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.LoginPath = "/Auth/Login";
    options.LogoutPath = "/Auth/Logout";
    options.AccessDeniedPath = "/Auth/Forbidden";
});

builder.Services.AddAuthorization(options =>
{
    var permissions = new[] {
        "System.Config.Manage", "System.Tenant.Manage", "System.Role.Manage", "System.User.Manage",
        "POS.Shift.Open", "POS.Shift.Close", "POS.Order.Create", "POS.Order.Void", "POS.Discount.Apply",
        "Warehouse.Inventory.View", "Warehouse.Voucher.Create", "Warehouse.Voucher.Approve", "Warehouse.Inventory.Adjust", "Warehouse.Supplier.Manage",
        "Catalog.Product.Manage", "Catalog.Category.Manage", "Catalog.Price.Manage",
        "Finance.Report.View", "Finance.Invoice.View", "Finance.Audit.Export"
    };
    
    foreach (var p in permissions)
    {
        options.AddPolicy(p, policy => policy.RequireClaim("Permission", p));
    }
});
builder.Services.AddHttpContextAccessor();

// Configure CORS for Next.JS Storefront & POS Frontend 
builder.Services.AddCors(options =>
{
    options.AddPolicy("StorefrontCorsPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddTransient<IMailLogic, MailLogic>();
// Lấy thông tin cấu hình trong tập tin appsettings.json và gán vào đối tượng MailSettings
builder.Services.Configure<MailSettings>(builder.Configuration.GetSection("MailSettings"));

var app = builder.Build();

// >>> [SELF-HEALING ARCHITECTURAL RECONCILIATION]: Automatically rectify historical orders where StatusId = 1 (Draft in schema) was assigned to paid checkouts.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DigiPoseDbContext>();
    try
    {
        var retailOrderIds = dbContext.Retails.Select(r => r.OrderId).Distinct().ToList();
        var historicalPaidOrders = dbContext.Orders
            .Where(o => (o.StatusId == 1 && (o.TenderedAmount > 0 || retailOrderIds.Contains(o.OrderId))) || (o.StatusId == 4 && retailOrderIds.Contains(o.OrderId)))
            .ToList();
        if (historicalPaidOrders.Any())
        {
            foreach (var ord in historicalPaidOrders)
            {
                ord.StatusId = 8; // 8: Completed
            }
            dbContext.SaveChanges();
            Console.WriteLine($">>> [SELF-HEALING_MIGRATION_SUCCESS]: Reconciled {historicalPaidOrders.Count} historical POS orders from Draft(1)/Processing(4) to Completed(8).");
        }

        // Reconcile true draft orders with StatusId == 4 (mislabeled as Draft previously) to StatusId = 1 (Draft)
        var historicalDrafts = dbContext.Orders
            .Where(o => o.StatusId == 4 && !retailOrderIds.Contains(o.OrderId) && o.TenderedAmount == 0)
            .ToList();
        if (historicalDrafts.Any())
        {
            foreach (var drf in historicalDrafts)
            {
                drf.StatusId = 1; // 1: Draft
            }
            dbContext.SaveChanges();
            Console.WriteLine($">>> [SELF-HEALING_MIGRATION_SUCCESS]: Reconciled {historicalDrafts.Count} draft POS orders from Processing(4) to Draft(1).");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine(">>> [SELF-HEALING_MIGRATION_ERR]: " + ex.Message);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();

// >>> [ZERO-TRUST ROUTE REMAPPING MIDDLEWARE]: Intercept legacy/bookmarked /Administrator/* URLs and transparently redirect to properly partitioned domain Areas
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    if (!string.IsNullOrEmpty(path) && path.StartsWith("/Administrator/", StringComparison.OrdinalIgnoreCase))
    {
        var catalogModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Categories", "Units", "Manufacturers", "TaxTypes", "ProductTypes", "ItemNatures", "Products"
        };
        var warehouseModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ProductInventories", "StockVouchers", "StockVoucherDetails", "StockTransfers", "StockAudits", "Suppliers"
        };
        var accountantModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "OrderStatuses", "PaymentMethods", "Orders", "OrderDetails", "InvoiceStatuses", "InvoiceTypes", "Invoices", "Retails"
        };

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 2 && string.Equals(segments[0], "Administrator", StringComparison.OrdinalIgnoreCase))
        {
            var controllerName = segments[1];
            string? targetArea = null;
            if (catalogModules.Contains(controllerName)) targetArea = "Catalog";
            else if (warehouseModules.Contains(controllerName)) targetArea = "Warehouse";
            else if (accountantModules.Contains(controllerName)) targetArea = "Accountant";

            if (targetArea != null)
            {
                var query = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : "";
                var targetUrl = $"/{targetArea}" + path.Substring(14) + query;
                context.Response.Redirect(targetUrl, permanent: false);
                return;
            }
        }
    }
    await next();
});

app.UseRouting();

app.UseCors("StorefrontCorsPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "administratorarea",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<PosRealtimeHub>("/hubs/pos");

app.Run();
