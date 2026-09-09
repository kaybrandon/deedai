# SOP-04 — Deed AI Phase 1 field / staff quick start

**Live:** https://appdeedai-bdfvbng5ckhgfzcp.southcentralus-01.azurewebsites.net  
**Audience:** BIS staff (Admin / Editor / Uploader / Viewer)

## Login
1. Open the live URL
2. Sign in with the account your Admin created (seed Admin email is `admin@bisconsultants.com` — password from KV/ops only)
3. Role-denied screens show a clear empty/disabled state (not a silent failure)

## Documents (Phase 1)
1. Open **Documents** — status chips: Queued / Processing / Ready / Failed. A Ready deed with the **Needs review** flag shows **Needs review** (not both).
2. **Retry** on Failed when available
3. **Upload** deed PDF (type/size limits apply)
4. Open a deed — Ready demo deeds show a PDF preview while editing. Review/edit mapped fields; save keeps draft; **Retry** on save fail. Placeholder only if no PDF exists.
5. Soft-delete uses a confirm sheet (ConfirmSheet)
6. List actions are large enough for tablet (≥44px)

## Swagger Authorize (Admin QA2)
When **Enable Swagger UI** is on, open `/swagger`, wait for the UI to paint, then in DevTools:
`window.__deedAiMeasureAuthorize()`
Top-bar Authorize and the modal Authorize / Logout / Close must each report `width` and `height` ≥ 44. Pass marker: `document.documentElement.dataset.deedaiAuthorizeHit === "pass"`. Measuring the inner lock icon or label span is the wrong node — use the helper (it reads `getBoundingClientRect()` on the buttons).

## OCR
- Upload → blob → queue → Document Intelligence → fields on the deed
- Failures show as **Failed** with Retry — not a silent hang

## Naming
- Filter/scope by **Client** (not County)
- External system features (later phases) say **Software** (not CAMA)

## Not in Phase 1 (yet)
Users admin UI · Settings CRUD · Reports CSV/Excel/PDF · Software lookup/push · full forgot-password email — see Phase 2/3 after Azure Pass.

## Escalation
Site down / login → ops/Dev · Wrong fields/scope → BA · Client-facing wording → Brandon via Chief of Staff
