# Phase 5.1.1 — Users searchable sortable table

Visual SoT: `05-settings-users.png` (Settings · Users density). Mask F (#32) is on `main`; keep its System nest and tokens. Do not take Volume weekly (#34).

## Must

1. **Real data table** (not a card stack). Hug rows, sticky header, ≥44px. Use Mask F density tokens when present (`--table-row-h`, `--table-cell-pad-y`, `--table-header-bg`, `--table-stripe`).
2. **One filter-row search** ≤360px across display name, full name, and email. No top-bar search.
3. **Column heading sort** asc/desc with a clear affordance: Display Name · Email · Role · Client(s) · Status.
4. **Per-column filters** on Role, Client, and Status (enabled/disabled).
5. **Carry 4.5:** Client(s) column lists every assignment. ConfirmSheet disable stays. Add/edit photo and password stay. Phase 4.8 verification / Resend verify stay.
6. **Admin-only Users.** Client / Software only. Never County / CAMA.

## Should

- URL and `sessionStorage` persist sort/filter.
- Empty: **No Users Yet**. Filtered empty: **No Users Match**.
- Paginate around 50 rows.

## Won’t

- In-cell edit, CSV export, replacing add/edit drawers, relitigating photo/password.
- Volume Over Time weekly bars. Do not flatten Mask F’s System nest.

## Nav

- Settings → **System** (singular mid-parent) → indented Software · Users · API.
- Never **Systems** plural. Never System as a sibling peer of Software.
- Users stays Admin-only.

## Fail if

- Card-only list · no search or duplicate search · columns not sortable/filterable · County/CAMA · new EF migration · Azure deploy.
