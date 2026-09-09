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

    public static readonly string[] All =
    [
        Grantor, Grantee, InstrumentDate, Consideration, ParcelId, LegalDescription, Client, Notes
    ];
}
