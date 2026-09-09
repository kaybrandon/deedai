using DeedAi.Domain.Abstractions;
using DeedAi.Infrastructure.Ocr;
using Microsoft.Extensions.Options;

namespace DeedAi.Worker;

public sealed class OcrQueueWorker(
    IServiceScopeFactory scopes,
    IOcrJobQueue queue,
    IOptions<OcrOptions> options,
    ILogger<OcrQueueWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Deed AI OCR worker listening on Azure Storage Queue / configured queue with long-poll (not 1s polling).");
        var visibility = TimeSpan.FromSeconds(Math.Max(30, options.Value.VisibilityTimeoutSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecordWorkerSignalAsync(dequeue: false, stoppingToken);
                var delivery = await queue.ReceiveAsync(visibility, stoppingToken);
                if (delivery is null)
                {
                    continue;
                }

                await RecordWorkerSignalAsync(dequeue: true, stoppingToken);
                using var scope = scopes.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<OcrProcessor>();
                try
                {
                    await processor.ProcessAsync(delivery, stoppingToken);
                    await queue.DeleteAsync(delivery, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "OCR job failed for {DocumentId}; message stays invisible until timeout or poison.", delivery.Job.DocumentId);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OCR worker loop error");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task RecordWorkerSignalAsync(bool dequeue, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var health = scope.ServiceProvider.GetRequiredService<DeedAi.Infrastructure.Health.OcrHealthRecorder>();
            if (dequeue)
            {
                await health.RecordDequeueAsync(stoppingToken);
                return;
            }

            await health.RecordWorkerHeartbeatAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "OCR worker health signal was skipped.");
        }
    }
}
