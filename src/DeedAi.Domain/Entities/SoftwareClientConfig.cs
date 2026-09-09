namespace DeedAi.Domain.Entities;

public sealed class SoftwareClientConfig
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public string? Vendor { get; set; }
    public string? ApiUrl { get; set; }
    public string? GroupCode { get; set; }
    public bool RemoveLeadingZeros { get; set; }
    public int DateLabelDepth { get; set; } = 1;
    public bool DisplaySalesTab { get; set; }
    public bool SendConsideration { get; set; } = true;
    public decimal ConsiderationThreshold { get; set; }
    public bool ResetExemptions { get; set; }
    public bool ResetSupplementYear { get; set; }
    public bool ResetSalesLetter { get; set; }
    public bool ResetSalesTab { get; set; }
    public bool ResetAgents { get; set; }
    public bool ResetMortgageCodes { get; set; }

    public bool HasAnyReset =>
        ResetExemptions || ResetSupplementYear || ResetSalesLetter || ResetSalesTab || ResetAgents || ResetMortgageCodes;
}
