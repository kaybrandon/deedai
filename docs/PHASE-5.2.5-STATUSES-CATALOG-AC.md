# Phase 5.2.5 — Statuses catalog parity

Visual SoT: Mask F Settings → System. Catalog labels are Client/Software only.

## Must

1. **Statuses catalog (Admin)** — Settings → System → Statuses lists and manages statuses. Seed these labels if missing:
   - **Complete** · **In Queue** · **Needs Work** · **New** · **Not Needed** · **Pending** · **Research** · **Upload Error**
2. **Map ↔ pipeline** — each catalog status maps to an internal pipeline or review state (or a documented extension). OCR stays Queued / Processing / Ready / Failed on `Document.Status`. The ribbon is not replaced.
3. **Assign on Documents / Review** — Editors and Admins set catalog status (role-gated). Documents list filters include catalog statuses. Chips use catalog display names.
4. **CRUD** — Admin can add, rename, and soft-disable. System / seed Must entries cannot be hard-deleted (disable is OK). ConfirmSheet on destructive disable/delete. Controls ≥44px.
5. **Carry** — Client/Software only · no County/CAMA · Mask F · Designer-first + **null-safe defaults on new columns** (backfill NULLs + DEFAULT · null-safe materialize) · health 200 · Title Case labels.

## Map (documented)

| Catalog | Code | Maps to | Kind |
| --- | --- | --- | --- |
| New | New | New | Review extension |
| In Queue | InQueue | Queued | Pipeline alias |
| Pending | Pending | Processing | Pipeline alias |
| Needs Work | NeedsWork | NeedsReview | Review |
| Research | Research | Research | Review extension |
| Complete | Complete | Ready | Review (Approved-like) |
| Not Needed | NotNeeded | NotNeeded | Review extension |
| Upload Error | UploadError | Failed | Pipeline alias |

Assigning a catalog status writes `ReviewStatus` only. It never changes OCR `Document.Status`.

## Should

- Sort/order statuses in Settings (sort-order field).
- Color / chip token per status (Mask F palette).
- Document status history (DocumentLog) — **parked**.

## Won’t

Super Admin · Flags catalog · Deed-type mapping table · replacing the OCR pipeline with free-text-only statuses.

## Fail if

- Must status labels missing from Settings catalog
- Assigning catalog status breaks the OCR ribbon / Ready + OCR-failed rule
- Documents cannot filter by catalog status
- CAMA / County · hard-delete of in-use system statuses · HTTP 500.30 after migration
