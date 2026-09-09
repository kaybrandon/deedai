using System.Text.Json;
using Azure.Storage.Queues;
using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Ocr;

namespace DeedAi.Infrastructure.Queueing;

public sealed class AzureOcrJobQueue(QueueClient queue) : IOcrJobQueue
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task EnqueueAsync(OcrJobMessage message, CancellationToken cancellationToken)
    {
        await queue.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var payload = JsonSerializer.Serialize(message, Json);
        await queue.SendMessageAsync(payload, cancellationToken);
    }

    public async Task<OcrQueueDelivery?> ReceiveAsync(TimeSpan visibilityTimeout, CancellationToken cancellationToken)
    {
        await queue.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        // ReceiveMessagesAsync long-polls (up to ~30s) when the queue is empty.
        var response = await queue.ReceiveMessagesAsync(
            maxMessages: 1,
            visibilityTimeout: visibilityTimeout,
            cancellationToken: cancellationToken);

        var message = response.Value.FirstOrDefault();
        if (message is null)
        {
            return null;
        }

        var job = JsonSerializer.Deserialize<OcrJobMessage>(message.MessageText, Json)
                  ?? throw new InvalidOperationException("OCR queue message was not valid JSON.");

        return new OcrQueueDelivery
        {
            Job = job,
            MessageId = message.MessageId,
            PopReceipt = message.PopReceipt,
            DequeueCount = (int)message.DequeueCount
        };
    }

    public async Task DeleteAsync(OcrQueueDelivery delivery, CancellationToken cancellationToken)
    {
        await queue.DeleteMessageAsync(delivery.MessageId, delivery.PopReceipt, cancellationToken);
    }

    public async Task<bool> CanReachAsync(CancellationToken cancellationToken)
    {
        try
        {
            _ = await queue.ExistsAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<OcrQueueSnapshot> GetSnapshotAsync(int poisonDequeueCount, CancellationToken cancellationToken)
    {
        try
        {
            var exists = await queue.ExistsAsync(cancellationToken);
            if (exists.Value != true)
            {
                return new OcrQueueSnapshot(0, null, 0);
            }

            var properties = await queue.GetPropertiesAsync(cancellationToken);
            var depth = Math.Max(0, properties.Value.ApproximateMessagesCount);
            var peeked = await queue.PeekMessagesAsync(maxMessages: 32, cancellationToken);
            var messages = peeked.Value ?? [];
            DateTimeOffset? oldest = null;
            foreach (var message in messages)
            {
                if (message.InsertedOn is { } inserted && (oldest is null || inserted < oldest))
                {
                    oldest = inserted;
                }
            }

            var poisonThreshold = Math.Max(1, poisonDequeueCount);
            var poison = messages.Count(x => x.DequeueCount >= poisonThreshold);
            return new OcrQueueSnapshot(depth, oldest, poison);
        }
        catch
        {
            return new OcrQueueSnapshot(0, null, 0);
        }
    }
}
