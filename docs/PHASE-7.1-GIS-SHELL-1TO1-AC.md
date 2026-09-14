# Phase 7.1 — GIS shell CSS/HTML 1:1

BA / CoS lock. SoT = live GIS Dashboard CSS bundle `index-Dnm45b_t.css` + PASS `04-gis-pass-brandon.png`.

#44 Azure Pass is **held**. Token-only restyles Fail. This phase ships the live GIS custom CSS **verbatim** plus Ant 5 layout/menu/card rules GIS applies via cssinjs.

## Must

1. **Same CSS/HTML for the shell** — not a third skin.
   - `spa/src/gis-shell.css` is the live GIS custom sheet (`:root --mask-*`, `.app-shell`, `.app-header`, `.filter-toolbar`, `.kpi-card`, `.content-wrap`, `.powered-by`).
   - `spa/src/gis-ant-layout.css` is the Ant 5 layout/menu/card/statistic/button density GIS uses (`siderBg #001529`, selected `#1890ff`, header 64px, card radius 2px).
   - AppShell DOM: `.app-shell.ant-layout` → `aside.ant-layout-sider` (brand + dark inline menu) → `.ant-layout` → `header.app-header` → `.content-wrap` → `footer.app-footer`.
2. **Chrome**
   - Dark sider `#001529`, full-width active `#1890FF`, white utility header (hamburger · `BIS Consultants · {Client}` · bell · profile).
   - Product mark + title in the sider top only. No second Deed AI title band. No gold Export as shell identity.
3. **Density**
   - Documents / Users table rows **~32px**. 53px is Fail.
   - Filter toolbar + kpi/chart cards match GIS packing.
4. **Deed AI IA**
   - Dashboard / Documents / Upload / Reports / Sales / Restore / Settings→System.
   - Client / Software only. No County / CAMA / Work Items copy.
5. **Carry**
   - Phase 6 AI extract · 6.1 status rules (ribbon stage-only · one catalog Status · catalog chips).
   - AdminSeed Test login on `/login`.
   - Gold Export stays on Review / Settings / Reports page actions only.

## Won’t

GIS roles/org · shapefile · work-item fields · dual theme · inventing a third palette.

## Fail if

- Shell still reads as DeedAi soft SaaS + gold Export / title band.
- `gis-shell.css` is missing the live `--mask-sider` / `--mask-primary` / `.app-header` / `.filter-toolbar` / `.kpi-card` strings.
- Table rows regress to ~53px.
- County / CAMA / Work Items jargon in UI copy.
