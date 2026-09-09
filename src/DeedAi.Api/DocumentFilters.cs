using DeedAi.Domain;
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
        DateTimeOffset? to,
        string? deedType = null)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.Name.Contains(search)
                || x.Status.Contains(search)
                || (x.Assignee != null && x.Assignee.DisplayName.Contains(search))
                || (x.DocumentNumber != null && x.DocumentNumber.Contains(search))
                || (x.Volume != null && x.Volume.Contains(search))
                || (x.Page != null && x.Page.Contains(search))
                || (x.Pid != null && x.Pid.Contains(search))
                || (x.DeedType != null && x.DeedType.Contains(search))
                || (x.MailingStreet != null && x.MailingStreet.Contains(search))
                || (x.MailingCity != null && x.MailingCity.Contains(search))
                || (x.MailingState != null && x.MailingState.Contains(search))
                || (x.MailingZip != null && x.MailingZip.Contains(search))
                || (x.Fields != null && (
                    (x.Fields.Grantor != null && x.Fields.Grantor.Contains(search))
                    || (x.Fields.Grantee != null && x.Fields.Grantee.Contains(search))
                    || (x.Fields.ParcelId != null && x.Fields.ParcelId.Contains(search))
                    || (x.Fields.LegalDescription != null && x.Fields.LegalDescription.Contains(search))
                    || (x.Fields.Notes != null && x.Fields.Notes.Contains(search)))));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = StatusCatalog.ApplyFilter(query, status);
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

        if (!string.IsNullOrWhiteSpace(deedType))
        {
            query = query.Where(x => x.DeedType == deedType);
        }

        return query;
    }

    public static IReadOnlyList<Document> ApplySearch(IEnumerable<Document> rows, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return rows as IReadOnlyList<Document> ?? rows.ToList();
        }

        return rows.Where(x => MatchesSearch(x, search)).ToList();
    }

    public static bool MatchesSearch(Document document, string search)
    {
        return Contains(document.Name, search)
               || Contains(document.Status, search)
               || Contains(document.Assignee?.DisplayName, search)
               || Contains(document.DocumentNumber, search)
               || Contains(document.Volume, search)
               || Contains(document.Page, search)
               || Contains(document.Pid, search)
               || Contains(document.DeedType, search)
               || Contains(document.MailingStreet, search)
               || Contains(document.MailingCity, search)
               || Contains(document.MailingState, search)
               || Contains(document.MailingZip, search)
               || PartyNames.Normalize(document.Grantors, document.Fields?.Grantor).Any(name => Contains(name, search))
               || PartyNames.Normalize(document.Grantees, document.Fields?.Grantee).Any(name => Contains(name, search))
               || Contains(document.Fields?.ParcelId, search)
               || Contains(document.Fields?.LegalDescription, search)
               || Contains(document.Fields?.Notes, search);
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

    private static bool Contains(string? value, string search) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains(search, StringComparison.OrdinalIgnoreCase);
}
