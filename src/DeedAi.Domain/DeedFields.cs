namespace DeedAi.Domain;

public static class DeedFields
{
    public const string Grantor = "grantor";
    public const string Grantee = "grantee";
    public const string InstrumentDate = "instrumentDate";
    public const string Consideration = "consideration";
    public const string ParcelId = "parcelId";
    public const string LegalDescription = "legalDescription";
    public const string Client = "client";
    public const string Notes = "notes";
    public const string DocumentNumber = "documentNumber";
    public const string Volume = "volume";
    public const string Page = "page";
    public const string DeedType = "deedType";
    public const string Pid = "pid";
    public const string MailingStreet = "mailingStreet";
    public const string MailingCity = "mailingCity";
    public const string MailingState = "mailingState";
    public const string MailingZip = "mailingZip";
    public const string ImageCode = "imageCode";
    public const string CertifiedYear = "certifiedYear";
    public const string DefaultYear = "defaultYear";
    public const string SalesRatioCode = "salesRatioCode";
    public const string FinanceCode = "financeCode";
    public const string InstrumentCode = "instrumentCode";

    public static readonly string[] All =
    [
        Grantor, Grantee, InstrumentDate, Consideration, ParcelId, LegalDescription, Client, Notes,
        DocumentNumber, Volume, Page, DeedType, Pid,
        MailingStreet, MailingCity, MailingState, MailingZip,
        ImageCode, CertifiedYear, DefaultYear, SalesRatioCode, FinanceCode, InstrumentCode
    ];

    public static bool IsKnown(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && All.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
}
