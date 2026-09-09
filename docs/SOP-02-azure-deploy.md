# SOP-02 — Deed AI Azure deploy (Phase 3+)

**Who:** Ops / Dev deploying `appdeedai`

## Checklist
- [ ] RG `rg-bis-deed-ai` · SQL `deedaihost01`/`dbdeedai` · Storage `stbisdeedai` (deeds + ocr-jobs queue) · KV `kv-bis-deed-ai`
- [ ] App Settings use **Key Vault references** for the secret names (see AZURE-PROD-NOTE). No secret values in chat or git.
- [ ] Publish Windows **Layout A** zip from `main`: `./scripts/publish-layout-a.sh` → zipdeploy to `appdeedai` (`.NET 10`)
- [ ] Startup **EF `MigrateAsync`** applies pending migrations (Phase 3+ and Phase 4 SQL Server scripts are idempotent for a partial apply; Phase 4A / 4AQa must be discoverable EF migrations)
- [ ] If **500.30**:
  - Resume serverless `dbdeedai` if paused
  - Verify KV refs resolve
  - UploadedBy FK must be **NoAction** (not a second cascade/set-null path on `Documents` → `Users`) — see AZURE-PROD-NOTE
  - Restart app
- [ ] Confirm `AdminSeedPassword` in KV matches the hash you expect. Rotating the KV secret updates `admin@bisconsultants.com` on next startup (does not wipe data).
- [ ] Optional App Setting: `Session__IdleTimeoutMinutes` (default **30**)
- [ ] Smoke: home 200 · `/api/health` 200 · Admin login · documents · dashboard. Admin `/api/health/detail` reports SQL / storage mode / queue mode only (no keys).

## Naming
**Client** (never County) · **Software** (never CAMA). Roles: Admin / Editor / Uploader / Viewer.

## No secrets in chat
Password / connection strings stay in KV or a private chmod-600 ops file — never paste into the repo or chat.
