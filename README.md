# Deed AI

BIS Consultants **Deed AI** — Phase 3. Naming in this product is **Client** (never County) and **Software** (never CAMA). Roles: **Admin**, **Editor**, **Uploader**, **Viewer**.

This repository replaces the README-only GitHub seed with a working Layout A application: a single .NET 10 API host serves the React/Vite SPA from `wwwroot` for Windows App Service `appdeedai`. Phase 1–3 are live there; see [AZURE-PROD-NOTE.md](docs/AZURE-PROD-NOTE.md) for deploy and the UploadedBy FK hotfix.

## Docs

Phase 1 operator and staff docs (no secrets). Start at [SOP.md](SOP.md) (root index).

- [SOP.md](SOP.md) — SOP index
- [AZURE-PROD-NOTE.md](docs/AZURE-PROD-NOTE.md) — live Azure ops note
- [AZURE-PREP.md](docs/AZURE-PREP.md) — historical Azure prep note
- [SOP-01-overview.md](docs/SOP-01-overview.md) — local / ops overview
- [SOP-02-azure-deploy.md](docs/SOP-02-azure-deploy.md) — Azure deploy checklist
- [SOP-04-staff-quickstart.md](docs/SOP-04-staff-quickstart.md) — staff field quick start
- [CHANGELOG.md](docs/CHANGELOG.md) — SOP changelog
- [PHASE-6-AI-EXTRACT-AC.md](docs/PHASE-6-AI-EXTRACT-AC.md) — Phase 6 AI extract
- [PHASE-5.2.3-SOFTWARE-DEPTH-AC.md](docs/PHASE-5.2.3-SOFTWARE-DEPTH-AC.md) — Phase 5.2.3 Software settings depth
- [PHASE-5.2.4-DELETE-POLICY-AC.md](docs/PHASE-5.2.4-DELETE-POLICY-AC.md) — Phase 5.2.4 Delete Policy
- Phase 1 wires: [login](docs/wires/01-login.png) · [dashboard](docs/wires/02-dashboard.png) · [documents](docs/wires/03-documents.png) · [upload](docs/wires/04-upload.png) · [review](docs/wires/05-review.png)

## Stack

| Piece | Choice |
| --- | --- |
| API | .NET 10 Web API (`src/DeedAi.Api`) |
| SPA | React + Vite + TypeScript (`spa/`) built into `src/DeedAi.Api/wwwroot` |
| OCR worker | .NET 10 worker on Azure Storage Queue `ocr-jobs` (long-poll, not a 1s loop) |
| Layout | **A** — one Windows App Service host serves API + SPA |
| SQL | Azure SQL `deedaihost01` / `dbdeedai` (SQLite for local/dev) |
| Storage | `stbisdeedai` container `deeds`; AI extract raw JSON stored in blob (`ai-raw/`) with a pointer |
| App Service | `appdeedai`, plan `asp-bis-deed-ai` B1, RG `rg-bis-deed-ai`, South Central US |

Azure OpenAI extract uses the **same subscription** as `appdeedai`. Key Vault names: `AzureOpenAIEndpoint`, `AzureOpenAIKey`, `AzureOpenAIDeployment`, optional `AzureOpenAIModel` (default `gpt-4o-mini`). Fail closed if unconfigured. Do not raise quotas. Escalate spend to Chief of Staff before a pricier model. Document Intelligence field-fill is **removed** — leftover `BISDocumentIntelligenceEndpoint` / `DocumentIntelligenceKey` are ignored.

## Phase 6 acceptance

- **AI PDF → locked Review fields:** Azure OpenAI fills `documentNumber`, `volume`, `page`, `deedType`, `pid`, `mailingStreet`, `mailingCity`, `mailingState`, `mailingZip`, `grantors[]`, `grantees[]`. Human edit loop stays on Review. Re-extract uses ConfirmSheet.
- **One path:** Document Intelligence field-fill is removed after cutover. No dual path and no feature flag that leaves both. Human edit / ConfirmSheet stay. Queue / worker / ribbon remain. Unconfigured Azure OpenAI fails closed (no silent mock in Azure).
- **KV model keys** on the same subscription. Default model `gpt-4o-mini`. Do not raise quotas. Escalate spend to CoS before a pricier model.
- **Carry:** Mask F Review · Client/Software · no CAMA · Designer-first `20260910040000_Phase6AiExtract` · health 200.
- **Should:** Confidence chips · batch re-extract · raw AI blob audit (`ai-raw/{id}.json`).
- **Won’t:** Azure deploy · County/CAMA · Super Admin · quota increase.

## Phase 5.2.5 acceptance

- **Statuses catalog (Admin):** Settings → System → Statuses lists and manages Client/Software statuses. Seeded if missing: **Complete** · **In Queue** · **Needs Work** · **New** · **Not Needed** · **Pending** · **Research** · **Upload Error**.
- **Map ↔ pipeline:** Each catalog entry maps to Queued / Processing / Ready / Failed or a review state (Needs Work → NeedsReview, Complete → Approved-like) or a documented extension (New, Research, Not Needed). OCR `Document.Status` and the ribbon stay Queued → Processing → Ready. Catalog assign never writes pipeline status.
- **Assign / filter:** Editors and Admins set catalog status on Documents and Review. List filters include catalog statuses. Chips use catalog display names (Title Case).
- **CRUD:** Admin add / rename / soft-disable. System and seed Must entries cannot be hard-deleted (disable OK). ConfirmSheet on disable/delete (≥44px).
- **Carry:** Client/Software only · no County/CAMA · Mask F · Designer-first `20260910030000_Phase525StatusesCatalog` · null-safe MapsTo / Kind / IsSeed (backfill + DEFAULT + coalesce) · health 200.
- **Should:** Sort order and Mask F color tokens. DocumentLog history is parked.
- **Won’t:** Super Admin · Flags catalog · Deed-type mapping table · replacing OCR with free-text-only statuses.

## Phase 5.2.4 acceptance

- **Delete Policy** (Settings → System): Admin persists who may soft-delete documents — **All Editors** or **Admin only**. Label **Delete Policy** / **Who Can Delete**. Never County / CAMA / Super Admin.
- **Enforce:** Soft-delete is hidden when the role is below the policy. Unauthorized API delete returns **403** (not 404). ConfirmSheet still required when allowed (≥44px). Uploader and Viewer never delete.
- **Restore:** Restore stays Admin-gated. Policy change does not alter restore or hard-delete.
- **Carry:** 4 roles · Client/Software · Mask F density · no secrets · Designer-first EF migration `20260910010000_DeletePolicy` · null-safe `WhoCanDelete` (DEFAULT AllEditors + NULL backfill + null-safe materialize) · health 200.
- **Should:** Audit last Admin who changed the policy. Editors see a read-only Delete Policy hint on Documents.
- **Won’t:** Super Admin matrix · hard-delete · County/CAMA · Statuses catalog (5.2.5) · Property defaults.

## Phase 5.2.3 acceptance

- **Image codes:** Admin view/edit Client-scoped image codes for Software lookup and push. Persist per Software instance.
- **Grantee combiner:** First / last / joined Grantee names on lookup and push. Label **Grantee**, never CAMA.
- **Certified / default year:** Persist on the Client Software instance, validate 1900–current+2, send on lookup/push when set.
- **Fuller field maps:** Typed maps include mailing, volume, page, document number, PID, legal, image code, and years — not only the original seven deed fields. No `cama*` API names.
- **Should:** Sales ratio / finance / instrument codes on the instance. Lookup sends year + image code (no Harris SOAP client).
- **Carry:** Six push resets · role-gated Push / lookup / retry / last-sync · Property defaults stay removed · Settings → System → Software · Mask F 680px form / full-width instances · no FieldHelp `?` pills · KV-only secrets.
- **Hard gates:** Designer-first migration `20260910020000_Phase523SoftwareDepth` (after `20260910010000_DeletePolicy`) with backfill + SQL defaults. No Azure deploy.

## Phase 5.2.1 acceptance

- **Review field depth:** Mask F 3-col queue | PDF | fields. Multi grantor/grantee rows (add/remove, persist order, empty-row validation, ≥44px, ConfirmSheet when the row has data).
- **Instrument block:** Document Number, Volume, Page, and Deed Type live in the fields pane and save on the document.
- **Mailing:** street, city, state, ZIP. Label **Mailing** — never CAMA.
- **Software:** Client-scoped search reuses existing lookup and fills PID / owner / mailing. Role-gated Push (same gates as Software) with ConfirmSheet; success/fail inline; no secrets.
- **Locked fields (shared with 5.2.2):** `documentNumber`, `volume`, `page`, `deedType`, `pid`, `mailingStreet`, `mailingCity`, `mailingState`, `mailingZip`, `grantors[]`, `grantees[]`. Never `docNo` / `vol` / County / CAMA.
- **Carry:** Failed + Incomplete never Ready + OCR-failed. Retry Extract. Title Case labels, sentence-case helpers. PDF preview or clear empty. Reuse `20260909220000_DocumentListFields` and the #36 NULL-default hotfix — no new EF migration.
- **Won’t:** AI extract, County/CAMA, Statuses/Documents/Software-settings slices, Super Admin, Property defaults, Azure deploy.

## Phase 5.2.2 acceptance

- **Documents table:** Mask F SoT density — hug rows, sticky header, zebra stripes, min 44px. One filter-row search ≤360px (no top-bar duplicate) across geo/legal (mailing + legal description), grantors/grantees, Volume, Page, Document Number, PID, name, status, and assignee.
- **Sort / filter:** Column headings sort Status · Client · Volume · Page · Type · PID · Doc # · Assignee · Updated with `aria-sort`. Filters on status, date, Client, assignee, and type.
- **Locked fields (shared with 5.2.1):** `documentNumber`, `volume`, `page`, `deedType`, `pid`, `mailingStreet`, `mailingCity`, `mailingState`, `mailingZip`, `grantors[]`, `grantees[]`. Never `docNo` / `vol` / County / CAMA.
- **Carry:** OCR ribbon, Retry Failed, ConfirmSheet soft-delete, chart-click deep links. Client / Software only.
- **Should:** URL + session persist. **No Documents Yet** / **No Documents Match**. Page size 50.
- **Won’t:** CSV, Super Admin, Flags/Statuses/Delete policy, 5.2.1 Review multi-party UI, glass/gradients, Azure deploy.
- **Hard gates:** EF migration `20260909220000_DocumentListFields` is Designer-first. No zipdeploy.

## Phase 5.1.1 acceptance

- **Users table:** Admin Users is a real data table (not a card stack). Mask F SoT density (`05-settings-users`): hug rows, sticky gray header, zebra stripes, role chips, single-line sort+filter headings, min 44px (`--table-row-h: 44px`).
- **Search:** One filter-row search ≤360px across display name, full name, and email. No second search in the top bar.
- **Sort / filter:** Column headings sort asc/desc with a visible affordance for Display Name, Email, Role, Client(s), and Status. Per-column filters on Role, Client, and Status (enabled/disabled).
- **Phase 4.5 carry:** Multi-Client users list every assignment in the Client(s) column. ConfirmSheet disable, add/edit panel, photo, and password stay. Admin-only. Client / Software only — never County / CAMA.
- **Should:** Sort/filter persist on the URL and in `sessionStorage`. Empty and no-match states. Paginate at 50 rows.
- **Won’t:** In-cell edit, CSV export, replacing add/edit, or relitigating photo/password. Volume weekly (#34).
- **Nav:** Settings → **System** (singular mid-parent from Mask F) → indented Software · Users · API. Never Systems plural. Never System as a sibling of Software.
- **Hard gates:** No new EF migration. No zipdeploy.

## Phase 5.1 acceptance

- **Mask F theme:** Mockitt Admin tokens — rail `#1E2430` · canvas `#F0F2F5` · surface `#FFFFFF` · text `#1A1F2A` · muted `#5C6573` · teal `#0D8A7F` · accent `#3B82F6` · Ready `#D8F0EA`/`#0B5F56` · Failed `#F5D6D3`/`#8B2E28`. Dark rail sitewide. Inter. Radii 5/8. Spacing 4/8/12/16.
- **Density:** Sidebar ~220px · top bar 48px · ribbon 42px · hit ≥44px · btn 32px · search max 360px · filters ~240px · forms ~32rem · Settings/Software detail ~680px · content max ~1440px. Software Instances table fills remaining width.
- **Software layout:** Client software form stays ~680px. Existing Client configs render as a full-width **Software Instances** table (no invented Environment / County / CAMA fields).
- **Documents:** exactly one search, in the filter row only (≤360px). No topbar search duplicate.
- **Settings nest:** **Settings** → **System** (mid-level nest parent, singular Title Case) → indented **Software** · **Users** · **API**. Not Software as Settings’ first child. Not System as a sibling of Software/Users/API. Never Systems / Workspace / County / CAMA. Parents are not double-highlighted with a leaf.
- **Help:** no FieldHelp `?` pills. Help is label `title` / `aria-describedby` only — not a second page Help widget.
- **Carry:** Phase 5.0.1 chart-click + Title Case. Client / Software naming · 4 roles · no secrets · no EF migration · no zipdeploy. Mask A stays on production until this ships.

## Phase 5.0.2 acceptance

- **Volume Over Time** (exact Title Case) is a **bar** chart — not line or area. One teal (`#0D8A7F`) **Uploaded** series per week; no stacked status series.
- X-axis buckets are **ISO weeks** (Monday–Sunday), not days. API `GET /api/dashboard/charts/volume` returns week-start labels plus `buckets[].from` / `buckets[].to`.
- Click a week bar or a ≥44px keyboard week link → Documents with `from`/`to` covering that week range. Client filter is preserved when set. An empty week shows the Documents empty state.
- Client / Software naming only. No secrets. No zipdeploy. No EF migration. Separate from Mask F.

## Phase 5.0 acceptance

- **Mask A theme (superseded by 5.1 in this tree):** Mist Slate + Teal tokens — bg `#F4F6F8`, sidebar `#E8EEF2`, accent `#4F7C8A`, text `#2C3A45`, Ready `#A8D5C0`, Failed `#E8B4B0`. Production remains Mask A until 5.1 deploys.
- **Density:** Sidebar ~200px, top bar 48px, page pad 16px, card gap 12px. Filters 220–280px. Forms 28–36rem. Primary actions ≥44px. ConfirmSheet soft-delete. Soft-dense shell on Login, Dashboard, Documents, Upload, Review, Users, Settings, Reports, Software.
- **IA:** Software + Users nest under Settings. No top-level Review (open from Documents). No Editor mode header toggle. Client / Software only · 4 roles · never County / CAMA.
- **Must-ship UX:** Sticky OCR ribbon (Upload → Queued → Processing → Review → Ready) on Documents / Upload / Review. Dashboard count cards + status-mix donut + volume-over-time with empty chart states. Soft status chips + Failed Retry. Never Ready chip + OCR-failed banner together. Review PDF pane is a real preview or a filled placeholder (not an empty dashed box). Failed deeds show incomplete fields + Retry extract.
- **Hard gates:** No new EF migration. No secrets. No zipdeploy. Do not regress Swagger Authorize ≥44px.

## Phase 4.9 acceptance

- **Admin health probes** on `GET /api/health/detail` (Admin only) and Settings **System health**:
  - Existing **SQL**, **Storage**, and **Queue** reachability remain. Overall is `ok` or `degraded`.
  - **Blob** write / read / delete canary — Pass or Fail. No connection string or key.
  - **Document Intelligence** — endpoint reachability and configured yes/no (`Mock` vs `Azure`). Fail is independent of Blob.
  - **OCR pipeline** — queue reachability **and** worker heartbeat or dequeue signal. Separate from Document Intelligence (queue ≠ DI).
  - **OCR queue visibility** (Azure queue is the bulk buffer — peek only, not a second buffer): depth, oldest waiting age, poison / Failed count, last DI success/fail timestamps.
- Refresh control is ≥44px. Client / Software wording only. Payloads and UI never include secrets.

## Phase 4.7 acceptance

- **Print:** Dashboard **Print** (≥44px) prints the current **filtered** count cards plus visible charts (status mix, by-user, volume). Print CSS hides shell clutter (sidebar, top bar, footer, filters, export actions).
- **Export PDF:** **Export PDF** (≥44px) downloads the same filtered counts and chart series from existing `GET /api/dashboard/counts` and `GET /api/dashboard/charts/*` data. Honors the applied date range and Client filter — never a silent unfiltered dump. Filename includes the date range (`deedai-dashboard-{from}-to-{to}.pdf`). Same role / ClientAccess as the dashboard APIs. Empty or failed export returns a clear message — never a blank PDF. No secrets; Client / Software wording only (no County / CAMA).
- **Should:** `{Client} Deed AI` title when a single Client is in scope; page numbers; generated timestamp.
- **Won’t:** Full BI pack or scheduled email PDFs.

## Phase 4.5 acceptance

- **Users group-by Client:** Phase 4.5 grouped the list by Client (multi-Client users in each group). Phase 5.1.1 replaces that card stack with a searchable sortable table; the Client(s) column still shows every assignment.
- **Profile photo:** upload, replace, and clear (ConfirmSheet on remove). Stored in blob storage. Shown in the header and on Users. JPEG/PNG/WebP/GIF, 2 MB max. Default avatar is initials when there is no photo.
- **Header title:** `{Client} Deed AI` only when the signed-in user's effective scope is exactly one Client; otherwise the product name **Deed AI**.
- **Left shell identity:** **Logged in as {name/email}** plus a **My profile** entry (≥44px) for display name, full name, photo, password change, and assigned Client(s) from existing identity / Client access (clear empty state when none).
- **Settings nest:** Settings → System (mid-level, singular) → Software / Users / API. Top-level Settings group, `/settings`, and Admin permission stay the same. Never Systems / County / CAMA.
- **Full name** sits under **Display name** on Users add/edit and My profile. Required when creating a user.
- **Confirm new password** whenever an Admin or the user sets or changes a password. Mismatch is an inline error. Same Identity password rules. Fields stay ≥44px.
- **Hard gates:** Client / Software naming only. Secrets stay in App Settings / Key Vault. Latest EF migration `20260910020000_Phase523SoftwareDepth` (after `20260910010000_DeletePolicy`) is Designer-first (`[Migration]` + `[DbContext]` + `BuildTargetModel`). Dense Mask F shell. No zipdeploy. Property defaults is not a live Settings feature.

## Phase 4.3 acceptance

- **SPA shell:** Dense SaaS layout (~200px sidebar, 48px top bar, 16/12 padding). Compact count cards and denser tables. Tap targets stay ≥44px. No horizontal page scroll around 768px. Not an AdminLTE clone.
- **Dashboard charts:** Chart.js on existing APIs — `GET /api/dashboard/charts/status-mix` (donut), `GET /api/dashboard/charts/by-user` (stacked bar, Must), `GET /api/dashboard/charts/volume` (line over time), plus existing `GET /api/dashboard/counts`. One date-range / Client filter drives counts and charts. Empty states when a series has no data. Same role / ClientAccess as the APIs (no extra client-side data).
- **Surfaces:** Visual density on Login, Dashboard, Documents, Review, Users, Settings, and Reports.

## Phase 4.8 acceptance

- **Admin Email (SendGrid | SMTP):** Settings panel switches the active mode. Only one mode sends (no dual-send). From name / address are Admin-editable. SMTP timeouts come from Key Vault (`SmtpTimeoutSeconds`, default 30s).
- **KV-only secrets:** SendGrid API key and SMTP host / port / TLS / username / password are App Setting / Key Vault names only. UI shows configured yes/no (SendGrid last-4 when present). Secret values never appear in the UI, API payloads, logs, or tests.
- **Status panel:** Active mode, configured?, last success / last fail (reason sanitized, no secrets).
- **Test send:** Admin types an address, confirms with ConfirmSheet, gets Pass/Fail. Actions ≥ 44px.
- **User email verification:** Verify link/token, unverified users gated when the toggle is on, Admin resend, disabled accounts stay blocked after verify.
- **Forgot / reset + OCR notify** use the active mode and fail closed when that mode is unconfigured (no silent Logging send).
- **Hard gates:** Client / Software naming, four roles, no secrets in repo, no Azure deploy, no 4.7 Print/PDF / 4.9 probes / 5.0 theme / 4.5–4.6 user-field work.

## Phase 4.2 acceptance

- **Ready PDF preview:** Seeded Ready demo deeds (`deeds/demo/…`) have a PDF in blob storage. Opening a Ready deed in review shows the iframe preview. `GET /api/documents/{id}/file` returns `application/pdf` when the blob exists or can be seeded for a demo path. The placeholder (“PDF is not available for this deed.”) shows only when there is truly no file.
- **Status vs Needs review:** Pipeline status is OCR only (Queued / Processing / Ready / Failed). Catalog statuses (Phase 5.2.5) are Client/Software labels mapped onto that pipeline or review workflow. **Needs Work** is the catalog name for Needs review (`ReviewStatus=NeedsWork` or `NeedsReview`). List/review chips show the catalog display name — not Ready and Needs Work together. Setting the flag, catalog status, and `displayStatus` stay in sync. Complete / Approved clears the flag. The OCR ribbon is unchanged.
- **System health (Admin):** Settings has a **System health** card (`#system-health`) for `GET /api/health/detail` — SQL / storage / queue reachability, plus Phase 4.9 Blob R/W, Document Intelligence, OCR pipeline, and queue visibility. No connection strings or keys.
- **Swagger Authorize:** Authorize (top bar and authorize-modal) hit target is ≥44px after Swagger paints (matches Copy Bearer). Phase 4.2.3 loads the pin **once, last** on a custom Swagger `index.html` (after `index.js`; no inline head dump / no early `InjectJavascript`) and re-applies every 250ms so React `display:inline` cannot shrink the button again. Phase 4.2.4 serves `/swagger/index.html` and `/swagger/index.js` with `Cache-Control: no-store` so browsers cannot keep a 7-day stale shell. After load, then QA2: `typeof window.__deedAiMeasureAuthorize === "function"`; `window.__deedAiMeasureAuthorize()` — every `getBoundingClientRect()` width/height ≥ 44; `html[data-deedai-authorize-hit=pass]`; `window.__deedAiAuthorizeRuntimeVersion === "4.2.3"`.
- **Hard gates:** Client / Software naming, four roles, KV-only secrets, no new EF migration, no Azure zipdeploy.

## Phase 4.1 acceptance

- **Swagger (Admin):** Settings toggle **Enable Swagger UI** is database-persisted (`AppSettings.Swagger.Enabled`) and **off by default**. When on, `/swagger` serves Swagger UI with JWT Authorize and a **Copy Bearer** control. When off, `/swagger` (and the OpenAPI JSON) return **404** — not the SPA. Enabling Swagger does not open anonymous API access. Non-admins cannot see or change the toggle. Optional `Swagger__Enabled=true` seeds ON in non-Production only; Production stays off unless an Admin turns it on.
- **Field Help:** Static `?` tooltips (44px, dismiss on outside tap) on Upload Client, Software settings, Sales Tab codes/threshold, report filters, Settings flags/statuses/deed-type maps, Restore / hard-delete ConfirmSheet, and the Swagger toggle. Client / Software wording only.

## Phase 3 acceptance

- **Reports PDF:** reviewed-deed PDF export (QuestPDF-equivalent writer) with the same Client / role gates as CSV and Excel. Empty or failed exports return a clear message — never a silent blank PDF.
- **Notify emails (SendGrid):** OCR Failed and Ready mail to the assignee, optional uploader. UI shows who gets it. Admin off-switch. `SendGridApiKey` / `SendGrid__ApiKey` from App Settings / Key Vault only.
- **Software sync polish:** push retry, last-sync status and fail reason on the deed, lookup by key fields (parcel, grantor, grantee, Client). Named **Software**, never CAMA. Secrets KV only.
- **Settings (Admin):** teams list CRUD, Client CRUD (name / active), export of Settings configs. Flags, statuses, and deed-type maps stay. No full legacy security-policy matrix.
- **UX:** ConfirmSheet on destructive Settings deletes, empty states on Reports and Software, actions ≥ 44px.
- **Hard gates:** Client / Software naming, four roles, KV-only secrets, CORS never `AllowAnyOrigin` + credentials, AuthZ tests for new endpoints.

## Phase 2 acceptance

- **Users (full):** Admin CRUD, role assign, Client access mapping; clear role-denied UI (not a blank page)
- **Forgot / reset password** via SendGrid (replaces contact-Admin stub); hashed single-use tokens; `SendGridApiKey` from App Settings / Key Vault only
- **Doc collab:** team members, flags, linked documents, assignee UI, bulk assign, next/prev on review
- **Settings (Admin):** flags, statuses, deed-type maps + JSON/CSV/Excel export
- **Reports:** CSV and Excel export of visible documents
- **Software lookup / push** to the external system (mock when `SoftwareBaseUrl` is empty)
- **UX P1:** dashboard cards stack on tablet, non-blocking upload progress dock, empty states

## Phase 1 acceptance

- JWT + RBAC (Admin / Editor / Uploader / Viewer)
- Documents list with status chips and **Retry**
- Upload limits: PDF only, 50 MB each
- Field edit as draft + **Retry extract**
- ConfirmSheet soft-delete (“restore from Admin later”)
- OCR queue happy path and fail path; mock Document Intelligence when keys are absent
- Dashboard counts (Uploaded / Queued / Processing / Ready / Failed)
- Actions ≥ 44px
- Clear role-denied message (not a blank page)
- CORS never uses `AllowAnyOrigin` + `AllowCredentials`
- Tests for authorization and OCR
- SPA source in this repo

## Schema gates

`Documents` + `DocumentFields`

- Status check: `Queued` / `Processing` / `Ready` / `Failed`
- Unique `BlobPath`
- Soft-delete (`DeletedAt`) with filtered lists (`IgnoreQueryFilters` only for Admin restore)
- Indexes: `ClientId`, `Status`, `AssigneeUserId`, `UpdatedAt`
- Document Intelligence raw payload in blob; `DiRawBlobPath` pointer on the document
- Migrations contain **no secrets**
- Poison queue (dequeue count ≥ 5) → `Failed` + user **Retry**

## Local run

Requires .NET 10 SDK and Node.js 20+.

```bash
cd spa && npm install && npm run build
cd ../src/DeedAi.Api && dotnet run
```

API: `http://localhost:5080` (serves the SPA after `npm run build`).

SPA hot reload:

```bash
# terminal 1
cd src/DeedAi.Api && dotnet run
# terminal 2
cd spa && npm run dev
```

Vite proxies `/api` and `/swagger` to `http://localhost:5080`.

Seeded local users (password `ChangeMe!1`):

| Email | Role |
| --- | --- |
| admin@bisconsultants.com | Admin |
| editor@bisconsultants.com | Editor |
| uploader@bisconsultants.com | Uploader |
| viewer@bisconsultants.com | Viewer |

Copy `.env.example` and set placeholders. Local extract uses `AzureOpenAI:Mode=Mock` (`Scan_bad.pdf` / names containing `fail` go to Failed). Unconfigured Azure OpenAI fails closed.

With `Queue__Mode=InMemory` and `Ocr__RunInProcess=true` the API hosts the worker in-process so local uploads complete without Azure. In Azure, set `Queue__Mode=Azure` and run `DeedAi.Worker` (or the WebJob packed by the publish script).

## Tests

```bash
dotnet test DeedAi.sln
```

Covers role denial, user CRUD / Client access, password reset happy/fail, upload/edit/delete/restore, CORS policy, OCR happy/fail, poison → Failed+Retry, report PDF empty/error states, dashboard print/export PDF filters, notify emails, Software retry / last-sync, and Settings teams / Clients.

## Publish (Layout A, Windows win-x64 zip)

```bash
chmod +x scripts/publish-layout-a.sh
./scripts/publish-layout-a.sh
```

Writes `artifacts/layout-a/deedai-win-x64.zip` (framework-dependent `win-x64`, IIS in-process) plus a copy at `artifacts/appdeedai-windows.zip`, and a continuous WebJob at `App_Data/jobs/continuous/ocr-worker`.

Deploy the zip to **appdeedai**. Set the App Service stack to **.NET 10**. Startup runs EF `MigrateAsync` (Phase 3+ and Phase 4 SQL Server scripts tolerate a partial apply). Apply settings from `.env.example` (secrets live in App Settings / Key Vault, never in source or migrations).

If login 401s after rotating Key Vault `AdminSeedPassword`, restart the app — startup now updates the `admin@bisconsultants.com` hash. Do not wipe the database. Session idle timeout defaults to **30 minutes** (`Session__IdleTimeoutMinutes` and/or Admin Settings).

**500.30 reminder:** resume serverless `dbdeedai` if paused; `UploadedBy` → Users must stay `ON DELETE NO ACTION` (SQL Server rejects a second cascade path next to Assignee `SET NULL`).

Suggested production App Settings / Key Vault names (placeholders only):

```
AdminSeedPassword
JwtSigningKey
AzureOpenAIEndpoint
AzureOpenAIKey
AzureOpenAIDeployment
AzureOpenAIModel
StorageConnection
SqlConnection
SendGridApiKey
SendGrid__ApiKey
SmtpHost
SmtpPort
SmtpTls
SmtpUsername
SmtpPassword
SmtpTimeoutSeconds
SoftwareApiKey
Software__ApiKey
Database__Provider=SqlServer
Storage__Mode=Azure
Queue__Mode=Azure
Ocr__RunInProcess=false
Session__IdleTimeoutMinutes=30
```

Phase 6 extract uses **AzureOpenAIEndpoint** + **AzureOpenAIKey** + **AzureOpenAIDeployment**. Leftover Document Intelligence App Setting names are ignored and are not a field-fill path.

## RBAC

| Action | Viewer | Uploader | Editor | Admin |
| --- | --- | --- | --- | --- |
| Dashboard / documents list / review | ✓ | ✓ | ✓ | ✓ |
| Upload | | ✓ | ✓ | ✓ |
| Edit fields, Retry | | | ✓ | ✓ |
| Soft-delete | | | when Delete Policy is All Editors | ✓ |
| Restore soft-deleted deeds | | | | ✓ |
| Users + Settings | | | | ✓ |
| Reports export | ✓ | ✓ | ✓ | ✓ |
| Software lookup | ✓ | ✓ | ✓ | ✓ |
| Software push / retry | | | ✓ | ✓ |
| Settings teams / Clients / notify switch | | | | ✓ |

Denied API calls return HTTP 403 JSON: `Access denied. Your {role} role cannot perform this action.` The SPA shows the same on `/denied`.

## Azure names (no secrets)

- Resource group: `rg-bis-deed-ai`
- App Service plan: `asp-bis-deed-ai` (B1, South Central US)
- Web app: `appdeedai` (Windows)
- SQL: `deedaihost01` / `dbdeedai`
- Storage: `stbisdeedai` — blob container `deeds`, queue `ocr-jobs`
