# Phase 5.2.4 — Delete Policy

GREENLIT · Client/Software naming only (never County/CAMA).

## Must

1. **Policy setting (Admin-only)** — persist who may soft-delete documents:
   - **All Editors** (Admin + Editor roles may soft-delete)
   - **Admin only** (Uploader/Viewer never; Editor denied)
2. **Settings UI** — Admin can view/change policy under **Settings → System** (label **Delete Policy** or **Who Can Delete** — never CAMA/Delete Deed legacy jargon unless Title Case product copy needs “Delete Documents”)
3. **Enforce on API + UI**
   - Soft-delete action hidden/disabled when role below policy
   - API returns **403** when role not allowed (not 404 that hides auth)
   - ConfirmSheet still required when allowed (≥44px)
4. **Restore** — restore-from-Settings (or existing restore path) stays Admin-gated as today unless already documented otherwise; policy change must not break restore
5. **Carry** — 4 roles only · Client/Software · Mask F density · no secrets · Designer-first EF migrations · **null-safe defaults on any new columns** (lesson from #35/#36 HTTP 500.30 — backfill NULLs + DEFAULT · null-safe materialize) · health 200 after deploy

## Should (if capacity)

- Audit/log who changed the policy (lightweight)
- Show current policy as read-only hint near Documents delete control for Editors

## Won’t

- Super Admin matrix · hard-delete · County/CAMA · Statuses catalog (5.2.5) · Property defaults

## Fail if

- No Settings control for All Editors vs Admin only
- Editor can soft-delete when policy is Admin only (UI or API)
- ConfirmSheet removed · 403 missing on unauthorized API delete
- Super Admin / CAMA wording

## QA smoke target

Admin sets **Admin only** → Editor soft-delete hidden + API 403 · Admin still ConfirmSheet deletes · switch to **All Editors** → Editor ConfirmSheet works · Viewer never · reload persists · health 200
