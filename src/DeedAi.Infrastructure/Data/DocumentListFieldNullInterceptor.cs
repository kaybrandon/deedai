using DeedAi.Domain.Entities;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DeedAi.Infrastructure.Data;

/// <summary>
/// After a row is read, replace NULL list fields with empty values so callers
/// never see the PR #35 SqlNullValueException path or a null Grantors list.
/// SQL Server still must map those columns as nullable strings (see converters).
/// </summary>
internal sealed class DocumentListFieldNullInterceptor : IMaterializationInterceptor
{
    public static readonly DocumentListFieldNullInterceptor Instance = new();

    public object InitializedInstance(MaterializationInterceptionData materializationData, object entity)
    {
        if (entity is Document document)
        {
            document.CoalesceNullListFields();
        }

        if (entity is DeletePolicySettings policy)
        {
            policy.CoalesceNulls();
        }

        if (entity is StatusDefinition status)
        {
            status.CoalesceNullCatalogFields();
        }

        return entity;
    }
}
