using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AbilityKit.Orleans.Gateway.Core;

public sealed class GatewayBackgroundTaskQueue : BackgroundService
{
    private readonly ILogger<GatewayBackgroundTaskQueue> _logger;

    private readonly Channel<Func<CancellationToken, ValueTask>> _queue = Channel.CreateUnbounded<Func<CancellationToken, ValueTask>>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public GatewayBackgroundTaskQueue(ILogger<GatewayBackgroundTaskQueue> logger)
    {
        _logger = logger;
    }

    public bool TryQueue(Func<CancellationToken, ValueTask> workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        return _queue.Writer.TryWrite(workItem);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var workItem in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await workItem(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Gateway background task failed.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Channel completed or host shutting down — normal termination.
        }
    }
}
