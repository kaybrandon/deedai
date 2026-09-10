# Changelog — Deed AI SOPs

## 2026-09-10 — Phase 6 AI extract
- Azure OpenAI (same subscription, `gpt-4o-mini` default) fills locked Review fields from the PDF. Document Intelligence field-fill is removed (no dual path, no flag leaving both). Fail closed if KV keys are missing.
- Human edit loop stays. Re-extract (single + batch) uses ConfirmSheet. Confidence chips and raw AI blob audit (`ai-raw/{id}.json`) are Should.
- KV names: AzureOpenAIEndpoint · AzureOpenAIKey · AzureOpenAIDeployment · AzureOpenAIModel. Do not raise quotas. Escalate spend to CoS before a pricier model.
- Designer-first EF migration `20260910040000_Phase6AiExtract` adds null-safe AiRawBlobPath / ExtractConfidenceJson. Client/Software only. Health 200. No Azure deploy.

## 2026-09-10 — Phase 5.2.5 Statuses catalog
- Settings → System → Statuses is a Client/Software catalog. Seeded Must eight: Complete, In Queue, Needs Work, New, Not Needed, Pending, Research, Upload Error. Each maps to pipeline Queued/Processing/Ready/Failed or a review/extension state.
- Editors assign catalog status on Documents and Review without changing OCR `Document.Status`. List filters and chips use catalog display names. Ready + OCR-failed stay mutually exclusive. Ribbon unchanged.
- Admin add/rename/soft-disable. Seed/system rows cannot be hard-deleted (ConfirmSheet on disable/delete, ≥44px). Sort order and Mask F colors.
- Designer-first EF migration `20260910030000_Phase525StatusesCatalog` adds nullable MapsTo / Kind / IsSeed with NULL backfill + SQL defaults + coalesce (lesson from #35/#36). No County/CAMA. DocumentLog parked.

## 2026-09-10 — Phase 5.2.4 Delete Policy
- Admin Settings → System persists **Delete Policy** / **Who Can Delete**: All Editors or Admin only. Uploader and Viewer never soft-delete.
- Documents hides Delete when the role is below the policy. Unauthorized API delete returns 403 (not 404). ConfirmSheet stays required when allowed (≥44px). Restore stays Admin-gated.
- Editors see a read-only Delete Policy hint. Last Admin who changed the policy is stored as a lightweight audit.
- Designer-first EF migration `20260910010000_DeletePolicy` adds `DeletePolicySettings` with null-safe `WhoCanDelete` (DEFAULT AllEditors + NULL backfill). Client / Software only. No Super Admin / County / CAMA.

## 2026-09-09 — Phase 5.2.3 Software settings depth
- Admin Software settings add Client-scoped image codes (lookup/push), Grantee combiner (never CAMA), certified + default year with a sensible range, and the sales-ratio / finance / instrument triad.
- Field maps expose the remaining typed deed keys (mailing, volume, page, document number, PID, legal, image code, years) so Harris-class maps match configured legacy keys. API names are Software only — no `cama*`.
- Lookup and push send combined Grantee, year, and image code when set. Six push resets, role-gated Push/lookup/retry/last-sync, Property defaults stay removed, Settings → System → Software nest, Mask F 680px form / full-width instances, no FieldHelp `?` pills, KV-only secrets.
- Designer-first EF migration `20260910020000_Phase523SoftwareDepth` (after Delete Policy) backfills new string columns and adds SQL defaults (lesson from #35/#36). No Azure deploy.

## 2026-09-09 — Phase 5.2.1 Review field depth
- Review is Mask F 3-col queue | PDF | extracted fields. Multi grantor/grantee rows add/remove, persist order, reject empty rows, ≥44px, ConfirmSheet when a named row is removed.
- Document Number, Volume, Page, Deed Type, PID, and Mailing street/city/state/ZIP save on the document using locked 5.2.2 names. No `docNo` / `vol` / County / CAMA.
- Client-scoped Software search reuses `/api/software/lookup` and applies PID, owner, and mailing. Push is role-gated with ConfirmSheet; success/fail inline; no secrets.
- Carry Failed + Incomplete (never Ready + OCR-failed), Retry Extract, Title Case labels, sentence-case helpers, PDF preview or clear empty. Reuses `20260909220000_DocumentListFields` plus the #36 NULL default hotfix. No new EF migration.

## 2026-09-09 — Hotfix: Document list NULL materialization (HTTP 500.30)
- After PR #35, Azure SQL existing `Documents` rows had NULL in columns added by `20260909220000_DocumentListFields`. Seed `EnsureReviewConsistencyAsync` loaded full rows and SQL Server `GetString` threw `SqlNullValueException` (ANCM 500.30).
- Follow-up Designer-first migration `20260909230000_DocumentListFieldNullDefaults` UPDATEs NULLs to empty string and adds SQL defaults for locked fields: DocumentNumber, Volume, Page, DeedType, Pid, MailingStreet/City/State/Zip, Grantors, Grantees.
- Grantors/Grantees EF conversion is `string?` so NULL still materializes to `[]`. Seeder projects only Id + ReviewStatus. Locked field names unchanged. No 5.2.1 Review UI. No Azure deploy.

## 2026-09-09 — Phase 5.2.2 Manage Documents rich list
- Documents is a Mask F SoT table: hug 44px rows, sticky gray header, zebra stripes, one filter-row Search ≤360px. No top-bar search.
- Search covers name/status/assignee, grantors/grantees, Volume, Page, Document Number, PID, mailing street/city/state/zip, and existing legal description.
- Column headings sort Status · Client · Volume · Page · Type · PID · Doc # · Assignee · Updated with `aria-sort`. Filters: status, date, Client, assignee, type (≥44px). Page size 50. URL + session persist. Empty / no-match.
- Locked shared fields with 5.2.1 Review: `documentNumber`, `volume`, `page`, `deedType`, `pid`, `mailingStreet`, `mailingCity`, `mailingState`, `mailingZip`, `grantors[]`, `grantees[]`. No `docNo` / `vol` / County / CAMA aliases.
- Carry OCR ribbon, Retry Failed, ConfirmSheet soft-delete, chart-click deep links. EF migration `20260909220000_DocumentListFields` is Designer-first (`[Migration]` + `[DbContext]` + `BuildTargetModel`). No zipdeploy.

## 2026-09-09 — Phase 5.1.1 Users searchable sortable table
- Admin Users is a real data table matching Mask F SoT density (`05-settings-users`): hug 44px rows, sticky gray header, zebra stripes, role chips, single-line sort+filter headings. One filter-row Search ≤360px across display name, full name, and email. No top-bar search.
- Column headings sort Display Name, Email, Role, Client(s), and Status. Role / Client / Status filters sit on those headings.
- Multi-Client users list every assignment in Client(s). ConfirmSheet disable and add/edit photo/password stay. URL + session persist sort/filter. Empty / no-match + page size 50. Client / Software only. No EF migration. No zipdeploy.
- Settings nest stays Mask F: Settings → System (singular mid-parent) → indented Software · Users · API. Never Systems plural. Users stays Admin-only.

## 2026-09-09 — Phase 5.1 Mask F Mockitt Admin theme
- Mask F tokens and dark rail (`#1E2430`) on Login, Dashboard, Documents, Upload, Review, Users, Settings, Reports, Software.
- Documents list has one filter-row search (max 360px). Settings nest is Settings → System (mid-level, singular) → Software / Users / API. `/settings` title is **System**. Never Systems / County / CAMA. Parents are not double-highlighted with a leaf.
- FieldHelp `?` pills removed. Help is native `title` / `aria-describedby`. Settings and Software forms hug ~680px / ~32rem with 4/8/12 spacing. Software Instances table uses remaining page width (Client configs — no invented Environment / County / CAMA columns).
- Volume Over Time is a single teal series `#0D8A7F`. Review uses a 3-column grid. Disable user still uses ConfirmSheet. No EF migration. No zipdeploy. Mask A remains on production until this ships.

## 2026-09-09 — Phase 5.0.2 Volume Over Time weekly bars
- Volume Over Time is a **bar** chart (not line/area) with **one teal Uploaded series** per ISO week — no stacked status bars. Title Case title unchanged.
- X-axis buckets are **ISO weeks** (Monday–Sunday). Click a week bar or ≥44px keyboard link opens Documents with `from`/`to` for that week and keeps Client when set. Empty week → Documents empty state.
- PDF volume table column is **Week**. No EF migration. Mask A tokens unchanged (separate from Mask F).

## 2026-09-09 — Phase 4.8 Admin email (SendGrid | SMTP)
- Admin Settings email panel switches **SendGrid** or **SMTP**. Secrets stay in Key Vault; UI shows configured yes/no and SendGrid last-4 only.
- Status panel: active mode, configured?, last success/fail. Test send to a typed address with ConfirmSheet and Pass/Fail.
- User email verification (link/token), unverified gate, Admin resend. Disabled accounts stay blocked.
- Forgot/reset and OCR notify use the active mode and fail closed when it is unconfigured.
- EF migration `20260909180000_Phase48AdminEmail` (after Phase 4.5, before Phase 4.9.1) includes Designer + `[Migration]` + `[DbContext]` + `BuildTargetModel`.

## 2026-09-09 — Phase 4.9.1 remove Property defaults
- Settings no longer has a Property defaults panel (Admin and non-Admin). Software field maps, the six push resets (Exemptions / Supplement Year / Sales Letter / Sales Tab / Agents / Mortgage Codes), and OCR Settings stay.
- `/api/settings/property-defaults` create/read/update/delete/reset are gone (404/410). No stubs.
- EF migration `20260909190000_Phase491RemovePropertyDefaults` drops the `PropertyDefaults` table and seed. Designer-first (`[Migration]` + `[DbContext]` + `BuildTargetModel`). Client / Software naming only.

## 2026-09-09 — Dashboard polish: Title Case + clickable chart filters
- BA chart titles (exact): **Status Mix**, **By Users**, **Volume Over Time**. Same titles on dashboard print/PDF export.
- Sitewide SPA Title Case on headings, nav, card titles, and primary button/tab/field labels. Kickers, placeholders, errors, and help stay sentence case.
- Status Mix donut segments and ≥44px keyboard legend links open Documents with `status` plus the applied **date range and Client**.
- By Users bars and ≥44px user links open Documents with `assigneeUserId` plus the applied date range and Client. Unassigned has no Documents assignee filter, so that column is not a link.
- Volume Over Time points and ≥44px day links open Documents for that **date bucket** (`from`/`to` = that day) plus Client. Documents list now honors `from`/`to` (same CreatedAt window as dashboard). Empty filters show the Documents empty state. Mask A unchanged. No EF migration.

## 2026-09-09 — Phase 4.2.4 no-store on Swagger index.html
- Swashbuckle served `/swagger/index.html` (and `index.js`) with `Cache-Control: max-age=604800, private`. QA2 could keep a 7-day stale shell while authorize.js was already no-store, so `__deedAiMeasureAuthorize` stayed undefined.
- `/swagger` middleware now sets `Cache-Control: no-store` on those shell responses via `Response.OnStarting` (same contract as authorize.js/css). Pin-last-only 4.2.3 is unchanged: single JS after `index.js`, HeadContent CSS only, no `InjectJavascript`.

## 2026-09-09 — Phase 4.7 Dashboard print + export PDF
- Dashboard **Print** uses print CSS (no sidebar / top bar / footer) and prints the filtered count cards plus visible Chart.js charts.
- **Export PDF** uses the same date + Client filters as `GET /api/dashboard/counts` and `/api/dashboard/charts/*`. Filename includes the date range. Role / ClientAccess gated. Empty or failed export is a clear error, not a blank file. `{Client} Deed AI` title when a single Client is in scope; page numbers and generated timestamp. No secrets; Client / Software only.

## 2026-09-09 — Phase 4.5 follow-up: profile Clients + Systems label
- My profile shows the signed-in user's assigned Client(s) from `/api/auth/me` identity (existing Client access). Empty state when none. Client wording only.
- Nested Settings nav item is **Systems** (top-level Settings group, `/settings`, and Admin gate unchanged).

## 2026-09-09 — Phase 4.2.3 Authorize pin-last-only
- Live QA2: `window.__deedAiMeasureAuthorize` was undefined and rects stayed ~34 / ~30 even though `/swagger/deedai-swagger-authorize.js` 200'd. Root cause: custom `index.html` plus HeadContent / InjectJavascript triple-loaded the runtime; the helper is assigned late and pin-last early-returned on a messy `__deedAiAuthorizeRuntime` flag.
- Single load only: CSS link in head, authorize JS once after `index.js`. No inline full-runtime dump. No `InjectJavascript` / `InjectStylesheet`. Helper is always assigned (even on re-entry); init is try/catch and sets `document.documentElement.dataset.deedaiAuthorizeError` on failure. Version `4.2.3`.

## 2026-09-09 — Phase 4.9 system health probes + queue visibility
- Admin `GET /api/health/detail` and Settings System health add Blob R/W (Pass/Fail), Document Intelligence reachability + configured, OCR pipeline (queue + worker heartbeat/dequeue), and queue visibility (depth, oldest waiting age, poison / Failed, last DI success/fail). Existing SQL / Storage / Queue remain. No secrets. No Azure deploy.

## 2026-09-09 — Phase 4.2.2 Authorize hit-target persist
- Live Azure after `#18` still measured ~34px (top bar) / ~30px (modal). Root cause: Swashbuckle 9 puts `HeadContent` / `InjectJavascript` in `<head>` before `swagger-ui-bundle.js`, and Swagger React re-applies `display:inline` after the 4s pin (height is ignored on inline). Custom `index.html` now loads `/swagger/deedai-swagger-authorize.js` **last**; a 250ms poll + attribute observer re-pins `height/min-height:44px; max-height:none; display:inline-flex !important`.
- Dev must self-verify after zipdeploy before pinging QA2: on `/swagger`, `window.__deedAiMeasureAuthorize()` — every `getBoundingClientRect()` width/height ≥ 44; `html[data-deedai-authorize-hit=pass]`; `window.__deedAiAuthorizeRuntimeVersion === "4.2.2"`.

## 2026-09-09 — Phase 4.5 Users / shell identity
- Users list groups by Client (multi-Client users appear in each assigned group; groups collapse/expand).
- Profile photo in blob storage (upload / replace / clear with ConfirmSheet). Shown in the header and Users. Initials fallback. JPEG/PNG/WebP/GIF, 2 MB.
- Header title is `{Client} Deed AI` only for a single-Client effective scope; otherwise **Deed AI**.
- Left shell: Logged in as {name/email} + My profile (≥44px) for name, full name, photo, and password change.
- Full name under Display name (required on create). Confirm new password with Identity rules and inline mismatch.
- EF migration `20260909160000_Phase45UsersIdentity` (Designer-first). Client / Software naming only.

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
- Phase 4A / Phase 4AQa now have `*.Designer.cs` files (`[Migration]` + `[DbContext]` + `BuildTargetModel`) so Azure `MigrateAsync` actually applies AppPolicies, field maps, SoftwareClientConfigs, SalesTabCodes, and `Documents.SalesTabCode`. PropertyDefaults was later dropped in Phase 4.9.1.
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
