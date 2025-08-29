using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HttpGossip.Internal
{
    internal sealed class LogWriterHostedService : BackgroundService
    {
        private readonly LogQueue _queue;
        private readonly ILogRepository _repo;
        private readonly ILogger<LogWriterHostedService> _logger;

        public LogWriterHostedService(LogQueue queue, ILogRepository repo, ILogger<LogWriterHostedService> logger)
        {
            _queue = queue;
            _repo = repo;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var item in _queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await _repo.InsertAsync(item, stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Never penalize the API — log and continue
                    _logger.LogWarning(ex, "[HttpGossip] Failed to persist log (ignored).");
                }
            }
        }
    }
}