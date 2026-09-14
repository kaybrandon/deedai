# Phase 7 — GIS Dashboard visual chrome

Visual system only. SoT: attached `gis-chrome-sot/01-gis-grid.png` + `02-gis-detail.png` and live GIS Work Items chrome (full-width teal header, **light gray** rail with teal small-cap labels, gold export chip, dense table / field stacks, obvious Powered By BIS Consultants). Client/Software naming only.

Does **not** port GIS product IA, work-item fields, roles, shapefile, Telerik, or GIS jargon.

Primary accent stays Mask F teal `#0D8A7F` (live GIS Ant primary `#1890ff` is **not** adopted). Header teal matches the SoT band, not GIS blue.

## Must

1. **Shell chrome (GIS SoT)**
   - Full-width solid teal header `#0D8A7F` **above** the rail (product title + gold role pill). Not a sider-first Mask F wordmark column.
   - Sidebar: light gray rail `#F0F2F5` with teal small-cap labels. No pale mint/soft-green hover wash. No dark rail.
   - **Powered By BIS Consultants** footer obvious on every shell page (teal rule, not a whisper).
2. **Density**
   - Forms + tables hug content (GIS packing). Kill Mask F soft empty canvas and airy page titles.
   - Documents table keeps Phase 6.1 scroll pane (~8–12 visible rows, sticky thead) with GIS-dense row height (`32px`) / heavier headers.
3. **Status-first settings / list feel**
   - Settings opens on Statuses; Documents Status filter is first.
   - OCR ribbon = **stage filter only** (Upload · Queued · Processing · Review · Ready).
   - **One** catalog Status toolbar (no Ready/Failed mix in that select).
   - Row chip catalog-only (no pipeline-pill + mixed dropdown fight).
4. **Work-item / detail density cues**
   - Review keeps **3-col** IA. Compact teal title band, tight field stacks, primary Save, gold Export.
5. **Tokens**
   - Primary accent stays Mask F teal `#0D8A7F` (no GIS blue `#1890ff` override).
   - Gold `#E8C547` for primary export chips (GIS Export To Excel role).
   - Client/Software only · no County/CAMA · no GIS domain copy.
6. **AdminSeed / QA demo on `/login`**
   - Visible panel: email `admin@bisconsultants.com`, password the current AdminSeed seed. Label **AdminSeed / QA demo**.

## Carry

Phase 6 AI extract path · 6.1 Documents UX Musts · Settings→System nest (singular) · no `?` pills · no glass/gradients/radial-gradient · no drop shadows except ConfirmSheet · health 200 after deploy.

## Won’t

GIS roles/org · shapefile · GIS work-item fields · drag-column grouping · pixel-clone Telerik · relitigate AI extract / Statuses catalog values / Super Admin / ads · dual theme mess · dark sider.

## Fail if

- Deed AI still reads as sparse Mask F soft shell (pale mint sider, DEED AI sider wordmark, airy padding, header only in the main column).
- Dark sidebar returns.
- Documents status chrome fight returns (ribbon as catalog, Ready/Failed in the catalog select, pipeline + catalog chips on the row).
- County / CAMA / Work Items / CAD jargon in Deed AI UI copy.
- Primary accent silently replaced without a PR note.
