using DeedAi.Api.Contracts;
using DeedAi.Domain;
using DeedAi.Domain.Entities;

namespace DeedAi.Api;

public static class DocumentListMapping
{
    public static DocumentListItem ToListItem(Document document) =>
        new(
            document.Id,
            document.Name,
            document.Client.Name,
            document.ClientId,
            document.Status,
            document.UpdatedAt,
            document.Assignee?.DisplayName,
            document.AssigneeUserId,
            document.Status == DocumentStatuses.Failed,
            document.DeletedAt != null,
            document.DeedType,
            document.ReviewStatus,
            document.Flags.Select(flag => new FlagSummary(flag.FlagDefinitionId, flag.Flag.Name, flag.Flag.Color)).ToList(),
            document.ErrorMessage,
            ReviewWorkflow.DisplayStatus(
                document.Status,
                document.ReviewStatus,
                ReviewWorkflow.HasNeedsReviewFlag(document.Flags.Select(flag => (flag.FlagDefinitionId, flag.Flag?.Name)))),
            document.Volume,
            document.Page,
            document.DocumentNumber,
            document.Pid ?? document.Fields?.ParcelId,
            document.MailingStreet,
            document.MailingCity,
            document.MailingState,
            document.MailingZip,
            PartyNames.Normalize(document.Grantors, document.Fields?.Grantor),
            PartyNames.Normalize(document.Grantees, document.Fields?.Grantee));
}
