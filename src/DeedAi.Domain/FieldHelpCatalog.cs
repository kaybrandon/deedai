namespace DeedAi.Domain;

/// <summary>
/// Static field Help copy (not Admin-editable). Client / Software wording only.
/// </summary>
public static class FieldHelpCatalog
{
    public const string UploadClient = "upload.client";
    public const string SoftwareEnablePush = "software.enablePush";
    public const string SoftwareVendor = "software.vendor";
    public const string SoftwareGroupCode = "software.groupCode";
    public const string SoftwareRemoveLeadingZeros = "software.removeLeadingZeros";
    public const string SoftwareDisplaySalesTab = "software.displaySalesTab";
    public const string SoftwareSendConsideration = "software.sendConsideration";
    public const string SoftwareDateLabelDepth = "software.dateLabelDepth";
    public const string SoftwareImageCodes = "software.imageCodes";
    public const string SoftwareGrantee = "software.grantee";
    public const string SoftwareCertifiedYear = "software.certifiedYear";
    public const string SoftwareDefaultYear = "software.defaultYear";
    public const string SalesCodes = "sales.codes";
    public const string SalesConsiderationThreshold = "sales.considerationThreshold";
    public const string ReportsDate = "reports.date";
    public const string ReportsAssignee = "reports.assignee";
    public const string ReportsStatus = "reports.status";
    public const string ReportsFlag = "reports.flag";
    public const string ReportsClient = "reports.client";
    public const string SettingsFlags = "settings.flags";
    public const string SettingsStatuses = "settings.statuses";
    public const string SettingsDeedTypeMaps = "settings.deedTypeMaps";
    public const string RestoreConfirmRestore = "restore.confirmRestore";
    public const string RestoreConfirmHardDelete = "restore.confirmHardDelete";
    public const string SettingsSwagger = "settings.swagger";
    public const string SettingsSystemHealth = "settings.systemHealth";
    public const string UsersRole = "users.role";
    public const string SettingsIdleTimeout = "settings.idleTimeout";
    public const string SettingsDeletePolicy = "settings.deletePolicy";
    public const string SettingsOcrTrim = "settings.ocrTrim";
    public const string SettingsEmailMode = "settings.emailMode";
    public const string SettingsEmailTest = "settings.emailTest";
    public const string SettingsVerifyRequired = "settings.verifyRequired";
    public const string UsersResendVerification = "users.resendVerification";
    public const string ReviewGrantors = "review.grantors";
    public const string ReviewGrantees = "review.grantees";
    public const string ReviewDocumentNumber = "review.documentNumber";
    public const string ReviewVolume = "review.volume";
    public const string ReviewPage = "review.page";
    public const string ReviewDeedType = "review.deedType";
    public const string ReviewPid = "review.pid";
    public const string ReviewMailing = "review.mailing";
    public const string ReviewSoftwareSearch = "review.softwareSearch";

    public static readonly IReadOnlyList<string> MustKeys =
    [
        UploadClient,
        SoftwareEnablePush,
        SoftwareVendor,
        SoftwareGroupCode,
        SoftwareRemoveLeadingZeros,
        SoftwareDisplaySalesTab,
        SoftwareSendConsideration,
        SoftwareDateLabelDepth,
        SoftwareImageCodes,
        SoftwareGrantee,
        SoftwareCertifiedYear,
        SoftwareDefaultYear,
        SalesCodes,
        SalesConsiderationThreshold,
        ReportsDate,
        ReportsAssignee,
        ReportsStatus,
        ReportsFlag,
        ReportsClient,
        SettingsFlags,
        SettingsStatuses,
        SettingsDeedTypeMaps,
        RestoreConfirmRestore,
        RestoreConfirmHardDelete,
        SettingsSwagger,
        SettingsEmailMode,
        SettingsEmailTest,
        SettingsVerifyRequired,
        ReviewGrantors,
        ReviewMailing,
        ReviewSoftwareSearch
    ];

    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
    {
        [UploadClient] = "Choose the Client this PDF belongs to. The deed stays scoped to that Client for review, reports, and Software push.",
        [SoftwareEnablePush] = "When on, Editors can push reviewed deeds to the Software system. When off, lookup still works but push is blocked.",
        [SoftwareVendor] = "The Software vendor name for this Client. It is an operator label only — the API key stays in Key Vault.",
        [SoftwareGroupCode] = "Software group code sent with lookup and push for this Client. Leave blank to use the default Software group.",
        [SoftwareRemoveLeadingZeros] = "When on, leading zeros are stripped from parcel IDs before Software lookup and push.",
        [SoftwareDisplaySalesTab] = "When on, the Sales page lists this Client’s deeds that meet the consideration threshold so Editors can assign a Sales Tab code.",
        [SoftwareSendConsideration] = "When on, the consideration amount is included on Software push. When off, consideration is omitted from the payload.",
        [SoftwareDateLabelDepth] = "How many mapped dates and labels to send on push: instrument only, plus updated, or plus created.",
        [SoftwareImageCodes] = "Image codes this Client sends on Software lookup and push. Add codes here; they stay scoped to this Software instance.",
        [SoftwareGrantee] = "How multiple Grantee names combine for Software lookup and push. First, last, or joined. The label is Grantee.",
        [SoftwareCertifiedYear] = "Certified Software year used on lookup and push when a year is required. Leave blank when the vendor does not use years.",
        [SoftwareDefaultYear] = "Default Software year sent on lookup and push when a year is not taken from the deed. Must be a sensible year.",
        [SalesCodes] = "Sales Tab codes Editors can assign when Display Sales Tab is on and consideration meets the Client threshold.",
        [SalesConsiderationThreshold] = "Deeds at or above this amount appear on the Sales page when Display Sales Tab is on for the Client.",
        [ReportsDate] = "Limit the report to deeds updated in this date range. Leave both dates blank to include every date you can see.",
        [ReportsAssignee] = "Show only deeds assigned to this person. Clear the filter to include unassigned deeds and all assignees.",
        [ReportsStatus] = "Filter by pipeline status (Queued, Processing, Ready, Failed) or Needs review. Combine with Client, flag, or assignee as needed.",
        [ReportsFlag] = "Show deeds that have this review flag. Flags are defined by an Admin in Settings.",
        [ReportsClient] = "Limit the report to one Client. You only see Clients your account can access.",
        [SettingsFlags] = "Review flags the team can apply on a deed. Name and color appear on Documents and Reports.",
        [SettingsStatuses] = "Client/Software catalog statuses used on Documents and Review. Seed statuses can be renamed or disabled. System OCR statuses stay on the ribbon.",
        [SettingsDeedTypeMaps] = "Map each deed type to the Software code used on lookup and push.",
        [RestoreConfirmRestore] = "Restore puts this soft-deleted deed back on Documents for its Client. Review history stays attached.",
        [RestoreConfirmHardDelete] = "Hard-delete permanently removes the deed and its PDF blob. This cannot be undone — use Restore if you only meant to bring it back.",
        [SettingsSwagger] = "When on, /swagger serves the API UI so Admins can authorize with a JWT. When off, /swagger returns 404. Enabling Swagger does not open anonymous API access.",
        [SettingsSystemHealth] = "Admin-only SQL, Storage, Queue, Blob read/write, Document Intelligence, and OCR pipeline checks plus queue depth. Modes and counts only — never connection strings, keys, or other secrets.",
        [UsersRole] = "Admin manages users and Settings. Editor reviews and pushes. Uploader adds PDFs. Viewer reads assigned Clients only.",
        [SettingsIdleTimeout] = "After this many idle minutes the SPA signs you out and returns to login. Unsaved draft field edits are not silently wiped.",
        [SettingsDeletePolicy] = "Choose who may soft-delete documents: All Editors, or Admin only. Restore stays Admin-only. Uploader and Viewer never delete.",
        [SettingsOcrTrim] = "Trim strips listed characters from extracted text. Discard drops listed words. Never put secrets in this list.",
        [SettingsEmailMode] = "Active mail mode is SendGrid or SMTP — only one sends. API keys, SMTP username, and SMTP password stay in Key Vault.",
        [SettingsEmailTest] = "Sends one test message to the address you type using the active mode. Result is Pass or Fail. Secrets are never shown.",
        [SettingsVerifyRequired] = "When on, unverified users cannot sign in. Disabled accounts stay blocked even after they verify.",
        [UsersResendVerification] = "Sends a new verification link to this user through the active mail mode. Disabled accounts still cannot sign in.",
        [ReviewGrantors] = "Add each grantor on its own row. Empty rows cannot be saved. Order is kept when you save the deed.",
        [ReviewGrantees] = "Add each grantee on its own row. Empty rows cannot be saved. Order is kept when you save the deed.",
        [ReviewDocumentNumber] = "Recorder document number saved with this deed and shown on Documents.",
        [ReviewVolume] = "Book or volume reference from the instrument, saved with this deed.",
        [ReviewPage] = "Page reference from the instrument, saved with this deed.",
        [ReviewDeedType] = "Deed type used on review and Software push for this Client.",
        [ReviewPid] = "Property identifier used for Software lookup and push. Reused as the Documents PID column.",
        [ReviewMailing] = "Mailing street, city, state, and ZIP saved with this deed for Software and Documents search.",
        [ReviewSoftwareSearch] = "Search Software for this Client by parcel ID or owner, then apply a result to fill PID, owner, and mailing."
    };
}
