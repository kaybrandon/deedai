using DeedAi.Domain.Abstractions;

namespace DeedAi.Infrastructure.Software;

public sealed class MockSoftwareClient : ISoftwareClient
{
    public SoftwarePushRequest? LastPush { get; private set; }

    public Task<SoftwareLookupResult?> LookupAsync(SoftwareLookupQuery query, CancellationToken cancellationToken)
    {
        if (!HasKeyField(query))
        {
            return Task.FromResult<SoftwareLookupResult?>(null);
        }

        if (ContainsMissing(query.ParcelId) || ContainsMissing(query.Grantor) || ContainsMissing(query.Grantee))
        {
            return Task.FromResult<SoftwareLookupResult?>(null);
        }

        var parcel = string.IsNullOrWhiteSpace(query.ParcelId) ? InferParcel(query) : query.ParcelId.Trim();
        return Task.FromResult<SoftwareLookupResult?>(new SoftwareLookupResult(
            parcel,
            string.IsNullOrWhiteSpace(query.Grantor) ? "Jane Example" : query.Grantor.Trim(),
            "Lot 4 Block 2 of North Addition",
            "100 Main St",
            $"SW-{parcel}",
            new Dictionary<string, string>
            {
                ["client"] = query.Client ?? "Acme",
                ["grantor"] = query.Grantor ?? "Jane Example",
                ["grantee"] = query.Grantee ?? "",
                ["source"] = "mock"
            }));
    }

    public Task<SoftwarePushResult> PushAsync(SoftwarePushRequest request, CancellationToken cancellationToken)
    {
        LastPush = request;
        if (string.IsNullOrWhiteSpace(request.ParcelId)
            && string.IsNullOrWhiteSpace(request.Grantor)
            && string.IsNullOrWhiteSpace(request.Grantee))
        {
            return Task.FromResult(new SoftwarePushResult(false, null, "Parcel ID, grantor, or grantee is required to push to Software."));
        }

        if (ContainsMissing(request.ParcelId))
        {
            return Task.FromResult(new SoftwarePushResult(false, null, "Software rejected this parcel. Check the key fields and retry."));
        }

        var key = request.ParcelId ?? request.Grantor ?? request.DocumentId.ToString("N")[..8];
        return Task.FromResult(new SoftwarePushResult(
            true,
            request.SoftwareCode is null ? $"SW-{key}" : $"{request.SoftwareCode}-{key}",
            "Pushed to Software (mock)."));
    }

    private static bool HasKeyField(SoftwareLookupQuery query) =>
        !string.IsNullOrWhiteSpace(query.ParcelId)
        || !string.IsNullOrWhiteSpace(query.Grantor)
        || !string.IsNullOrWhiteSpace(query.Grantee)
        || !string.IsNullOrWhiteSpace(query.Client)
        || !string.IsNullOrWhiteSpace(query.InstrumentDate);

    private static bool ContainsMissing(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains("missing", StringComparison.OrdinalIgnoreCase);

    private static string InferParcel(SoftwareLookupQuery query) =>
        !string.IsNullOrWhiteSpace(query.Grantor) ? $"GRANTOR-{Slug(query.Grantor)}" : "KEY-FIELDS";

    private static string Slug(string value) =>
        new(value.Where(char.IsLetterOrDigit).Take(12).ToArray());
}
