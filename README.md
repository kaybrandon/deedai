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
- Phase 1 wires: [login](docs/wires/01-login.png) · [dashboard](docs/wires/02-dashboard.png) · [documents](docs/wires/03-documents.png) · [upload](docs/wires/04-upload.png) · [review](docs/wires/05-review.png)

## Stack

| Piece | Choice |
| --- | --- |
| API | .NET 10 Web API (`src/DeedAi.Api`) |
| SPA | React + Vite + TypeScript (`spa/`) built into `src/DeedAi.Api/wwwroot` |
| OCR worker | .NET 10 worker on Azure Storage Queue `ocr-jobs` (long-poll, not a 1s loop) |
| Layout | **A** — one Windows App Service host serves API + SPA |
| SQL | Azure SQL `deedaihost01` / `dbdeedai` (SQLite for local/dev) |
| Storage | `stbisdeedai` container `deeds`; Document Intelligence raw JSON stored in blob with a pointer |
| App Service | `appdeedai`, plan `asp-bis-deed-ai` B1, RG `rg-bis-deed-ai`, South Central US |

Document Intelligence may live in **Central US**. Configure the **explicit endpoint**; do not assume it is in the same region as the app.

## Phase 5.0 acceptance

- **Mask A theme:** Mist Slate + Teal tokens — bg `#F4F6F8`, sidebar `#E8EEF2`, accent `#4F7C8A`, text `#2C3A45`, Ready `#A8D5C0`, Failed `#E8B4B0`. Processing gold · Queued slate · Review teal-tint. Failed/Queued chips use darker text and a soft border.
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

## Phase 4.5 acceptance

- **Users group-by Client:** the Users list is grouped by Client. A user assigned to more than one Client appears under each of those groups. Groups collapse/expand. Users with no Client access sit in a **No Client access** group.
- **Profile photo:** upload, replace, and clear (ConfirmSheet on remove). Stored in blob storage. Shown in the header and on Users. JPEG/PNG/WebP/GIF, 2 MB max. Default avatar is initials when there is no photo.
- **Header title:** `{Client} Deed AI` only when the signed-in user's effective scope is exactly one Client; otherwise the product name **Deed AI**.
- **Left shell identity:** **Logged in as {name/email}** plus a **My profile** entry (≥44px) for display name, full name, photo, and password change.
- **Full name** sits under **Display name** on Users add/edit and My profile. Required when creating a user.
- **Confirm new password** whenever an Admin or the user sets or changes a password. Mismatch is an inline error. Same Identity password rules. Fields stay ≥44px.
- **Hard gates:** Client / Software naming only. Secrets stay in App Settings / Key Vault. EF migration `20260909160000_Phase45UsersIdentity` is Designer-first (`[Migration]` + `[DbContext]` + `BuildTargetModel`). Dense Mask A shell. No zipdeploy.

## Phase 4.3 acceptance

- **SPA shell:** Dense SaaS layout (~200px sidebar, 48px top bar, 16/12 padding). Compact count cards and denser tables. Tap targets stay ≥44px. No horizontal page scroll around 768px. Not an AdminLTE clone.
- **Dashboard charts:** Chart.js on existing APIs — `GET /api/dashboard/charts/status-mix` (donut), `GET /api/dashboard/charts/by-user` (stacked bar, Must), `GET /api/dashboard/charts/volume` (line over time), plus existing `GET /api/dashboard/counts`. One date-range / Client filter drives counts and charts. Empty states when a series has no data. Same role / ClientAccess as the APIs (no extra client-side data).
- **Surfaces:** Visual density on Login, Dashboard, Documents, Review, Users, Settings, and Reports.

## Phase 4.2 acceptance

- **Ready PDF preview:** Seeded Ready demo deeds (`deeds/demo/…`) have a PDF in blob storage. Opening a Ready deed in review shows the iframe preview. `GET /api/documents/{id}/file` returns `application/pdf` when the blob exists or can be seeded for a demo path. The placeholder (“PDF is not available for this deed.”) shows only when there is truly no file.
- **Status vs Needs review:** Pipeline status is OCR only (Queued / Processing / Ready / Failed). **Needs review** is a flag that implies review workflow and sets `ReviewStatus=NeedsReview`. List/review chips show Needs review — not Ready and Needs review together. Setting the flag, review-status dropdown, and `displayStatus` stay in sync. Approved clears the flag.
- **System health (Admin):** Settings has a **System health** card (`#system-health`) for `GET /api/health/detail` — SQL / storage / queue reachability, plus Phase 4.9 Blob R/W, Document Intelligence, OCR pipeline, and queue visibility. No connection strings or keys.
- **Swagger Authorize:** Authorize (top bar and authorize-modal) hit target is ≥44px after Swagger paints (matches Copy Bearer). Phase 4.2.3 loads the pin **once, last** on a custom Swagger `index.html` (after `index.js`; no inline head dump / no early `InjectJavascript`) and re-applies every 250ms so React `display:inline` cannot shrink the button again. After load, then QA2: `typeof window.__deedAiMeasureAuthorize === "function"`; `window.__deedAiMeasureAuthorize()` — every `getBoundingClientRect()` width/height ≥ 44; `html[data-deedai-authorize-hit=pass]`; `window.__deedAiAuthorizeRuntimeVersion === "4.2.3"`.
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

Copy `.env.example` and set placeholders. Empty Document Intelligence endpoint/key uses the **mock** extractor (`Scan_bad.pdf` / names containing `fail` go to Failed).

With `Queue__Mode=InMemory` and `Ocr__RunInProcess=true` the API hosts the worker in-process so local uploads complete without Azure. In Azure, set `Queue__Mode=Azure` and run `DeedAi.Worker` (or the WebJob packed by the publish script).

## Tests

```bash
dotnet test DeedAi.sln
```

Covers role denial, user CRUD / Client access, password reset happy/fail, upload/edit/delete/restore, CORS policy, OCR happy/fail, poison → Failed+Retry, report PDF empty/error states, notify emails, Software retry / last-sync, and Settings teams / Clients.

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
DocumentIntelligenceKey
BISDocumentIntelligenceEndpoint
StorageConnection
SqlConnection
SendGridApiKey
SendGrid__ApiKey
SoftwareApiKey
Software__ApiKey
Database__Provider=SqlServer
Storage__Mode=Azure
Queue__Mode=Azure
Ocr__RunInProcess=false
Session__IdleTimeoutMinutes=30
```

Document Intelligence uses **BISDocumentIntelligenceEndpoint** + **DocumentIntelligenceKey**. Leftover `DocumentIntelligenceEndpoint` is ignored. The DI resource may be Central US — set the explicit endpoint.

## RBAC

| Action | Viewer | Uploader | Editor | Admin |
| --- | --- | --- | --- | --- |
| Dashboard / documents list / review | ✓ | ✓ | ✓ | ✓ |
| Upload | | ✓ | ✓ | ✓ |
| Edit fields, Retry, soft-delete | | | ✓ | ✓ |
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
