# Phase 7 — GIS Dashboard visual chrome

Visual system only. SoT: Brandon PASS screenshot `04-gis-pass-brandon.png` + live GIS Dashboard (`appgisdashboard`, CSS `:root`).

**Hard lock:** dark navy/charcoal sider is Must. White/light rail is Fail. Brandon FAIL screenshot `03-deedai-fail-brandon.png` (pale sider · teal header · gold Export as shell) must not ship.

Does **not** port GIS product IA, work-item fields, roles, shapefile, Telerik, or GIS jargon.

## Tokens (from live GIS CSS, documented)

| Role | Hex | Source |
|---|---|---|
| Sider / rail | `#001529` | `--mask-sider` |
| Sider menu nest | `#000c17` | `--mask-sider-menu` |
| Active nav / primary | `#1890FF` | `--mask-primary` (PASS blue highlight) |
| Active hover | `#40A9FF` | `--mask-primary-hover` |
| Utility header | `#FFFFFF` | `--mask-header` |
| Canvas | `#F0F2F5` | `--mask-bg` |
| Accent (charts / status only) | `#0D8A7F` | Mask F teal — not shell |
| Export chip (page actions) | `#E8C547` | kept on Export buttons, not sider |
| Navy token (legacy) | `#1E2430` | Mask F — not the live sider |

`#3B82F6` remains as `--accent` / `--blue`. Shell active nav uses live GIS `#1890FF`.

## Must

1. **Shell chrome (GIS PASS)**
   - Full-height **dark sider** `#001529` with white brand + sentence-case nav.
   - Active nav: full-width vibrant blue `#1890FF` (not mint/teal wash).
   - Slim **white** utility header over the canvas (PASS), not a teal-band-only Mask F shell.
   - **Powered By BIS Consultants** footer on every shell page.
2. **Density**
   - Documents rows **32px** (`--table-row-h`). Keep 6.1 ~8–12 row scroll pane + sticky thead.
   - Forms + dashboard cards hug GIS packing.
3. **Status-first**
   - Settings opens on Statuses; Documents Status filter first.
   - OCR ribbon = stage only. One catalog Status toolbar. Row chip catalog-only.
4. **Review**
   - Keep 3-col IA, compact title band, primary Save, gold Export class on the page action.
5. **AdminSeed / QA demo on `/login`**
   - Visible panel: `admin@bisconsultants.com` + current AdminSeed password. Client workspace copy.

## Carry

Phase 6 AI extract · 6.1 Documents Musts · Settings→System nest · no `?` pills · no glass/radial-gradient · ConfirmSheet-only drop shadows · Client/Software only.

## Won’t

GIS roles/org · shapefile · work-item fields · County/CAMA/Work Items copy · dual theme · white/light rail.

## Fail if

- White, light gray, or pale mint sider ships.
- Deed AI still reads as FAIL screenshot (pale sider + teal header as the whole chrome story).
- Documents row height regresses to ~53px.
- Documents status chrome fight returns.
- County / CAMA / Work Items jargon in UI copy.
