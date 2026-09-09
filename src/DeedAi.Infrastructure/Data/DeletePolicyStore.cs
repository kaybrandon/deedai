using DeedAi.Domain;
using DeedAi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeedAi.Infrastructure.Data;

public static class DeletePolicyStore
{
    public static async Task<DeletePolicySettings> EnsureAsync(DeedAiDbContext db, CancellationToken cancellationToken)
    {
        var item = await db.DeletePolicySettings.FirstOrDefaultAsync(cancellationToken);
        if (item is not null)
        {
            item.CoalesceNulls();
            return item;
        }

        item = new DeletePolicySettings
        {
            Id = DeletePolicySettings.SingletonId,
            WhoCanDelete = DeletePolicy.AllEditors,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.DeletePolicySettings.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }
}
