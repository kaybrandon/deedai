# Phase 7 — GIS Dashboard visual chrome

Visual system only. SoT: live GIS Work Items grid + detail (teal header band, **light** rail with teal labels, gold export chip, dense table / field stacks, Powered By BIS Consultants). Client/Software naming only.

Does **not** port GIS product IA, work-item fields, roles, shapefile, Telerik, or GIS jargon.

## Must

1. **Shell chrome (GIS-like)**
   - Strong header bar: solid Mask F teal `#0D8A7F` band (live GIS cyan-teal role) with product title + primary actions.
   - Sidebar: denser **light rail with teal small-cap labels** (status-bucket feel). Not a dark rail.
   - **Powered By** footer parity with GIS (BIS Consultants).
2. **Density**
   - Forms + tables hug content (GIS-level packing). Less empty canvas than post–Mask F soft padding.
   - Documents table keeps Phase 6.1 scroll pane (~8–12 visible rows, sticky thead) with GIS-dense row height (`32px`) / heavier headers.
3. **Status-first settings / list feel**
   - Settings opens on Statuses; Documents Status filter is first.
   - OCR ribbon = **stage filter only** (Upload · Queued · Processing · Review · Ready).
   - **One** catalog Status toolbar (no Ready/Failed mix in that select).
   - Row chip catalog-only (no pipeline-pill + mixed dropdown fight).
4. **Work-item / detail density cues**
   - Review keeps **3-col** IA. Compact teal title band, tight field stacks, primary Save, gold Export.
5. **Tokens**
   - Primary accent stays Mask F teal `#0D8A7F` (no GIS teal hex override).
   - Gold `#E8C547` for primary export chips (GIS Export To Excel role).
   - Client/Software only · no County/CAMA · no GIS domain copy.

## Carry

Phase 6 AI extract path · 6.1 Documents UX Musts · Settings→System nest (singular) · no `?` pills · no glass/gradients/radial-gradient · no drop shadows except ConfirmSheet · health 200 after deploy.

## Won’t

GIS roles/org · shapefile · GIS work-item fields · drag-column grouping · pixel-clone Telerik · relitigate AI extract / Statuses catalog values / Super Admin / ads · dual theme mess.

## Fail if

- Dark sidebar returns.
- Documents status chrome fight returns (ribbon as catalog, Ready/Failed in the catalog select, pipeline + catalog chips on the row).
- County / CAMA / Work Items / CAD jargon in Deed AI UI copy.
- Primary accent silently replaced without a PR note.
