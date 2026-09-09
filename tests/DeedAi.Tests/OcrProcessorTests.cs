using DeedAi.Domain;
using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Entities;
using DeedAi.Domain.Ocr;
using DeedAi.Infrastructure.Data;
using DeedAi.Infrastructure.Email;
using DeedAi.Infrastructure.Health;
using DeedAi.Infrastructure.Ocr;
using DeedAi.Infrastructure.Queueing;
using DeedAi.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DeedAi.Tests;

public sealed class OcrProcessorTests
{
    [Fact]
    public async Task Happy_path_marks_ready_and_stores_di_raw_pointer()
    {
        await using var ctx = await CreateDb();
        var blobs = new InMemoryBlobStorage();
        var document = await SeedDocument(ctx, blobs, "Deed_ok.pdf");
        var processor = CreateProcessor(ctx, blobs, new MockDocumentIntelligenceClient());

        var delivery = new OcrQueueDelivery
        {
            Job = new OcrJobMessage { DocumentId = document.Id, BlobPath = document.BlobPath },
            MessageId = "1",
            PopReceipt = "r",
            DequeueCount = 1
        };

        await processor.ProcessAsync(delivery, CancellationToken.None);

        var updated = await ctx.Documents.Include(x => x.Fields).SingleAsync(x => x.Id == document.Id);
        Assert.Equal(DocumentStatuses.Ready, updated.Status);
        Assert.False(string.IsNullOrWhiteSpace(updated.DiRawBlobPath));
        Assert.True(await blobs.ExistsAsync(updated.DiRawBlobPath!, CancellationToken.None));
        Assert.Equal("Jane Example", updated.Fields?.Grantor);
        Assert.False(updated.Fields!.IsDraft);
    }

    [Fact]
    public async Task Fail_path_marks_failed_and_is_retryable()
    {
        await using var ctx = await CreateDb();
        var blobs = new InMemoryBlobStorage();
        var document = await SeedDocument(ctx, blobs, "Scan_bad.pdf");
        var queue = new InMemoryOcrJobQueue();
        var processor = CreateProcessor(ctx, blobs, new MockDocumentIntelligenceClient());

        var delivery = new OcrQueueDelivery
        {
            Job = new OcrJobMessage { DocumentId = document.Id, BlobPath = document.BlobPath },
            MessageId = "2",
            PopReceipt = "r",
            DequeueCount = 1
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync(delivery, CancellationToken.None));

        var updated = await ctx.Documents.SingleAsync(x => x.Id == document.Id);
        Assert.Equal(DocumentStatuses.Failed, updated.Status);
        Assert.Contains("OCR failed", updated.ErrorMessage);

        updated.Status = DocumentStatuses.Queued;
        updated.ErrorMessage = null;
        await ctx.SaveChangesAsync();
        await queue.EnqueueAsync(new OcrJobMessage { DocumentId = updated.Id, BlobPath = updated.BlobPath }, CancellationToken.None);
        var retry = await queue.ReceiveAsync(TimeSpan.FromMinutes(1), CancellationToken.None);
        Assert.NotNull(retry);
        Assert.Equal(updated.Id, retry!.Job.DocumentId);
    }

    [Fact]
    public async Task Poison_dequeue_marks_failed_with_retry_message()
    {
        await using var ctx = await CreateDb();
        var blobs = new InMemoryBlobStorage();
        var document = await SeedDocument(ctx, blobs, "Deed_ok.pdf");
        var processor = CreateProcessor(ctx, blobs, new MockDocumentIntelligenceClient(), poisonCount: 5);

        var delivery = new OcrQueueDelivery
        {
            Job = new OcrJobMessage { DocumentId = document.Id, BlobPath = document.BlobPath },
            MessageId = "3",
            PopReceipt = "r",
            DequeueCount = 5
        };

        await processor.ProcessAsync(delivery, CancellationToken.None);

        var updated = await ctx.Documents.SingleAsync(x => x.Id == document.Id);
        Assert.Equal(DocumentStatuses.Failed, updated.Status);
        Assert.Contains("Retry", updated.ErrorMessage);
        Assert.Contains("poison", updated.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static OcrProcessor CreateProcessor(
        DeedAiDbContext db,
        IBlobStorage blobs,
        IDocumentIntelligenceClient di,
        int poisonCount = 5) =>
        new(db, blobs, di, Options.Create(new OcrOptions { PoisonDequeueCount = poisonCount }), NullLogger<OcrProcessor>.Instance, new NullOcrNotifier(), new OcrPipelineSignal());

    private static async Task<DeedAiDbContext> CreateDb()
    {
        var options = new DbContextOptionsBuilder<DeedAiDbContext>()
            .UseSqlite($"Data Source=file:ocr-{Guid.NewGuid():N}?mode=memory&cache=shared")
            .Options;
        var db = new DeedAiDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        db.Clients.Add(new Client { Id = DatabaseSeeder.AcmeId, Name = "Acme" });
        await db.SaveChangesAsync();
        return db;
    }

    private static async Task<Document> SeedDocument(DeedAiDbContext db, InMemoryBlobStorage blobs, string name)
    {
        var document = new Document
        {
            Id = Guid.NewGuid(),
            Name = name,
            ClientId = DatabaseSeeder.AcmeId,
            Status = DocumentStatuses.Queued,
            BlobPath = $"deeds/{Guid.NewGuid():N}.pdf",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        await using var pdf = new MemoryStream("%PDF-1.4 test"u8.ToArray());
        await blobs.UploadAsync(document.BlobPath, pdf, "application/pdf", CancellationToken.None);
        return document;
    }
}
