# Phase 5.2.3 — Software settings depth

Visual SoT: Mask F Software (`08-settings-software.png`) — form ~680px · instances full width · no FieldHelp `?` pills.

## Must

1. **Image codes:** Admin view/edit Software image-code settings for lookup/push; persist per Software instance · Client-scoped.
2. **Grantee combiner:** setting for how multiple grantees combine for Software push/search. Label **Grantee**, never CAMA.
3. **Certified / default year:** certified year + default year on Software settings; persist · validate a sensible year range · used by lookup/push where applicable.
4. **Fuller field maps (Harris+):** expose remaining typed field-map depth that was still Partial vs legacy (beyond Vendor / URL / keyConfigured / Group / zeros / Sales Tab / Send Consideration / date depth already Present) so Harris-class maps are not thinner than old Software settings for configured keys. New code must **not** use `cama*` API names — Software labels only.
5. **Carry:** six push resets · role-gated Push / lookup / retry / last-sync · Property defaults stay removed · nest Settings → System → Software · Mask F density · no FieldHelp `?` pills · KV-only secrets · no secrets in UI · Designer-first migrations · null-safe defaults on new cols (lesson from #35/#36 — backfill + SQL defaults required to avoid 500.30).

## Should

Sales ratio / finance / instrument code triad on the Client Software instance. Deep Harris AdvancedSearch only if capacity (year + image code are sent on lookup; no vendor SOAP client in this slice).

## Won’t

County / CAMA · Super Admin · Property defaults · AI extract · Delete policy / Statuses (5.2.4 / 5.2.5).

## Chrome

Form hugs ~680px (`--detail-w`). Software Instances table uses remaining page width. Help is native `title` / `aria-describedby` only.

## Hard gates

EF migration `20260910020000_Phase523SoftwareDepth` is Designer-first (`[Migration]` + `[DbContext]` + `BuildTargetModel`) and follows `20260910010000_DeletePolicy`. New string columns backfill NULLs and add SQL defaults. No Azure deploy.
