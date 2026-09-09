using DeedAi.Domain.Abstractions;

namespace DeedAi.Infrastructure.Software;

public sealed class MockSoftwareClient : ISoftwareClient
{
    public Task<SoftwareLookupResult?> LookupAsync(string parcelId, string? clientName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(parcelId))
        {
            return Task.FromResult<SoftwareLookupResult?>(null);
        }

        if (parcelId.Contains("missing", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<SoftwareLookupResult?>(null);
        }

        return Task.FromResult<SoftwareLookupResult?>(new SoftwareLookupResult(
            parcelId,
            "Jane Example",
            "Lot 4 Block 2 of North Addition",
            "100 Main St",
            $"SW-{parcelId}",
            new Dictionary<string, string>
            {
                ["client"] = clientName ?? "Acme",
                ["source"] = "mock"
            }));
    }

    public Task<SoftwarePushResult> PushAsync(SoftwarePushRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ParcelId))
        {
            return Task.FromResult(new SoftwarePushResult(false, null, "Parcel ID is required to push to Software."));
        }

        return Task.FromResult(new SoftwarePushResult(
            true,
            request.SoftwareCode is null ? $"SW-{request.ParcelId}" : $"{request.SoftwareCode}-{request.ParcelId}",
            "Pushed to Software (mock)."));
    }
}
