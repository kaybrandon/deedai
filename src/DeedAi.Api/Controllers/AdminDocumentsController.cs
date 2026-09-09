using DeedAi.Api.Contracts;
using DeedAi.Domain;
using DeedAi.Domain.Abstractions;
using DeedAi.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeedAi.Api.Controllers;

[ApiController]
[Authorize(Policy = RolePolicies.CanAdmin)]
[Route("api/admin/documents")]
public sealed class AdminDocumentsController(DeedAiDbContext db, IBlobStorage blobs) : ControllerBase
{
    [HttpGet("deleted")]
    public async Task<ActionResult<IReadOnlyList<DocumentListItem>>> Deleted(
        [FromQuery] Guid? clientId,
        CancellationToken cancellationToken)
    {
        var query = db.Documents.IgnoreQueryFilters()
            .Where(x => x.DeletedAt != null)
            .Include(x => x.Client)
            .Include(x => x.Assignee)
            .Include(x => x.Flags).ThenInclude(x => x.Flag)
            .AsQueryable();
        if (clientId is not null)
        {
            query = query.Where(x => x.ClientId == clientId);
        }

        var rows = await query.AsNoTracking().ToListAsync(cancellationToken);
        return rows.OrderByDescending(x => x.DeletedAt).Select(ToListItem).ToList();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> HardDelete(Guid id, CancellationToken cancellationToken)
    {
        var document = await db.Documents.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document is null)
        {
            return NotFound();
        }

        if (document.DeletedAt is null)
        {
            return BadRequest(new { message = "Soft-delete the deed first, then hard-delete from Restore." });
        }

        await RemoveDocumentAsync(document, cancellationToken);
        return Ok(new { message = "Permanently deleted." });
    }

    [HttpPost("purge-deleted")]
    public async Task<ActionResult<PurgeDeletedResponse>> PurgeDeleted(
        [FromQuery] Guid? clientId,
        CancellationToken cancellationToken)
    {
        var query = db.Documents.IgnoreQueryFilters().Where(x => x.DeletedAt != null);
        if (clientId is not null)
        {
            query = query.Where(x => x.ClientId == clientId);
        }

        var docs = await query.ToListAsync(cancellationToken);
        foreach (var document in docs)
        {
            await RemoveDocumentAsync(document, cancellationToken);
        }

        return new PurgeDeletedResponse(docs.Count, $"Permanently deleted {docs.Count} soft-deleted deed(s).");
    }

    private async Task RemoveDocumentAsync(DeedAi.Domain.Entities.Document document, CancellationToken cancellationToken)
    {
        var links = await db.DocumentLinks
            .Where(x => x.SourceDocumentId == document.Id || x.TargetDocumentId == document.Id)
            .ToListAsync(cancellationToken);
        db.DocumentLinks.RemoveRange(links);
        db.Documents.Remove(document);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            await blobs.DeleteAsync(document.BlobPath, cancellationToken);
            if (!string.IsNullOrWhiteSpace(document.DiRawBlobPath))
            {
                await blobs.DeleteAsync(document.DiRawBlobPath, cancellationToken);
            }
        }
        catch (IOException)
        {
            // blob already gone
        }
    }

    private static DocumentListItem ToListItem(DeedAi.Domain.Entities.Document x) =>
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
            x.Flags.Select(f => new FlagSummary(f.FlagDefinitionId, f.Flag.Name, f.Flag.Color)).ToList(),
            x.ErrorMessage);
}
