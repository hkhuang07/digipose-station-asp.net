using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using DigiPOSE.Models;
using DigiPOSE.Services;

namespace DigiPOSE.Services.Background
{
    public class InventoryWarmupWorker : IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IInventoryRAMService _ramService;
        private readonly ILogger<InventoryWarmupWorker> _logger;

        public InventoryWarmupWorker(
            IServiceScopeFactory scopeFactory,
            IInventoryRAMService ramService,
            ILogger<InventoryWarmupWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _ramService = ramService;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation(">>> [WARMUP_INIT]: Synchronizing physical SQL database inventory into RAM cache...");
            
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DigiPoseDbContext>();

            try
            {
                var allInventories = await db.ProductInventories
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                foreach (var inv in allInventories)
                {
                    _ramService.InitializeOrUpdateStock(inv.TenantId, inv.ProductId, inv.StockQuantity);
                }

                _logger.LogInformation(">>> [WARMUP_SUCCESS]: Successfully cached {Count} SKU balances into ConcurrentDictionary.", allInventories.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ">>> [WARMUP_CRITICAL]: Failed to seed RAM inventory during application startup.");
                throw; // Fast-fail server boot if inventory cannot be verified
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
