# Changelog — Deed AI SOPs

## 2026-09-09 — Phase 4 Azure schema hotfix
- Phase 4A / Phase 4AQa are now registered EF migrations (`[Migration]` + `[DbContext]`) so Azure `MigrateAsync` actually applies AppPolicies, field maps, PropertyDefaults, SoftwareClientConfigs, SalesTabCodes, and `Documents.SalesTabCode`.
- SQL Server scripts are idempotent (`IF OBJECT_ID` / `IF COL_LENGTH` / `IF NOT EXISTS`). `Phase4AzureRepair` re-applies any missing Phase 4 objects without wiping data. UploadedBy stays `ON DELETE NO ACTION`.
- Ops: after merge, redeploy Layout A zip to `appdeedai`. Do not drop Clients or baseline-wipe.

## 2026-09-09 — Phase 3 + hotfix + Hardening B
- AZURE-PROD-NOTE / SOP-02: Layout A zip, EF MigrateAsync, 500.30 cascade lesson (UploadedBy NoAction), serverless SQL Resume, AdminSeedPassword hash sync, Client/Software naming, idle timeout default 30 minutes.
- Public `/api/health` stays shallow; Admin `/api/health/detail` adds SQL / storage / queue checks (no secrets).

## 2026-09-08 — EF baseline tip
- AZURE-PROD-NOTE: Phase 2 redeploy 500.30 can be missing `__EFMigrationsHistory` vs existing schema — baseline history, don’t re-run InitialCreate.

## 2026-09-08 — Phase 1 Azure Pass
- Added AZURE-PROD-NOTE, SOP-01 overview, SOP-02 Azure deploy, SOP-04 staff quick start after QA Azure Pass (login/health/documents/dashboard).
- Documented serverless SQL Resume tip for 500.30; KV names only; Client/Software; 4 roles.
- Phase 1 wires under `docs/wires/`.
