# Deed AI — Azure PROD note (Phase 3 + hotfix)

**Date:** 2026-09-09 (America/Chicago)  
**Status:** Phase 1–3 live on `appdeedai`. Hotfix PR #8 merged (UploadedBy FK `ON DELETE NO ACTION`). Phase 4 Hardening B adds health detail, seed reseed, idle timeout, OCR cleanup, failed requeue.

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
3. Startup runs `MigrateAsync` (SQL Server). Phase 3+ migrations are written to survive a re-run after a **partial apply**.
4. Smoke: `/` 200 · `/api/health` 200 · Admin login · documents · dashboard. Admin may call `/api/health/detail` (SQL / storage mode / queue mode — no secrets).

## Known ops: 500.30
1. Confirm KV refs on App Settings resolve (no secret values in chat).
2. If SQL unavailable: Portal → **`dbdeedai`** → **Resume** (Serverless auto-pause), then restart App Service.
3. Check App Service logs / Kudu if still failing.
4. After a Phase 2+ zip: if EF tries `InitialCreate` on existing tables → baseline `__EFMigrationsHistory` (do not recreate schema).
5. **Cascade lesson (PR #8):** SQL Server rejects two cascade paths from `Documents` → `Users`. `AssigneeUserId` may `SET NULL`; **`UploadedByUserId` must be `ON DELETE NO ACTION`**. A Phase 3 deploy that created the FK as cascade/set-null failed startup; hotfix recreates the FK as NoAction and is idempotent on re-run.

## Admin seed sync
`DatabaseSeeder` used to create users only when `Users` was empty. Rotating Key Vault `AdminSeedPassword` did **not** update the existing `admin@bisconsultants.com` hash (live 401). Startup now updates that seed admin hash when `AdminSeedPassword` (or aliases `Admin__SeedPassword` / `Admin:SeedPassword`) is present. Seeded demo users still on the default seed password are updated too. Data is not wiped. **Never log the password.**

## Session idle timeout
Default **30 minutes** (`Session__IdleTimeoutMinutes` App Setting and/or Admin Settings → Session idle timeout). Expired session returns to login with a reason; unsaved draft field edits are not silently wiped.

## Phase 1 smoke (QA closed)
- `/` 200 · `/api/health` 200 · Admin login · `/api/auth/me` · `/api/documents` · `/api/dashboard`

## Docs gates
- Client-facing wording / external handout → Brandon via Chief of Staff.
