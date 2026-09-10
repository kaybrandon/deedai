# Phase 6.1 — Documents list UX delta

Visual SoT: Mask F Documents (`03-documents.png` from F-mockitt-admin). GREENLIT after Phase 6 Azure Pass. Client/Software only. Does **not** reopen Phase 6 AI extract.

## Must

1. **Mask F density** — hug rows · sticky header · spacing 4/8/12 · no sparse empty canvas above/below table.
2. **Constrain table height** — sticky thead + ~8–12 hug rows in a **scrolling tbody pane**; filters + OCR ribbon stay fixed; do not let the grid stretch full browser height as the only UI.
3. **Unify status chrome (ribbon vs catalog — UX lock)**
   - **OCR ribbon** = pipeline *stage* filter only (Upload → Queued → Processing → Review → Ready) — **not** a second status vocabulary.
   - **One catalog Status control** in the toolbar — 5.2.5 values only: Complete · In Queue · Needs Work · New · Not Needed · Pending · Research · Upload Error.
   - **Row chip = catalog status only** (one chip) — do **not** also show pipeline pills on the row.
   - Drop/replace any Status dropdown that mixes Ready/Failed/Review with catalog values — that is the “pill + dropdown fight”.
4. **Carry 5.2.2 columns** — sortable **Volume · Page · Type · PID · Doc #** (+ Status/Client/Assignee/Updated as needed); one ≤360px search · ConfirmSheet soft-delete · chart deep links · Settings→System nest · no `?`/CAMA.

## Should

- Compact filter row so catalog Status + filters share one toolbar.
- Table pane **horizontal scroll** on narrow viewports so Actions stay reachable with many columns.

## Won’t

- Dropping Statuses catalog · using ribbon as catalog status · reopening Phase 6 AI extract · glass/gradients · full-width search.

## Fail if

- Table still full-bleed dominant with no internal scroll / sticky thead.
- Pipeline pill **and** catalog dropdown both act as competing row/toolbar status UIs.
- Row shows pipeline pills alongside catalog chips.
- Catalog assign/filter Musts regress · Vol/Page/Type/PID/Doc # columns missing · CAMA/County.

## QA smoke

Documents: ribbon stage filter only · one catalog Status control · row chip catalog-only · table scrolls ~8–12 rows · Vol/Page/Type/PID/Doc # sort · set Needs Work · filter · Mask F density.
