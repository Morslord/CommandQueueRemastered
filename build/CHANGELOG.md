## VERSION 0.4.1

- Place LimiterItemCount directly above BlockGameplayInput in RiskOfOptions General settings.

- Add independent personal IgnoreTemporaryItems/IgnoreEtherealItems switches (default off), separate from the three Behaviour choices. Exclude opted-out recipients from all distribution algorithms and proximity auto-opening while retaining manual Behaviour. Sync each client's own flags to the host with connection validation and expiry; host preferences affect only the host's own player. With no eligible recipient, preserve the original drop location.

- Skip queued items absent or unavailable in temporary/ethereal Command pools, selecting the next eligible item of the same tier without removing skipped entries. Preserve Pause, Limiter, consumption and both Repeat modes; permanent essence selection retains its usual head rule.
- Add one Item Distribution Rules / DrifterSalvage dropdown: Original keeps the skill location; SkillUser (default) keeps each caster's drops together; ItemDistribution uses the per-tier mode. Migrate previous checkbox/target settings automatically. Master/stage rules and personal opt-outs still apply. Other temporary sources remain independent.

- Add a separate Limiter view with one whole stack per item, independent per-group Limiter On/Off, insertion-order storage and Sort by ID. Limiter drag operations always move whole stacks; Queue retains its split-stack controls.
- Display owned/limit counts in the item selection grid, with LimiterItemCount choosing permanent-only, temporary, ethereal or both special stack types. Mark active limits with a thin silver-gray bottom strip in Queue view. Align Limiter and Void strips to screen pixels, rounding fractional heights down with a one-pixel minimum to keep thin markers consistent between rows. Dim disabled limits and gray out capped queued items. Skip capped entries without consuming or reordering them; make them eligible again after inventory loss. Limiter targets do not select items absent from the queue.
- Save limiter targets and enabled states with presets and supported ProperSave data. Show a combined preset preview with summed queue quantity/limit badges and gray limiter-only items. Continue reading older queue documents. Load Queue and Limiter groups independently so limiter-only presets preserve the Queue and queue-only presets preserve limits. Order previews by tier, with queued items before limiter-only items.
- Add Item Distribution Rules with a master switch, named stage checklist, chest/exchange-pool/teleporter-reward switches and optional Hex blocks of seven. Bazaar Between Time and Computational Exchange are excluded by default; existing additional exclusions are retained. Source exclusions retain original drop locations. Exclude test scenes and combine duplicate Bulwark's Ambry entries.
- Fix FewestItems burst accounting by including outstanding permanent assignments until collection, transfer completion or cancellation. Respect AllItems/SameTier and retain shared pickup access.
- Display distribution options as Hallowed, Hex, Pages in that order while preserving saved configuration keys. Clarify host authority and per-tier tie rotation in FewestItemsCount.
- Arrange six equal-width action buttons in two groups: Queue, Limiter, Repeat/Limiter On/Off on the left; Pause, Clear Group, Clear All on the right. Keep Pause separate from Limiter state.
- Show Groups, General and Special Items on the first RiskOfOptions page; Item Distribution Rules then Item Distribution on the second page. Other mods retain native category paging. New UI, native source and reservation hooks still require in-game validation; custom mod drop paths may bypass source detection.

- Allow matching queued Command essences to open while viewing the scoreboard, retaining guards for other menus, dialogs, typing, dragging and active interactions. Clarify that ExclusiveItemSelection Automatic does not open essences without a matching queue entry.

## VERSION 0.4.0

- Add per-tier Command essence distribution: Original (default), RoundRobin, Killer and FewestItems, with independent Void tiers. Missing eligible killers fall back to round robin; with no eligible players, drops retain their original location.
- Add FewestItemsCount: count permanent item stacks across all items or only the matching item tier. Ties rotate between players.
- Add AutoPickupCommandEssence (default off, General): automatically open nearby unobstructed essences only when an active queue contains an available matching entry. Respect interaction range, menus, pause and special-item settings.
- Add AutoCollectQueuedItems (default off, General): collect items selected using a queue entry, preserving pickup status and Repeat rules. Multiplayer requires support and activation on both host and selecting client.
- Add ExcludePausedPlayers (default off, General): exclude players whose matching queue group is paused from distribution. Updated clients report pause states; unknown or stale states remain eligible.
- Queue complete assigned-drop data and create essences at the recipient's current position, limited to 30 per second with at least 1/30 second between deliveries and no catch-up bursts. Bound automatic pickup processing during large drop waves and use current essence geometry for proximity and line-of-sight checks.
- Integrate distribution and collection settings with RiskOfOptions, using Item Distribution, Special Items, consistent White/Green/Red and Void rarity labels, and readable description paragraphs.
- Format preset files with one compact preset per line, preserving contents and legacy import status.

## VERSION 0.3.4

- Add BlockGameplayInput in General settings and RiskOfOptions with three choices: On (default, block both scoreboard views), CommandQueueOnly (block Command Queue while allowing gameplay in Players), and Off (allow gameplay in both views). Changes and view switches apply immediately.

## VERSION 0.3.3

- Add a Pause button between Repeat and Clear group for the currently selected group. Each group can be paused independently; the button turns orange-yellow when that group is paused. Essences for paused groups open normal Command menus without consuming the queue or advancing Repeat. Other groups continue automatically. The shared Void tab pauses all Void tiers together. Group pauses reset at run start/end and are not stored in presets or ProperSave.
- Reorder presets by dragging their rows. Show a cursor label and insertion line, scroll at list edges, and save the new order immediately. Preserve preset contents, IDs, selection and preview. Drop outside the list or press Escape to cancel.
- Fix queued selection of Halcyon Seed, Newly Hatched Zoea, Exposed Cerebellum, Encrypted Cerebellum, Prison Matrix and Sentry Key from exclusive Command essences, regardless of queue position. Add ExclusiveItemSelection: Default requires a queued entry; Automatic also selects without one. Respect Pause, enabled groups and temporary/ethereal policies. Existing entries follow consumption/Repeat rules; no automatic collection.
- Prevent queued exclusive items from blocking later normal Boss items when the exclusive items are not offered by that essence. Keep the exclusive items queued for their matching essences.
- Prevent outdated automatic selections if the queue changes before the item is selected.

## VERSION 0.3.2

- Automatically import legacy CommandQueue / CommandQueueExtended presets from BepInEx/config/commandQueueSaves.json once per Remastered preset file. Keep the source unchanged and retain existing Remastered presets.
- Persist import completion with the presets, including after imported presets are deleted. Retain names, item order and quantities; split stacks above 999. Legacy presets contain no Repeat settings.
- Continue reading existing Remastered preset arrays. Invalid legacy input aborts the complete import and is logged for retry on the next startup.
