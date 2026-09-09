# Changelog — Deed AI SOPs

## 2026-09-09 — Phase 4.9 system health probes + queue visibility
- Admin `GET /api/health/detail` and Settings System health add Blob R/W (Pass/Fail), Document Intelligence reachability + configured, OCR pipeline (queue + worker heartbeat/dequeue), and queue visibility (depth, oldest waiting age, poison / Failed, last DI success/fail). Existing SQL / Storage / Queue remain. No secrets. No Azure deploy.

## 2026-09-09 — Phase 4.2.2 Authorize hit-target persist
- Live Azure after `#18` still measured ~34px (top bar) / ~30px (modal). Root cause: Swashbuckle 9 puts `HeadContent` / `InjectJavascript` in `<head>` before `swagger-ui-bundle.js`, and Swagger React re-applies `display:inline` after the 4s pin (height is ignored on inline). Custom `index.html` now loads `/swagger/deedai-swagger-authorize.js` **last**; a 250ms poll + attribute observer re-pins `height/min-height:44px; max-height:none; display:inline-flex !important`.
- Dev must self-verify after zipdeploy before pinging QA2: on `/swagger`, `window.__deedAiMeasureAuthorize()` — every `getBoundingClientRect()` width/height ≥ 44; `html[data-deedai-authorize-hit=pass]`; `window.__deedAiAuthorizeRuntimeVersion === "4.2.2"`.

## 2026-09-09 — Phase 5.0 Mask A theme
- Mist Slate + Teal tokens on the soft-dense shell (Login, Dashboard, Documents, Upload, Review, Users, Settings, Reports, Software).
- Sticky OCR ribbon on Documents / Upload / Review. Failed review shows incomplete fields + Retry extract. Soft Failed/Queued chips. No new EF migration.

## 2026-09-09 — Phase 4.2.1 Authorize hit-target runtime
- CSS-only `#17` was on the live page and still lost after Swagger paint (~34px top bar, ~30px modal). Runtime JS now wraps `SwaggerUIBundle` `onComplete`, uses `MutationObserver`, re-pins a late stylesheet, and sets inline `!important` 44×44 on Authorize (top bar + modal).
- QA2 measure (after `/swagger` paints): `window.__deedAiMeasureAuthorize()` — every item `width`/`height` ≥ 44. Pass marker: `document.documentElement.dataset.deedaiAuthorizeHit === "pass"`.

## 2026-09-09 — Phase 4.2.1 Authorize hit-target hotfix
- Swagger UI Authorize (top bar + authorize-modal Authorize / Logout / Close) is a real ≥44px tap target. Prior HeadContent `min-height: 44px` lost to Swagger's `display: inline` on `.btn.authorize` (QA measured ~34px). Override now forces `inline-flex`, `min-height`/`min-width` 44px, and padding.

## 2026-09-09 — Phase 4.3 SPA shell + Chart.js
- Dense SaaS shell (~200px sidebar, 48px top bar, 16/12 padding). Compact status cards and denser tables; actions stay ≥44px. No horizontal page scroll at ~768px.
- Dashboard Chart.js wired to existing `GET /api/dashboard/charts/status-mix`, `by-user`, and `volume` plus `GET /api/dashboard/counts`. Same date-range / Client filter and role/ClientAccess as the APIs. Empty chart states when no data.
- First-surface visual refresh only (Login, Dashboard, Documents, Review, Users / Settings / Reports). Client / Software naming only.

## 2026-09-09 — Phase 4.2 smoke fixes
- Ready demo deeds seed a real PDF at `deeds/demo/…` so review preview (`GET /api/documents/{id}/file`) works. Placeholder only when a file truly does not exist.
- Needs review is a flag that drives review workflow / `ReviewStatus`. Pipeline status stays Queued/Processing/Ready/Failed. List and review chips show **Needs review** (not Ready + Needs review). Approving or clearing the flag keeps them in sync.
- Admin Settings **System health** card embeds `/api/health/detail` (SQL / storage / queue mode only — no secrets).
- Swagger UI Authorize hit target is ≥44px.
- Rebased onto `main` after PR #15 (dense shell + Chart.js).

## 2026-09-09 — Phase 4.1 Swagger + field Help
- Admin Settings **Enable Swagger UI** (DB-persisted, off by default). `/swagger` is 404 when off; JWT Authorize + Copy Bearer when on. API auth unchanged.
- Static field Help tooltips on Must fields (Client / Software wording only). No Azure deploy / App Service changes.
- Rebased onto PR #11. Migration id is `20260909140000_Phase41SwaggerHelp` (after `Phase4AzureRepair`, no timestamp collision) with a `*.Designer.cs` (`[Migration]` + `[DbContext]` + `BuildTargetModel`).

## 2026-09-09 — Phase 4 Azure schema hotfix
- Phase 4A / Phase 4AQa now have `*.Designer.cs` files (`[Migration]` + `[DbContext]` + `BuildTargetModel`) so Azure `MigrateAsync` actually applies AppPolicies, field maps, PropertyDefaults, SoftwareClientConfigs, SalesTabCodes, and `Documents.SalesTabCode`.
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
