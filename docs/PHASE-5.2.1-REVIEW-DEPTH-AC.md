# Phase 5.2.1 — Review field depth

Visual SoT: Mask F Review (`04-review.png`) — 3-col queue | PDF | fields. Denser fields, not a new theme.

## Must

1. **Multi grantor/grantee rows:** add/remove; persist order; empty-row validation; ≥44px; ConfirmSheet on remove if the row has data.
2. **Document Number · Volume · Page · Deed Type** in the Review fields pane and saved with the document.
3. **Mailing:** street · city · state · ZIP. Label **Mailing**, never CAMA.
4. **Software search** on Review (Client-scoped) fills key property fields; ≥44px results; reuse existing Software lookup.
5. **Role-gated Push to Software** from Review (same gates as the Software page); ConfirmSheet before Push; success/fail inline; no secrets.
6. **Carry:** 3-col queue | PDF | fields · Failed + Incomplete never Ready + OCR-failed · Retry Extract · Mask F density · Title Case labels · helpers sentence case · PDF page or clear empty.

## Shared persisted / API names (locked with 5.2.2)

camelCase JSON; EF/C# PascalCase. Do not invent aliases (`docNo`, `vol`, CAMA names). Reuse `20260909220000_DocumentListFields` columns and the #36 `20260909230000_DocumentListFieldNullDefaults` hotfix. No new migration.

| UI | Persisted / API |
| --- | --- |
| Doc # | `documentNumber` |
| Volume | `volume` |
| Page | `page` |
| Type | `deedType` |
| PID | `pid` |
| Geo / mailing | `mailingStreet` · `mailingCity` · `mailingState` · `mailingZip` |
| Parties | `grantors[]` · `grantees[]` |

Legacy `DocumentFields.Grantor` / `Grantee` / `ParcelId` stay in sync with the first party / PID for OCR and older clients.

## Won’t

AI extract · County/CAMA · Statuses/Documents/Software-settings slices · Super Admin · Property defaults · Azure deploy.

## Fail if

- Single grantor/grantee strings only · invented field aliases · County/CAMA · Ready chip + OCR-failed banner · secrets on Review · no ConfirmSheet before Push.
