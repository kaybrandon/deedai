# Phase 5.2.2 — Manage Documents (rich list)

Visual SoT: Mask F Documents table — hug rows, sticky header, one ≤360px search.

## Must

1. **One filter-row search** ≤360px (no top-bar duplicate) across geo/legal (mailing + existing legal description) · grantors/grantees · Volume · Page · Document Number · PID · plus existing name/status/assignee.
2. **Columns sortable:** Status · Client · Volume · Page · Type · PID · Doc # · Assignee · Updated.
3. **Column-header sort** with `aria-sort`.
4. **Filters:** status · date · Client · assignee · type; ≥44px.
5. **Hug rows · sticky header · page/virtualize · Mask F density** (`--table-row-h`, `--table-cell-pad-y`, `--table-header-bg`, `--table-stripe`).
6. **Carry:** OCR ribbon · Retry Failed · ConfirmSheet soft-delete · chart-click deep links · Client/Software only · no County/CAMA.

## Shared persisted / API names (locked with 5.2.1)

camelCase JSON; EF/C# PascalCase. Do not invent aliases (`docNo`, `vol`, CAMA names).

| UI | Persisted / API |
| --- | --- |
| Doc # | `documentNumber` |
| Volume | `volume` |
| Page | `page` |
| Type | `deedType` |
| PID | `pid` |
| Geo / mailing | `mailingStreet` · `mailingCity` · `mailingState` · `mailingZip` |
| Parties | `grantors[]` · `grantees[]` |

Legacy `DocumentFields.Grantor` / `Grantee` / `ParcelId` remain for OCR and current Review strings. List search reads `grantors[]` / `grantees[]` and falls back to those strings until 5.2.1 multi-party Review merges.

## Should

- URL + `sessionStorage` persist sort/filter.
- Empty: **No Documents Yet**. Filtered empty: **No Documents Match**.
- Paginate around 50 rows.

## Won’t

- CSV export · Super Admin · Flags/Statuses/Delete policy · 5.2.1 Review multi-party UI · glass/gradients.

## Fail if

- Card-only list · no search or duplicate search · required columns not sortable · County/CAMA · invented field aliases · Azure deploy.
