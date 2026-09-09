using DeedAi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeedAi.Api;

public static class DocumentFilters
{
    public static IQueryable<Document> Apply(
        IQueryable<Document> query,
        string? search,
        string? status,
        Guid? clientId,
        Guid? assigneeUserId,
        Guid? flagId,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.Name.Contains(search)
                || (x.Fields != null && (x.Fields.Grantor!.Contains(search) || x.Fields.ParcelId!.Contains(search))));
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

        return query;
    }

    public static IReadOnlyList<Document> ApplyDates(
        IEnumerable<Document> rows,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        var filtered = rows;
        if (from is not null)
        {
            filtered = filtered.Where(x => x.CreatedAt >= from);
        }

        if (to is not null)
        {
            filtered = filtered.Where(x => x.CreatedAt <= to);
        }

        return filtered.ToList();
    }

    public static IQueryable<Document> WithReportIncludes(IQueryable<Document> query) =>
        query
            .Include(x => x.Client)
            .Include(x => x.Assignee)
            .Include(x => x.Fields)
            .Include(x => x.Flags).ThenInclude(x => x.Flag);
}
