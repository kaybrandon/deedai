# Deed AI — Azure PROD note (Phase 3 + hotfix)

**Date:** 2026-09-09 (America/Chicago)  
**Status:** Phase 1–4 zipdeployed on `appdeedai`. Hotfix PR #8 merged (UploadedBy FK `ON DELETE NO ACTION`). Phase 4 Hardening B adds health detail, seed reseed, idle timeout, OCR cleanup, failed requeue. Phase 4A / 4AQa schema apply is repaired (discoverable + idempotent SQL); redeploy Layout A zip after that hotfix merges.

## Live URL
- **HTTPS:** https://appdeedai-bdfvbng5ckhgfzcp.southcentralus-01.azurewebsites.net  
- (Short name `appdeedai.azurewebsites.net` may redirect to this hostname.)
- **Admin email:** `admin@bisconsultants.com`  
- **Password:** Key Vault `AdminSeedPassword` / private ops file only — **never** paste into chat or docs.

## Resources (names only)
| Piece | Name |
|---|---|
| Resource group | `rg-bis-deed-ai` (South Central US) |
| GitHub | `kaybrandon/deedai` |
| App Service | `appdeedai` (Windows Layout A — API + SPA) |
| SQL | `deedaihost01` / `dbdeedai` (Serverless — can auto-pause) |
| Storage | `stbisdeedai` · blob `deeds` · queue `ocr-jobs` |
| Key Vault | `kv-bis-deed-ai` |

## Key Vault secret names (values never in docs/chat)
`SqlConnection` · `StorageConnection` · `BISDocumentIntelligenceEndpoint` · `DocumentIntelligenceKey` · `JwtSigningKey` · `AdminSeedPassword`

Ignore leftover `DocumentIntelligenceEndpoint` if present. Phase 2+ also uses `SendGridApiKey` / `SoftwareApiKey` when those features are on.

## Naming & roles
- **Client** (not County) · **Software** (not CAMA)
- Roles: **Admin** / **Editor** / **Uploader** / **Viewer**

## Deploy (Layout A)
1. Publish Windows Layout A zip from `main`: `./scripts/publish-layout-a.sh` → `artifacts/layout-a/deedai-win-x64.zip`.
2. Zipdeploy to `appdeedai`. App stack **.NET 10**.
3. Startup runs `MigrateAsync` (SQL Server). Phase 3+ and Phase 4 SQL Server scripts are idempotent (`IF OBJECT_ID` / `IF COL_LENGTH`) so a **partial apply** can finish. Phase 4A / Phase 4AQa are registered EF migrations (they were previously invisible) plus a no-data-wipe `Phase4AzureRepair` catch-up.
4. Smoke: `/` 200 · `/api/health` 200 · Admin login · documents · dashboard. Admin Settings **System health** (or `/api/health/detail`) reports SQL / storage / queue plus Blob R/W, Document Intelligence, OCR pipeline, and queue visibility — modes and counts only (no secrets). Ready demo deeds should preview a PDF.

## Known ops: 500.30
1. Confirm KV refs on App Settings resolve (no secret values in chat).
2. If SQL unavailable: Portal → **`dbdeedai`** → **Resume** (Serverless auto-pause), then restart App Service.
3. Check App Service logs / Kudu if still failing.
4. After a Phase 2+ zip: if EF tries `InitialCreate` on existing tables → baseline `__EFMigrationsHistory` (do not recreate schema). Historical `There is already an object named 'Clients'` is this case — do **not** drop Clients.
5. **Cascade lesson (PR #8):** SQL Server rejects two cascade paths from `Documents` → `Users`. `AssigneeUserId` may `SET NULL`; **`UploadedByUserId` must be `ON DELETE NO ACTION`**. A Phase 3 deploy that created the FK as cascade/set-null failed startup; hotfix recreates the FK as NoAction and is idempotent on re-run.
6. **Phase 4 schema 500:** Kudu `Invalid object name 'AppPolicies'` / `Invalid column name 'SalesTabCode'` means Phase 4A DDL never landed (handwritten migrations lacked `[Migration]` / `[DbContext]`, so EF skipped them). After the schema hotfix merge, **zipdeploy Layout A again**. Startup will create missing Phase 4 objects only; existing rows stay. Then `/api/health` should be 200.

## Admin seed sync
`DatabaseSeeder` used to create users only when `Users` was empty. Rotating Key Vault `AdminSeedPassword` did **not** update the existing `admin@bisconsultants.com` hash (live 401). Startup now updates that seed admin hash when `AdminSeedPassword` (or aliases `Admin__SeedPassword` / `Admin:SeedPassword`) is present. Seeded demo users still on the default seed password are updated too. Data is not wiped. **Never log the password.**

## Session idle timeout
Default **30 minutes** (`Session__IdleTimeoutMinutes` App Setting and/or Admin Settings → Session idle timeout). Expired session returns to login with a reason; unsaved draft field edits are not silently wiped.

## Phase 1 smoke (QA closed)
- `/` 200 · `/api/health` 200 · Admin login · `/api/auth/me` · `/api/documents` · `/api/dashboard`

## Docs gates
- Client-facing wording / external handout → Brandon via Chief of Staff.
