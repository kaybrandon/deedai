# Deed AI — Azure PROD note (Phase 1)

**Date:** 2026-09-08 (America/Chicago)  
**Status:** QA **Azure Pass** on Phase 1 login/smoke. Phase 2 #4 merged (redeploy in flight). Phase 3 building.

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

Ignore leftover `DocumentIntelligenceEndpoint` if present.

## Naming & roles
- **Client** (not County) · **Software** (not CAMA)
- Roles: **Admin** / **Editor** / **Uploader** / **Viewer**

## Known ops: 500.30
1. Confirm KV refs on App Settings resolve (no secret values in chat).
2. If SQL unavailable: Portal → **`dbdeedai`** → **Resume** (Serverless auto-pause), then restart App Service.
3. Check App Service logs / Kudu if still failing.
4. After a Phase 2+ zip: if EF tries `InitialCreate` on existing tables → baseline `__EFMigrationsHistory` (do not recreate schema).

## Phase 1 smoke (QA closed)
- `/` 200 · `/api/health` 200 · Admin login · `/api/auth/me` · `/api/documents` · `/api/dashboard`

## Docs gates
- Phase 2/3 how-tos after those Azure Passes.
- Client-facing wording / external handout → Brandon via Chief of Staff.
