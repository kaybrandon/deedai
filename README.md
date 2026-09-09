# Deed AI

BIS Consultants **Deed AI** — Phase 2. Naming in this product is **Client** (never County) and **Software** (never CAMA). Roles: **Admin**, **Editor**, **Uploader**, **Viewer**.

This repository replaces the README-only GitHub seed with a working Layout A application: a single .NET 10 API host serves the React/Vite SPA from `wwwroot` for Windows App Service `appdeedai`.

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

Vite proxies `/api` to `http://localhost:5080`.

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

Covers role denial, user CRUD / Client access, password reset happy/fail, upload/edit/delete/restore, CORS policy, OCR happy/fail, and poison → Failed+Retry.

## Publish (Layout A, Windows win-x64 zip)

```bash
chmod +x scripts/publish-layout-a.sh
./scripts/publish-layout-a.sh
```

Writes `artifacts/layout-a/deedai-win-x64.zip` (framework-dependent `win-x64`, IIS in-process) plus a copy at `artifacts/appdeedai-windows.zip`, and a continuous WebJob at `App_Data/jobs/continuous/ocr-worker`.

Deploy the zip to **appdeedai**. Set the App Service stack to **.NET 10**. Apply settings from `.env.example` (secrets live in App Settings / Key Vault, never in source or migrations).

Suggested production App Settings / Key Vault names (placeholders only):

```
AdminSeedPassword
JwtSigningKey
DocumentIntelligenceKey
BISDocumentIntelligenceEndpoint
StorageConnection
SqlConnection
SendGridApiKey
SoftwareApiKey
Database__Provider=SqlServer
Storage__Mode=Azure
Queue__Mode=Azure
Ocr__RunInProcess=false
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
| Software push | | | ✓ | ✓ |

Denied API calls return HTTP 403 JSON: `Access denied. Your {role} role cannot perform this action.` The SPA shows the same on `/denied`.

## Azure names (no secrets)

- Resource group: `rg-bis-deed-ai`
- App Service plan: `asp-bis-deed-ai` (B1, South Central US)
- Web app: `appdeedai` (Windows)
- SQL: `deedaihost01` / `dbdeedai`
- Storage: `stbisdeedai` — blob container `deeds`, queue `ocr-jobs`
