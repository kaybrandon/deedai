using System.Collections.Concurrent;
using System.Text.Json;
using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Ocr;

namespace DeedAi.Infrastructure.Queueing;

public sealed class InMemoryOcrJobQueue : IOcrJobQueue
{
    private readonly ConcurrentQueue<HeldMessage> _ready = new();
    private readonly ConcurrentDictionary<string, HeldMessage> _invisible = new();

    public Task EnqueueAsync(OcrJobMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ready.Enqueue(new HeldMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            PopReceipt = Guid.NewGuid().ToString("N"),
            Job = message,
            DequeueCount = 0
        });
        return Task.CompletedTask;
    }

    public Task<OcrQueueDelivery?> ReceiveAsync(TimeSpan visibilityTimeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReleaseExpired();
        if (!_ready.TryDequeue(out var held))
        {
            return Task.FromResult<OcrQueueDelivery?>(null);
        }

        held.DequeueCount++;
        held.VisibleAt = DateTimeOffset.UtcNow.Add(visibilityTimeout);
        held.PopReceipt = Guid.NewGuid().ToString("N");
        _invisible[held.MessageId] = held;
        return Task.FromResult<OcrQueueDelivery?>(new OcrQueueDelivery
        {
            Job = held.Job,
            MessageId = held.MessageId,
            PopReceipt = held.PopReceipt,
            DequeueCount = held.DequeueCount
        });
    }

    public Task DeleteAsync(OcrQueueDelivery delivery, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _invisible.TryRemove(delivery.MessageId, out _);
        return Task.CompletedTask;
    }

    public Task EnqueuePoisonForTestsAsync(OcrJobMessage message, int dequeueCount)
    {
        _ready.Enqueue(new HeldMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            PopReceipt = Guid.NewGuid().ToString("N"),
            Job = message,
            DequeueCount = dequeueCount
        });
        return Task.CompletedTask;
    }

    public string Snapshot() => JsonSerializer.Serialize(_ready.Select(x => x.Job));

    private void ReleaseExpired()
    {
        foreach (var pair in _invisible)
        {
            if (pair.Value.VisibleAt <= DateTimeOffset.UtcNow && _invisible.TryRemove(pair.Key, out var held))
            {
                _ready.Enqueue(held);
            }
        }
    }

    private sealed class HeldMessage
    {
        public required string MessageId { get; set; }
        public required string PopReceipt { get; set; }
        public required OcrJobMessage Job { get; set; }
        public int DequeueCount { get; set; }
        public DateTimeOffset VisibleAt { get; set; }
    }
}
