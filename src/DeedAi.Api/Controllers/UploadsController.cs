using System.Security.Claims;
using DeedAi.Api.Contracts;
using DeedAi.Domain;
using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Entities;
using DeedAi.Domain.Ocr;
using DeedAi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeedAi.Api.Controllers;

[ApiController]
[Authorize(Policy = RolePolicies.CanUpload)]
[Route("api/uploads")]
public sealed class UploadsController(DeedAiDbContext db, IBlobStorage blobs, IOcrJobQueue queue) : ControllerBase
{
    public const long MaxBytes = 50L * 1024 * 1024;

    [HttpPost]
    [RequestSizeLimit(MaxBytes + (2 * 1024 * 1024))]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxBytes + (2 * 1024 * 1024))]
    public async Task<ActionResult<UploadResult>> Upload(
        [FromForm] Guid clientId,
        [FromForm] IFormFileCollection files,
        CancellationToken cancellationToken)
    {
        if (!await db.Clients.AnyAsync(x => x.Id == clientId, cancellationToken))
        {
            return BadRequest(new { message = "Select a valid Client." });
        }

        var allowed = await ClientAccess.AllowedClientIdsAsync(db, User, cancellationToken);
        if (!ClientAccess.CanSee(allowed, clientId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                title = "Access denied",
                message = "Access denied. You do not have access to this Client."
            });
        }

        var uploadedBy = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : (Guid?)null;

        var created = new List<DocumentListItem>();
        var errors = new List<string>();

        foreach (var file in files)
        {
            var error = ValidatePdf(file);
            if (error is not null)
            {
                errors.Add(error);
                continue;
            }

            var id = Guid.NewGuid();
            var blobPath = $"{clientId:N}/{id:N}/{file.FileName}";
            await using var stream = file.OpenReadStream();
            await blobs.UploadAsync(blobPath, stream, "application/pdf", cancellationToken);

            var document = new Document
            {
                Id = id,
                Name = file.FileName,
                ClientId = clientId,
                Status = DocumentStatuses.Queued,
                BlobPath = blobPath,
                UploadedByUserId = uploadedBy,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Documents.Add(document);
            await db.SaveChangesAsync(cancellationToken);
            await queue.EnqueueAsync(new OcrJobMessage { DocumentId = id, BlobPath = blobPath }, cancellationToken);

            var clientName = await db.Clients.Where(x => x.Id == clientId).Select(x => x.Name).FirstAsync(cancellationToken);
            created.Add(new DocumentListItem(
                document.Id,
                document.Name,
                clientName,
                clientId,
                document.Status,
                document.UpdatedAt,
                null,
                null,
                false,
                false,
                null,
                null,
                [],
                null,
                document.Status));
        }

        return new UploadResult(created.Count, created, errors);
    }

    private static string? ValidatePdf(IFormFile file)
    {
        if (file.Length <= 0)
        {
            return $"{file.FileName}: empty file.";
        }

        if (file.Length > MaxBytes)
        {
            return $"{file.FileName}: exceeds 50 MB limit.";
        }

        var ext = Path.GetExtension(file.FileName);
        if (!string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return $"{file.FileName}: PDF only.";
        }

        if (!string.IsNullOrEmpty(file.ContentType)
            && !file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            && !file.ContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return $"{file.FileName}: PDF only.";
        }

        return null;
    }
}
