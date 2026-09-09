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
        [FromQuery] Guid? flagId,
        [FromQuery] bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var role = ClientAccess.Role(User);
        var allowed = await ClientAccess.AllowedClientIdsAsync(db, User, cancellationToken);
        IQueryable<Document> query = includeDeleted && AppRoles.CanAdmin(role)
            ? db.Documents.IgnoreQueryFilters()
            : db.Documents;
        query = ClientAccess.VisibleDocuments(query, allowed)
            .Include(x => x.Client)
            .Include(x => x.Assignee)
            .Include(x => x.Flags).ThenInclude(x => x.Flag);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Name.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status || x.ReviewStatus == status);
        }

        if (clientId is not null)
        {
            query = query.Where(x => x.ClientId == clientId);
        }

        if (assigneeUserId is not null)
        {
            query = query.Where(x => x.AssigneeUserId == assigneeUserId);
        }

        if (flagId is not null)
        {
            query = query.Where(x => x.Flags.Any(f => f.FlagDefinitionId == flagId));
        }

        var rows = await query.AsNoTracking().ToListAsync(cancellationToken);
        return rows.OrderByDescending(x => x.UpdatedAt).Select(ToListItem).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentDetail>> Get(Guid id, CancellationToken cancellationToken)
    {
        var document = await LoadVisible(id, includeDeleted: false, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        var allowed = await ClientAccess.AllowedClientIdsAsync(db, User, cancellationToken);
        var neighbors = (await ClientAccess.VisibleDocuments(db.Documents.AsNoTracking(), allowed).ToListAsync(cancellationToken))
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => x.Id)
            .ToList();
        var index = neighbors.IndexOf(id);
        var previous = index > 0 ? neighbors[index - 1] : (Guid?)null;
        var next = index >= 0 && index < neighbors.Count - 1 ? neighbors[index + 1] : (Guid?)null;

        var fields = document.Fields;
        var linked = document.OutgoingLinks
            .Select(x => new LinkedDocument(x.TargetDocumentId, x.Target.Name, x.Note))
            .Concat(document.IncomingLinks.Select(x => new LinkedDocument(x.SourceDocumentId, x.Source.Name, x.Note)))
            .GroupBy(x => x.Id)
            .Select(g => g.First())
            .ToList();

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
            document.DeedType,
            document.ReviewStatus,
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
            next,
            document.Flags.Select(x => new FlagSummary(x.FlagDefinitionId, x.Flag.Name, x.Flag.Color)).ToList(),
            document.Team.Select(x => new TeamMember(x.UserId, x.User.DisplayName, x.User.Role)).ToList(),
            linked);
    }

    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> File(Guid id, CancellationToken cancellationToken)
    {
        var document = await LoadVisible(id, includeDeleted: false, cancellationToken);
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
        var document = await LoadVisible(id, includeDeleted: false, cancellationToken);
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
        if (request.DeedType is not null)
        {
            document.DeedType = request.DeedType;
        }

        if (request.ReviewStatus is not null)
        {
            document.ReviewStatus = request.ReviewStatus;
        }

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
        var document = await LoadVisible(id, includeDeleted: false, cancellationToken);
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

    [HttpPut("{id:guid}/assignee")]
    [Authorize(Policy = RolePolicies.CanEdit)]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignRequest request, CancellationToken cancellationToken)
    {
        var document = await LoadVisible(id, includeDeleted: false, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        if (request.AssigneeUserId is not null && !await db.Users.AnyAsync(x => x.Id == request.AssigneeUserId && x.IsActive, cancellationToken))
        {
            return BadRequest(new { message = "Select a valid assignee." });
        }

        document.AssigneeUserId = request.AssigneeUserId;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Assigned." });
    }

    [HttpPost("bulk-assign")]
    [Authorize(Policy = RolePolicies.CanEdit)]
    public async Task<IActionResult> BulkAssign([FromBody] BulkAssignRequest request, CancellationToken cancellationToken)
    {
        if (request.DocumentIds.Count == 0)
        {
            return BadRequest(new { message = "Select at least one document." });
        }

        if (request.AssigneeUserId is not null && !await db.Users.AnyAsync(x => x.Id == request.AssigneeUserId && x.IsActive, cancellationToken))
        {
            return BadRequest(new { message = "Select a valid assignee." });
        }

        var allowed = await ClientAccess.AllowedClientIdsAsync(db, User, cancellationToken);
        var docs = await ClientAccess.VisibleDocuments(db.Documents, allowed)
            .Where(x => request.DocumentIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        foreach (var document in docs)
        {
            document.AssigneeUserId = request.AssigneeUserId;
            document.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = $"Assigned {docs.Count} document(s).", count = docs.Count });
    }

    [HttpPut("{id:guid}/flags")]
    [Authorize(Policy = RolePolicies.CanEdit)]
    public async Task<IActionResult> SetFlags(Guid id, [FromBody] SetFlagsRequest request, CancellationToken cancellationToken)
    {
        var document = await LoadVisible(id, includeDeleted: false, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        var existing = await db.DocumentFlags.Where(x => x.DocumentId == id).ToListAsync(cancellationToken);
        db.DocumentFlags.RemoveRange(existing);
        var valid = await db.FlagDefinitions
            .Where(x => request.FlagIds.Contains(x.Id) && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        foreach (var flagId in valid.Distinct())
        {
            db.DocumentFlags.Add(new DocumentFlag { DocumentId = id, FlagDefinitionId = flagId });
        }

        document.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Flags updated." });
    }

    [HttpPost("{id:guid}/links")]
    [Authorize(Policy = RolePolicies.CanEdit)]
    public async Task<IActionResult> Link(Guid id, [FromBody] LinkDocumentRequest request, CancellationToken cancellationToken)
    {
        if (id == request.TargetDocumentId)
        {
            return BadRequest(new { message = "A document cannot link to itself." });
        }

        var source = await LoadVisible(id, includeDeleted: false, cancellationToken);
        var target = await LoadVisible(request.TargetDocumentId, includeDeleted: false, cancellationToken);
        if (source is null || target is null)
        {
            return NotFound();
        }

        var exists = await db.DocumentLinks.AnyAsync(
            x => (x.SourceDocumentId == id && x.TargetDocumentId == request.TargetDocumentId)
                 || (x.SourceDocumentId == request.TargetDocumentId && x.TargetDocumentId == id),
            cancellationToken);
        if (!exists)
        {
            db.DocumentLinks.Add(new DocumentLink
            {
                SourceDocumentId = id,
                TargetDocumentId = request.TargetDocumentId,
                Note = request.Note
            });
            source.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { message = "Linked." });
    }

    [HttpDelete("{id:guid}/links/{targetId:guid}")]
    [Authorize(Policy = RolePolicies.CanEdit)]
    public async Task<IActionResult> Unlink(Guid id, Guid targetId, CancellationToken cancellationToken)
    {
        var source = await LoadVisible(id, includeDeleted: false, cancellationToken);
        if (source is null)
        {
            return NotFound();
        }

        var links = await db.DocumentLinks
            .Where(x => (x.SourceDocumentId == id && x.TargetDocumentId == targetId)
                        || (x.SourceDocumentId == targetId && x.TargetDocumentId == id))
            .ToListAsync(cancellationToken);
        db.DocumentLinks.RemoveRange(links);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Unlinked." });
    }

    [HttpPost("{id:guid}/team")]
    [Authorize(Policy = RolePolicies.CanEdit)]
    public async Task<IActionResult> AddTeam(Guid id, [FromBody] TeamMemberRequest request, CancellationToken cancellationToken)
    {
        var document = await LoadVisible(id, includeDeleted: false, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        if (!await db.Users.AnyAsync(x => x.Id == request.UserId && x.IsActive, cancellationToken))
        {
            return BadRequest(new { message = "Select a valid team member." });
        }

        if (!await db.DocumentTeamMembers.AnyAsync(x => x.DocumentId == id && x.UserId == request.UserId, cancellationToken))
        {
            db.DocumentTeamMembers.Add(new DocumentTeamMember { DocumentId = id, UserId = request.UserId });
            document.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { message = "Team member added." });
    }

    [HttpDelete("{id:guid}/team/{userId:guid}")]
    [Authorize(Policy = RolePolicies.CanEdit)]
    public async Task<IActionResult> RemoveTeam(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var document = await LoadVisible(id, includeDeleted: false, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        var row = await db.DocumentTeamMembers.FirstOrDefaultAsync(x => x.DocumentId == id && x.UserId == userId, cancellationToken);
        if (row is not null)
        {
            db.DocumentTeamMembers.Remove(row);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { message = "Team member removed." });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = RolePolicies.CanEdit)]
    public async Task<IActionResult> SoftDelete(Guid id, CancellationToken cancellationToken)
    {
        var document = await LoadVisible(id, includeDeleted: false, cancellationToken);
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

    private async Task<Document?> LoadVisible(Guid id, bool includeDeleted, CancellationToken cancellationToken)
    {
        var allowed = await ClientAccess.AllowedClientIdsAsync(db, User, cancellationToken);
        IQueryable<Document> query = includeDeleted ? db.Documents.IgnoreQueryFilters() : db.Documents;
        return await ClientAccess.VisibleDocuments(query, allowed)
            .Include(x => x.Client)
            .Include(x => x.Assignee)
            .Include(x => x.Fields)
            .Include(x => x.Flags).ThenInclude(x => x.Flag)
            .Include(x => x.Team).ThenInclude(x => x.User)
            .Include(x => x.OutgoingLinks).ThenInclude(x => x.Target)
            .Include(x => x.IncomingLinks).ThenInclude(x => x.Source)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    private static DocumentListItem ToListItem(Document x) =>
        new(
            x.Id,
            x.Name,
            x.Client.Name,
            x.ClientId,
            x.Status,
            x.UpdatedAt,
            x.Assignee?.DisplayName,
            x.AssigneeUserId,
            x.Status == DocumentStatuses.Failed,
            x.DeletedAt != null,
            x.DeedType,
            x.ReviewStatus,
            x.Flags.Select(f => new FlagSummary(f.FlagDefinitionId, f.Flag.Name, f.Flag.Color)).ToList());
}
