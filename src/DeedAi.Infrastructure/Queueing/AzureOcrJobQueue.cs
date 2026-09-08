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
}
