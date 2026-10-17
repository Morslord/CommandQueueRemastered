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
