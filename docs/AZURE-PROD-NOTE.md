# Deed AI — Azure PROD note (Phase 1–2)

**Date:** 2026-09-08 (America/Chicago)  
**Status:** Phase 1 Azure Pass · Phase 2 Azure Pass with notes · Phase 3 building.

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

## API note (Phase 2)
- Dashboard counts: **`/api/dashboard/counts`** (200). Bare `/api/dashboard` may fall through to SPA HTML — SPA must use `/counts` (locked).

## Demo deeds (testing)
Live seed for UI walks (no secrets):
| File | Status |
|---|---|
| `Deed_2024_0812.pdf` | Ready (mapped fields) |
| `Scan_bad.pdf` | Failed (+ Retry) |
| `Batch_44.pdf` | Processing |
| `Queued_north.pdf` | Queued |

Expected counts: uploaded 4 · queued 1 · processing 1 · ready 1 · failed 1

## Phase smoke (QA closed)
- **P1:** `/` 200 · `/api/health` 200 · Admin login · documents · dashboard
- **P2:** Users (Admin 200 / Viewer 403) · Settings CRUD + CSV export · Forgot unknown → generic success · Reports · SPA `/users` `/settings` `/reports` `/forgot-password`

## Docs gates
- Phase 3 how-tos after that Azure Pass.
- Client-facing wording / external handout → Brandon via Chief of Staff.
