# SOP-04 — Deed AI staff quick start (Phase 1–2)

**Live:** https://appdeedai-bdfvbng5ckhgfzcp.southcentralus-01.azurewebsites.net  
**Audience:** BIS staff (Admin / Editor / Uploader / Viewer)

## Login
1. Open the live URL
2. Sign in with the account your Admin created (seed Admin email is `admin@bisconsultants.com` — password from KV/ops only)
3. Role-denied screens show a clear empty/disabled state (not a silent failure)
4. **Forgot password:** enter email → always see a generic success (does not reveal whether the account exists)

## Documents (Phase 1)
1. Open **Documents** — status chips: Queued / Processing / Ready / Failed
2. **Retry** on Failed when available
3. **Upload** deed PDF (type/size limits apply)
4. Open a deed — review/edit mapped fields; save keeps draft; **Retry** on save fail
5. Soft-delete uses a confirm sheet (ConfirmSheet)
6. List actions are large enough for tablet (≥44px)

### Demo deeds (testing)
Four sample deeds are on live Azure for walks: Ready · Failed+Retry · Processing · Queued. Dashboard should show **4 / 1 / 1 / 1 / 1**.

## OCR
- Upload → blob → queue → Document Intelligence → fields on the deed
- Failures show as **Failed** with Retry — not a silent hang

## Users (Phase 2 · Admin)
1. Open **Users** (Admin only — Viewer gets 403 / denied)
2. List users · add / disable / re-enable as needed
3. Role changes follow Admin / Editor / Uploader / Viewer

## Settings (Phase 2 · Admin)
1. Open **Settings**
2. Manage flags / statuses / deed types
3. Export CSV when you need a snapshot

## Reports (Phase 2)
1. Open **Reports**
2. Run / download available exports (CSV / Excel / PDF as shipped)
3. Empty list is OK when no report data yet

## Naming
- Filter/scope by **Client** (not County)
- External system features (later phases) say **Software** (not CAMA)

## Not in Phase 2 (yet)
Software lookup/push · full Phase 3 scope — see Phase 3 after Azure Pass.

## Escalation
Site down / login → ops/Dev · Wrong fields/scope → BA · Client-facing wording → Brandon via Chief of Staff
