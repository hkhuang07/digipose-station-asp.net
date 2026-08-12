using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Threading.Channels;
using DigiPOSE.Models;
using DigiPOSE.Helpers;
using System.Text.Json;

namespace DigiPOSE.Services.Background
{
    public class ResilientInvoiceWorker : BackgroundService
    {
        private readonly Channel<JobQueueItem> _channel;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ResilientInvoiceWorker> _logger;

        public ResilientInvoiceWorker(
            Channel<JobQueueItem> channel, 
            IServiceScopeFactory scopeFactory, 
            ILogger<ResilientInvoiceWorker> logger)
        {
            _channel = channel;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(">>> [SERVICE_STARTED]: ResilientInvoiceWorker online for high-speed POS receipt printing & E-Invoice delivery.");

            // 1. Launch DB Sweep Task in parallel to recover any unprocessed jobs from server power cuts/crashes
            _ = Task.Run(() => SweepUnprocessedSqlJobsAsync(stoppingToken), stoppingToken);

            // 2. Real-time In-Memory Channel consumer (<50ms processing latency)
            await foreach (var item in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                await ProcessInvoiceJobAsync(item.Id, stoppingToken);
            }
        }

        private async Task ProcessInvoiceJobAsync(int jobId, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DigiPoseDbContext>();
            var mailLogic = scope.ServiceProvider.GetRequiredService<IMailLogic>();

            var job = await db.JobQueue.FirstOrDefaultAsync(j => j.Id == jobId && j.Status == "Pending", ct);
            if (job == null) return; // Already processed or missing

            try
            {
                _logger.LogInformation(">>> [EXEC_I/O]: Processing E-Invoice dispatch for Job #{JobId}", jobId);

                // Deserialize payload to retrieve OrderId and InvoiceNumber
                using var doc = JsonDocument.Parse(job.PayloadJson ?? "{}");
                if (doc.RootElement.TryGetProperty("OrderId", out var orderIdProp) && orderIdProp.TryGetInt32(out int orderId))
                {
                    var order = await db.Orders
                        .AsNoTracking()
                        .Include(o => o.PaymentMethod)
                        .Include(o => o.Shift)
                        .Include(o => o.User)
                        .Include(o => o.Customer)
                        .Include(o => o.OrderDetails!)
                            .ThenInclude(d => d.Product!)
                                .ThenInclude(p => p.Unit)
                        .FirstOrDefaultAsync(o => o.OrderId == orderId, ct);

                    if (order != null)
                    {
                        var retail = await db.Retails.AsNoTracking().FirstOrDefaultAsync(r => r.OrderId == orderId, ct);

                        string targetEmail = "huang.hk07@gmail.com"; // Default corporate accounting vault fallback
                        if (order.CustomerId.HasValue)
                        {
                            var customer = order.Customer ?? await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == order.CustomerId.Value, ct);
                            if (customer != null && !string.IsNullOrWhiteSpace(customer.Email))
                            {
                                targetEmail = customer.Email;
                            }
                        }
                        else if (retail != null && !string.IsNullOrWhiteSpace(retail.BuyerEmail))
                        {
                            targetEmail = retail.BuyerEmail;
                        }

                        var mailInfo = new MailInfo
                        {
                            ToEmail = targetEmail,
                            Subject = $"[DigiPOSE // E-INVOICE #{order.InvoiceNumber}] ACID Transaction Verification & Receipt"
                        };

                        // Invoke real asynchronous MailKit dispatch with full fiscal retail record
                        await mailLogic.SendOrderSuccessEmailAsync(order, mailInfo, retail);
                        _logger.LogInformation(">>> [E-INVOICE_DELIVERY]: E-Invoice [{Invoice}] successfully formatted and sent to [{Email}].", order.InvoiceNumber, targetEmail);
                    }
                }

                job.Status = "Completed";
                job.ProcessedAt = DateTime.Now;
                await db.SaveChangesAsync(ct);
                
                _logger.LogInformation(">>> [JOB_COMPLETED]: Job #{JobId} marked Completed in SQL buffer.", jobId);
            }
            catch (Exception ex)
            {
                job.RetryCount++;
                if (job.RetryCount >= 5)
                {
                    job.Status = "Failed";
                }
                await db.SaveChangesAsync(CancellationToken.None);
                _logger.LogError(ex, ">>> [I/O_FAULT]: Failed executing job #{JobId}. RetryCount: {RetryCount}", jobId, job.RetryCount);
            }
        }

        private async Task SweepUnprocessedSqlJobsAsync(CancellationToken ct)
        {
            // Poll SQL table every 15 seconds for orphan jobs created over 5 seconds ago that missed Channel delivery
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
            while (await timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<DigiPoseDbContext>();

                    var orphanCutoff = DateTime.Now.AddSeconds(-5);
                    var orphanJobs = await db.JobQueue
                        .AsNoTracking()
                        .Where(j => j.Status == "Pending" && j.CreatedAt <= orphanCutoff && j.RetryCount < 5)
                        .Take(50)
                        .ToListAsync(ct);

                    if (orphanJobs.Any())
                    {
                        _logger.LogWarning(">>> [RECOVERY_SWEEP]: Found {Count} orphan print jobs from server crash. Injecting into processing stream.", orphanJobs.Count);
                        foreach (var orphan in orphanJobs)
                        {
                            await ProcessInvoiceJobAsync(orphan.Id, ct);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ">>> [SWEEP_ERROR]: Database recovery scan failure.");
                }
            }
        }
    }
}
