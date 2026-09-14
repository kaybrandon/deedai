# Phase 7 — GIS Dashboard shell HTML + CSS (1:1 chrome)

Visual system only. SoT: Brandon PASS screenshot `04-gis-pass-brandon.png` + live GIS Dashboard (`appgisdashboard` CSS `:root` + shell markup).

**Hard lock:** Brandon after #44 (`05-deedai-live-brandon-still-wrong.png`) still failed because tokens changed while the **shell HTML stayed DeedAi**. This pass ports GIS **shell CSS + HTML structure** — not another inspired-by theme.

Does **not** port GIS product IA, work-item fields, roles, shapefile, Telerik, or GIS jargon.

## FAIL → PASS (live #44 vs GIS)

| FAIL after #44 | PASS (GIS) now in Deed AI |
|---|---|
| Second **Deed AI** title band in the header + gold Export as the primary chrome cue | Product mark + title live in the **sider top** only. Header is GIS utility chrome: hamburger · `BIS Consultants · {Client}` · bell · profile. Export PDF is a small ghost in `filter-actions`, not gold shell identity. |
| Soft SaaS metric cards (label + number) | GIS `kpi-card` + `ant-statistic` with colored icon chips |
| Airy filter / page-head / Print+gold Export rhythm | GIS `filter-toolbar` (fields left, actions right) matching Download PDF / Send report placement |
| Chart cards still padded SaaS panels | GIS `ant-card` / `compact-card` / `chart-card` (40px head, 12px body, `#f0f0f0` border, 2px radius, no drop shadow) |
| Profile / Sign Out parked in the sider footer | GIS header user menu (View/My Profile + Sign Out). Sider is brand + nav only. |
| Teal/gold remnants as shell identity | Live GIS tokens: sider `#001529`, active `#1890FF`, header `#fff`, canvas `#f0f2f5`, radius `2px` |

## Tokens (from live GIS CSS `:root`)

| Role | Hex | Source |
|---|---|---|
| Sider / rail | `#001529` | `--mask-sider` |
| Sider menu nest | `#000c17` | `--mask-sider-menu` |
| Active nav / primary | `#1890FF` | `--mask-primary` |
| Active hover | `#40A9FF` | `--mask-primary-hover` |
| Utility header | `#FFFFFF` | `--mask-header` |
| Canvas | `#F0F2F5` | `--mask-bg` |
| Card / border / radius | `#fff` / `#f0f0f0` / `2px` | `--mask-card` `--mask-border` `--mask-radius` |
| Accent (charts / status only) | `#0D8A7F` | Mask F teal — not shell |
| Export chip (Review / Settings / Reports only) | `#E8C547` | not sider, not dashboard chrome |

## Shell HTML (ported from live GIS `app-shell`)

```
.app-shell.ant-layout
  aside.ant-layout-sider.sidebar#app-sidebar
    .brand > .brand-mark + title
    .ant-menu.ant-menu-dark.ant-menu-inline
  .ant-layout.shell-body
    header.app-header.topbar
      .app-header-left (hamburger + .app-header-brand)
      .app-header-right (bell + .app-header-user)
    .content-wrap
    footer.app-footer > .powered-by
```

Dashboard chrome: `.page-title` + `.compact-card` presence + `.filter-toolbar` + `.kpi-card` + `.chart-card.ant-card`.

## Must

1. **Shell chrome (GIS PASS)**
   - Full-height **dark sider** `#001529` width 220, brand 64px, Ant dark menu, full-width blue `#1890FF` active.
   - Slim **white** utility header (hamburger · Client breadcrumb · bell · profile). No second product title band.
   - **Powered By BIS Consultants** footer on every shell page.
2. **Density**
   - Documents rows **32px**. Keep 6.1 ~8–12 row scroll pane + sticky thead.
   - Dashboard cards / filters / chart heads match GIS packing (8px gutters, 2px radius).
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

GIS roles/org · shapefile · work-item fields · County/CAMA/Work Items copy · dual theme · white/light rail · a third invented theme.

## Fail if

- White, light gray, or pale mint sider ships.
- Header still reads as a Deed AI title band + gold Export as the chrome story.
- Dashboard cards/filters still read as Mask F soft SaaS.
- Documents row height regresses to ~53px.
- County / CAMA / Work Items jargon in UI copy.
