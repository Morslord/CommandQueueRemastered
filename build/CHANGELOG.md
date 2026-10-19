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
