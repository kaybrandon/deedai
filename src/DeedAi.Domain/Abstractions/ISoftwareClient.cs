namespace DeedAi.Domain.Abstractions;

public sealed record SoftwareLookupQuery(
    string? ParcelId,
    string? Grantor,
    string? Grantee,
    string? Client,
    string? InstrumentDate,
    string? DeedType,
    int? Year = null,
    string? ImageCode = null);

public sealed record SoftwareLookupResult(
    string ParcelId,
    string? Owner,
    string? LegalDescription,
    string? Address,
    string? SoftwareRecordId,
    IReadOnlyDictionary<string, string> Extra);

public sealed record SoftwarePushRequest(
    Guid DocumentId,
    string? ParcelId,
    string? DeedType,
    string? SoftwareCode,
    string? Grantor,
    string? Grantee,
    string? InstrumentDate,
    string? Consideration,
    string? Client,
    string? Notes,
    IReadOnlyDictionary<string, string> MappedFields);

public sealed record SoftwarePushResult(bool Succeeded, string? SoftwareRecordId, string Message);

public interface ISoftwareClient
{
    Task<SoftwareLookupResult?> LookupAsync(SoftwareLookupQuery query, CancellationToken cancellationToken);
    Task<SoftwarePushResult> PushAsync(SoftwarePushRequest request, CancellationToken cancellationToken);
}
