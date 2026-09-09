export const HELP = {
  "upload.client":
    "Choose the Client this PDF belongs to. The deed stays scoped to that Client for review, reports, and Software push.",
  "software.enablePush":
    "When on, Editors can push reviewed deeds to the Software system. When off, lookup still works but push is blocked.",
  "software.vendor":
    "The Software vendor name for this Client. It is an operator label only — the API key stays in Key Vault.",
  "software.groupCode":
    "Software group code sent with lookup and push for this Client. Leave blank to use the default Software group.",
  "software.removeLeadingZeros":
    "When on, leading zeros are stripped from parcel IDs before Software lookup and push.",
  "software.displaySalesTab":
    "When on, the Sales page lists this Client’s deeds that meet the consideration threshold so Editors can assign a Sales Tab code.",
  "software.sendConsideration":
    "When on, the consideration amount is included on Software push. When off, consideration is omitted from the payload.",
  "software.dateLabelDepth":
    "How many mapped dates and labels to send on push: instrument only, plus updated, or plus created.",
  "software.imageCodes":
    "Image codes this Client sends on Software lookup and push. Add codes here; they stay scoped to this Software instance.",
  "software.grantee":
    "How multiple Grantee names combine for Software lookup and push. First, last, or joined. The label is Grantee.",
  "software.certifiedYear":
    "Certified Software year used on lookup and push when a year is required. Leave blank when the vendor does not use years.",
  "software.defaultYear":
    "Default Software year sent on lookup and push when a year is not taken from the deed. Must be a sensible year.",
  "sales.codes":
    "Sales Tab codes Editors can assign when Display Sales Tab is on and consideration meets the Client threshold.",
  "sales.considerationThreshold":
    "Deeds at or above this amount appear on the Sales page when Display Sales Tab is on for the Client.",
  "reports.date":
    "Limit the report to deeds updated in this date range. Leave both dates blank to include every date you can see.",
  "reports.assignee": "Show only deeds assigned to this person. Clear the filter to include unassigned deeds and all assignees.",
  "reports.status":
    "Filter by pipeline status (Queued, Processing, Ready, Failed) or Needs review. Combine with Client, flag, or assignee as needed.",
  "reports.flag": "Show deeds that have this review flag. Flags are defined by an Admin in Settings.",
  "reports.client": "Limit the report to one Client. You only see Clients your account can access.",
  "settings.flags": "Review flags the team can apply on a deed. Name and color appear on Documents and Reports.",
  "settings.statuses": "Pipeline and review statuses used in filters and reports. System statuses cannot be deleted.",
  "settings.deedTypeMaps": "Map each deed type to the Software code used on lookup and push.",
  "restore.confirmRestore":
    "Restore puts this soft-deleted deed back on Documents for its Client. Review history stays attached.",
  "restore.confirmHardDelete":
    "Hard-delete permanently removes the deed and its PDF blob. This cannot be undone — use Restore if you only meant to bring it back.",
  "settings.swagger":
    "When on, /swagger serves the API UI so Admins can authorize with a JWT. When off, /swagger returns 404. Enabling Swagger does not open anonymous API access.",
  "settings.systemHealth":
    "Admin-only SQL, Storage, Queue, Blob read/write, Document Intelligence, and OCR pipeline checks plus queue depth. Modes and counts only — never connection strings, keys, or other secrets.",
  "users.role": "Admin manages users and Settings. Editor reviews and pushes. Uploader adds PDFs. Viewer reads assigned Clients only.",
  "settings.idleTimeout":
    "After this many idle minutes the SPA signs you out and returns to login. Unsaved draft field edits are not silently wiped.",
  "settings.deletePolicy":
    "Choose who may soft-delete documents: All Editors, or Admin only. Restore stays Admin-only. Uploader and Viewer never delete.",
  "settings.ocrTrim": "Trim strips listed characters from extracted text. Discard drops listed words. Never put secrets in this list.",
  "settings.emailMode":
    "Active mail mode is SendGrid or SMTP — only one sends. API keys, SMTP username, and SMTP password stay in Key Vault.",
  "settings.emailTest":
    "Sends one test message to the address you type using the active mode. Result is Pass or Fail. Secrets are never shown.",
  "settings.verifyRequired":
    "When on, unverified users cannot sign in. Disabled accounts stay blocked even after they verify.",
  "users.resendVerification":
    "Sends a new verification link to this user through the active mail mode. Disabled accounts still cannot sign in.",
  "review.grantors":
    "Add each grantor on its own row. Empty rows cannot be saved. Order is kept when you save the deed.",
  "review.grantees":
    "Add each grantee on its own row. Empty rows cannot be saved. Order is kept when you save the deed.",
  "review.documentNumber": "Recorder document number saved with this deed and shown on Documents.",
  "review.volume": "Book or volume reference from the instrument, saved with this deed.",
  "review.page": "Page reference from the instrument, saved with this deed.",
  "review.deedType": "Deed type used on review and Software push for this Client.",
  "review.pid": "Property identifier used for Software lookup and push. Reused as the Documents PID column.",
  "review.mailing":
    "Mailing street, city, state, and ZIP saved with this deed for Software and Documents search.",
  "review.softwareSearch":
    "Search Software for this Client by parcel ID or owner, then apply a result to fill PID, owner, and mailing."
} as const;

export type HelpKey = keyof typeof HELP;

export const MUST_HELP_KEYS: HelpKey[] = [
  "upload.client",
  "software.enablePush",
  "software.vendor",
  "software.groupCode",
  "software.removeLeadingZeros",
  "software.displaySalesTab",
  "software.sendConsideration",
  "software.dateLabelDepth",
  "software.imageCodes",
  "software.grantee",
  "software.certifiedYear",
  "software.defaultYear",
  "sales.codes",
  "sales.considerationThreshold",
  "reports.date",
  "reports.assignee",
  "reports.status",
  "reports.flag",
  "reports.client",
  "settings.flags",
  "settings.statuses",
  "settings.deedTypeMaps",
  "restore.confirmRestore",
  "restore.confirmHardDelete",
  "settings.swagger",
  "settings.emailMode",
  "settings.emailTest",
  "settings.verifyRequired",
  "review.grantors",
  "review.mailing",
  "review.softwareSearch"
];
