using DeedAi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DeedAi.Infrastructure.Data;

/// <summary>
/// SQL Server rejects multiple cascade paths from Documents → Users.
/// Assignee may SetNull; UploadedBy must be NoAction/Restrict.
/// </summary>
public static class DocumentUserFkRules
{
    public static IReadOnlyList<string> Validate(IModel model)
    {
        var errors = new List<string>();
        var document = model.FindEntityType(typeof(Document));
        if (document is null)
        {
            errors.Add("Document entity is missing from the model.");
            return errors;
        }

        var userFks = document.GetForeignKeys()
            .Where(fk => fk.PrincipalEntityType.ClrType == typeof(UserAccount))
            .ToList();

        var cascadeOrSetNull = userFks
            .Where(fk => fk.DeleteBehavior is DeleteBehavior.Cascade or DeleteBehavior.ClientCascade or DeleteBehavior.SetNull)
            .ToList();
        if (cascadeOrSetNull.Count > 1)
        {
            errors.Add("Documents→Users has more than one Cascade/SetNull FK; SQL Server rejects multiple cascade paths.");
        }

        var uploadedBy = document.FindNavigation(nameof(Document.UploadedBy))?.ForeignKey;
        if (uploadedBy is null)
        {
            errors.Add("UploadedBy FK is missing.");
        }
        else if (uploadedBy.DeleteBehavior is not (DeleteBehavior.NoAction or DeleteBehavior.Restrict))
        {
            errors.Add($"UploadedBy must be NoAction/Restrict, was {uploadedBy.DeleteBehavior}.");
        }

        var assignee = document.FindNavigation(nameof(Document.Assignee))?.ForeignKey;
        if (assignee is null)
        {
            errors.Add("Assignee FK is missing.");
        }

        return errors;
    }
}
