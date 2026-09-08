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
[Authorize]
[Route("api/documents")]
public sealed class DocumentsController(DeedAiDbContext db, IBlobStorage blobs, IOcrJobQueue queue) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentListItem>>> List(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? assigneeUserId,
        [FromQuery] bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var role = User.FindFirstValue(ClaimTypes.Role) ?? AppRoles.Viewer;
        IQueryable<Document> query = includeDeleted && AppRoles.CanAdmin(role)
            ? db.Documents.IgnoreQueryFilters()
            : db.Documents;

        query = query.Include(x => x.Client).Include(x => x.Assignee);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Name.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (clientId is not null)
        {
            query = query.Where(x => x.ClientId == clientId);
        }

        if (assigneeUserId is not null)
        {
            query = query.Where(x => x.AssigneeUserId == assigneeUserId);
        }

        var items = await query
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new DocumentListItem(
                x.Id,
                x.Name,
                x.Client.Name,
                x.ClientId,
                x.Status,
                x.UpdatedAt,
                x.Assignee != null ? x.Assignee.DisplayName : null,
                x.AssigneeUserId,
                x.Status == DocumentStatuses.Failed,
                x.DeletedAt != null))
            .ToListAsync(cancellationToken);

        return items;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentDetail>> Get(Guid id, CancellationToken cancellationToken)
    {
        var document = await db.Documents
            .Include(x => x.Client)
            .Include(x => x.Assignee)
            .Include(x => x.Fields)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        var neighbors = await db.Documents
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var index = neighbors.IndexOf(id);
        var previous = index > 0 ? neighbors[index - 1] : (Guid?)null;
        var next = index >= 0 && index < neighbors.Count - 1 ? neighbors[index + 1] : (Guid?)null;

        var fields = document.Fields;
        return new DocumentDetail(
            document.Id,
            document.Name,
            document.Client.Name,
            document.ClientId,
            document.Status,
            document.UpdatedAt,
            document.Assignee?.DisplayName,
            document.AssigneeUserId,
            document.ErrorMessage,
            document.DiRawBlobPath,
            new FieldDraft(
                fields?.Grantor,
                fields?.Grantee,
                fields?.InstrumentDate,
                fields?.Consideration,
                fields?.ParcelId,
                fields?.Client ?? document.Client.Name,
                fields?.Notes,
                fields?.IsDraft ?? false),
            previous,
            next);
    }

    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> File(Guid id, CancellationToken cancellationToken)
    {
        var document = await db.Documents.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        if (!await blobs.ExistsAsync(document.BlobPath, cancellationToken))
        {
            return NotFound(new { message = "PDF is not available for this demo row or has not been uploaded yet." });
        }

        var stream = await blobs.OpenReadAsync(document.BlobPath, cancellationToken);
        return new FileStreamResult(stream, "application/pdf")
        {
            FileDownloadName = document.Name
        };
    }

    [HttpPut("{id:guid}/fields")]
    [Authorize(Policy = RolePolicies.CanEdit)]
    public async Task<ActionResult<FieldDraft>> UpdateFields(
        Guid id,
        [FromBody] FieldUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var document = await db.Documents.Include(x => x.Fields).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        var fields = document.Fields ?? new DocumentFields
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id
        };

        fields.Grantor = request.Grantor;
        fields.Grantee = request.Grantee;
        fields.InstrumentDate = request.InstrumentDate;
        fields.Consideration = request.Consideration;
        fields.ParcelId = request.ParcelId;
        fields.Client = request.Client;
        fields.Notes = request.Notes;
        fields.IsDraft = request.IsDraft;
        fields.UpdatedAt = DateTimeOffset.UtcNow;
        document.UpdatedAt = DateTimeOffset.UtcNow;

        if (document.Fields is null)
        {
            db.DocumentFields.Add(fields);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new FieldDraft(
            fields.Grantor,
            fields.Grantee,
            fields.InstrumentDate,
            fields.Consideration,
            fields.ParcelId,
            fields.Client,
            fields.Notes,
            fields.IsDraft);
    }

    [HttpPost("{id:guid}/retry")]
    [Authorize(Policy = RolePolicies.CanEdit)]
    public async Task<IActionResult> Retry(Guid id, CancellationToken cancellationToken)
    {
        var document = await db.Documents.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        document.Status = DocumentStatuses.Queued;
        document.ErrorMessage = null;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await queue.EnqueueAsync(new OcrJobMessage { DocumentId = document.Id, BlobPath = document.BlobPath }, cancellationToken);
        return Ok(new { message = "Queued for OCR", status = document.Status });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = RolePolicies.CanEdit)]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken cancellationToken)
    {
        var document = await db.Documents.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        document.DeletedAt = DateTimeOffset.UtcNow;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Soft-deleted. An Admin can restore it later." });
    }

    [HttpPost("{id:guid}/restore")]
    [Authorize(Policy = RolePolicies.CanAdmin)]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        var document = await db.Documents.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        document.DeletedAt = null;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Restored." });
    }
}
