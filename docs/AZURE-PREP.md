# Deed AI — Azure prep note (Phase 1)

**Status:** Superseded for live ops by [AZURE-PROD-NOTE.md](AZURE-PROD-NOTE.md) after QA Azure Pass. Kept for history.

## Resources (locked)
| Piece | Name |
|---|---|
| Resource group | `rg-bis-deed-ai` (South Central US) |
| GitHub | `kaybrandon/deedai` |
| SQL server / DB | `deedaihost01` / `dbdeedai` (Serverless — can auto-pause) |
| Storage | `stbisdeedai` · blob `deeds` · queue `ocr-jobs` |
| App Service | `appdeedai` → https://appdeedai.azurewebsites.net |
| Key Vault | `kv-bis-deed-ai` |

## Key Vault secret **names** (values never in chat/docs)
`SqlConnection` · `StorageConnection` · `BISDocumentIntelligenceEndpoint` · `DocumentIntelligenceKey` · `JwtSigningKey` · `AdminSeedPassword`

## Known ops: 500.30 after deploy
If the site returns **500.30** and logs show SQL unavailable: open Portal → SQL database **`dbdeedai`** → **Resume** (Serverless auto-pause). Then restart App Service / re-smoke. Don’t paste connection strings into chat.

## Naming
**Client** (not County) · **Software** (not CAMA) · 4 roles: Admin / Editor / Uploader / Viewer

## Docs gate
Full operator SOPs after QA Azure Pass. Phase 2/3 how-tos after those ship.
