# CommandQueue Remastered

**Beta 0.4.0** — plan Artifact of Command item queues directly in the Risk of Rain 2 scoreboard.

Development build for branch testing. Gameplay and multiplayer testing is incomplete; some essences can remain on the ground while moving.

Build item queues, repeat your preferred picks and save presets without leaving the game's interface.

## Features

- **Scoreboard integration:** switch between the original **Players** overview and **Command Queue** using the game's configured scoreboard key.
- **Optional gameplay controls:** choose `On`, `CommandQueueOnly` or `Off` for `BlockGameplayInput` to control which scoreboard views block movement and gameplay buttons.
- **Item queues:** add quantities, reorder or split stacks with drag and drop, remove items and clear queues with confirmation.
- **Automatic Command selection:** selects the next queued item for the essence's actual tier. Empty queues or unavailable next items normally open the manual selection menu; the exclusive-item setting below provides an optional exception.
- **Pause:** pause automatic selection independently for each item group without losing quantities or Repeat progress. The Pause button turns orange-yellow when the currently selected group is paused.
- **Repeat modes:** rotate selected items to the end, or cycle through the planned quantities while keeping the displayed order and counts fixed.
- **Presets:** save, load, preview, rename and delete queue plans. Drag preset rows to reorder them; the order is saved automatically.
- **Item information:** icons, permanent inventory counters and a scrollable description panel with optional LookingGlass stats.
- **Configurable special pickups:** choose separate behavior for temporary and ethereal Command items. Configure six exclusive items to select when queued anywhere or automatically even without a queue entry.
- **Optional integrations:** RiskOfOptions for in-game settings, LookingGlass for descriptions and ProperSave for run queue persistence.
- **Essence locations:** choose the original drop location, round robin, killer or fewest permanent items independently for each actual item tier.
- **Optional item collection:** collect automatically selected queued items through native item-transfer orbs. Manual choices and selections without a queue entry retain normal drops.

## Installation and use

Essence relocation is controlled by the host/server; clients do not need this mod to see relocated essences. Automatic collection requires Remastered 0.4.0 or newer and `AutoCollectQueuedItems` enabled on both the host and selecting client. On older/unmodded hosts, queued selection retains normal drops. These multiplayer paths still require in-game validation.

Use a game profile with BepInExPack and SeekersPatcher installed. Place the compiled DLL in that profile's `BepInEx/plugins/` directory.

CommandQueueExtended, CommandQueueExtendedIconFix and ProperSave_CommandQueueExtended are incompatible with Remastered and must be disabled in the same profile. ProperSave itself is a separate optional integration.

Start a run with Artifact of Command enabled, open the scoreboard using the game's configured key (normally Tab), and select **Command Queue**. **Players** displays the player overview.

The available groups are **White, Green, Red, Lunar, Boss, Void, Hallowed, Hex and Pages**. Void items share one tab with rarity color strips; selection and Repeat use each essence's actual item tier.

Interact with a Command essence to select the next eligible item from its tier's queue. An empty queue or unavailable next item opens the normal manual picker. Other choice windows retain their normal behavior.

## Queue controls

**Controller support is untested and controller compatibility is unverified.** Keyboard and mouse are the intended controls.

| Action | Result |
| --- | --- |
| Left-click an available item | Queue one item |
| Left-click a queued item | Add one to that stack |
| Shift + left-click | Add ten |
| Ctrl + left-click | Add one at the front of the displayed group |
| Ctrl + Shift + left-click | Add ten at the front of the displayed group |
| Right-click a queued item | Remove one |
| Shift + right-click | Remove up to ten |
| Ctrl + right-click | Remove the whole stack |
| Drag a queued item | Move the whole stack |
| Shift + drag | Move half the stack, rounded down |
| Ctrl + drag | Move one item |

Ctrl takes priority over Shift when removing or dragging. Drag modifiers can change during the drag. The cursor icon shows the moved quantity, and a vertical marker indicates the insertion position. Dragging near the queue's upper or lower edge scrolls the list.

Matching neighbors within a displayed group merge after moves, removals and Repeat rotation. Each stack holds up to **999 items**; merging preserves excess quantities in separate stacks. Direct additions beyond a stack's limit are rejected and logged.

**Clear all** requires confirmation, with **Cancel** selected initially.

## Repeat and presets

**Pause** sits between Repeat and Clear group and turns orange-yellow while active. It pauses automatic selection only for the currently selected group, opening normal Command menus for that group while preserving quantities, order and Repeat progress. Other groups continue selecting automatically. Each group has its own pause state; the shared Void group covers all Void tiers. Click again in that group to resume. All group pauses reset at run start/end and are not saved in presets or ProperSave.

Drag a preset row to reorder the list. A cursor label and insertion line show the destination; dragging near the list edges scrolls it. The order is saved immediately and survives restarting the game. Drop outside the list or press Escape to cancel.

Enable **Repeat** for a group to keep using its plan. With `RotateRepeatedItems` enabled, each selected item moves to the end of its tier's queue. With rotation disabled, an internal cursor cycles through the planned quantities while displayed order and counts stay fixed. Editing the queue, changing Repeat settings or mode, or restoring a queue starts a new cycle. Cycle progress is not persisted.

Presets support saving, loading, renaming, deletion and read-only **Preview**. Select a saved row to use its action buttons. Loading replaces only groups containing items in the preset; other groups and their Repeat settings remain intact. An empty preset leaves the queue unchanged.

Presets are stored in `BepInEx/config/commandQueueRemastered.presets.json`. Invalid preset data is logged, and preset writes are disabled for that session to protect the file.

Remastered automatically imports `BepInEx/config/commandQueueSaves.json` from the same profile once, adding legacy CommandQueue / CommandQueueExtended presets without changing the source or existing presets. Import completion is stored with the presets, so deleted imports stay deleted. Names, order and quantities are retained; Repeat starts off because the legacy format contains no Repeat settings. A missing source can be added before a later startup. Invalid source data aborts the entire import and is logged for retry on the next startup.

With ProperSave installed, run queue persistence is available for singleplayer and the multiplayer host. Multiplayer-client queue persistence is not supported.

## Item information and settings

Item counters show permanent inventory stacks, excluding temporary and ethereal items. Queue badges show planned quantities.

The description panel shows the hovered or last selected item, using LookingGlass text when available and vanilla text otherwise. Descriptions work independently of the game's **Enable Inspect** setting. Long text wraps within the panel and scrolls vertically. The mouse wheel over an item with overflowing description text scrolls that description; selecting a different item resets the text scroll position.

With RiskOfOptions installed, use **Settings → MOD OPTIONS → CommandQueue Remastered**. Otherwise edit `BepInEx/config/local.commandqueue.remastered.cfg`.

| Setting | Default | Purpose |
| --- | --- | --- |
| Nine group toggles | All enabled | Choose which groups are available |
| `RotateRepeatedItems` | Enabled | Rotate Repeat picks to the end |
| `BlockGameplayInput` | `On` | `On`: block both views; `CommandQueueOnly`: block only Command Queue; `Off`: allow both |
| `MenuScale` | 1.00x | Scale the whole interface, range 0.50x–2.00x |
| `ItemScale` | 1.00x | Scale item icons, range 0.50x–2.00x |
| `TemporaryItemBehavior` | `ManualMenu` | Handle temporary Command pickups |
| `EtherealItemBehavior` | `ManualMenu` | Handle ethereal Command pickups |
| `ExclusiveItemSelection` | `Default` | Select the six exclusive items when queued anywhere; `Automatic` also selects without an entry |
| `AutoCollectQueuedItems` | Disabled | Collect only automatic selections using an existing queue entry |
| `AutoPickupCommandEssence` | Disabled | Open nearby essences automatically when an active queue has a matching item |
| `ExcludePausedPlayers` | Disabled | Host: skip players whose matching queue group is paused |
| `FewestItemsCount` | `AllItems` | Count permanent stacks across all items, or only `SameTier` |
| Item Distribution: one mode per actual tier | `Original` | `Original`, `RoundRobin`, `Killer` or `FewestItems` |

Essence drop modes apply to Command item droplets from all sources; equipment and non-Command drops retain their original locations. Settings use actual tier names (`Tier1` = White, `Tier2` = Green, `Tier3` = Red, `Cursed` = Hallowed); all four Void tiers have independent choices. Round robin uses living player characters, ordered by stable network ID, and resets each run. Killer includes owned minions; unavailable/dead killers and sources without a kill context fall back to round robin. Delayed boss rewards without a live kill context use this fallback. With no living players, the original location is retained. Essences remain shared and unreserved; relocation does not automatically open them.

FewestItems counts permanent stack quantities, excluding temporary/ethereal items. Choose `AllItems` or `SameTier`. Ties rotate independently per actual tier. Inventory is evaluated for each drop; uncollected essences are not counted as owned items. These are host settings and update immediately.

`AutoCollectQueuedItems` defaults off. Only a successful automatic selection with an existing queue entry is collected. Use AutoPickupCommandEssence to also open matching nearby essences automatically. Manual choices, empty/unavailable queues, paused groups and exclusive Automatic choices without an entry keep their normal behavior. Collection preserves temporary/ethereal status and the existing consumption/Repeat policies, including selection without consumption.

Preset files retain the versioned container and legacy import status, with one compact preset per line for easier manual editing.

Assigned drops wait as complete pickup data until delivery. The host leaves at least 1/30 second between deliveries (at most 30 per second), never creates more than one per rendered frame, and does not catch up with a burst after a frame hitch, using the recipient's current position and the native Command conversion. Waiting drops do not depend on live droplet objects. If the recipient is gone, the drop uses its original position and normal physics. Original-location drops keep their normal flight.

AutoPickup registers live essences when they appear and removes them when disabled. Each local player checks at most 32 candidate distances and four nearby choices per processing step, beginning at most one interaction. New essences receive priority; older candidates are visited in rotation. Full-scene essence/prompt searches and global distance sorting are avoided. Queue, pause, normal range, line of sight and next-frame selection validation remain active. Actual frame-time impact and fast movement in multiplayer still require in-game testing.

RiskOfOptions categories are **Item Distribution** and **Special Items**, with **VoidWhite**, **VoidGreen** and **VoidRed** matching the basic rarity names. Descriptions separate behavior choices with blank lines. Existing configuration sections/keys retain their names and values.

The three collection/distribution switches are under **General**. `AutoCollectQueuedItems` automatically collects the item selected from your queue; otherwise it drops on the ground. In multiplayer, enable it on both host and selecting client. Existing values from the former Item collection category are retained.

`AutoPickupCommandEssence` opens a nearby essence using normal interaction range, only if an active queue has an available matching entry. It leaves empty, paused, disabled or unavailable queues alone, including exclusive Automatic selections without a queue entry. It does not interact through world geometry or while a menu/another interaction is active. Combine it with `AutoCollectQueuedItems` to also collect the selected item.

`ExcludePausedPlayers` is a host option affecting RoundRobin, Killer and FewestItems. It excludes only the paused group; other groups remain eligible, and the shared Void pause covers all four Void tiers. If all players are excluded, keep the original drop location. Remote pause reporting requires this updated mod on those clients. Unknown or stale states remain eligible; pause changes are communicated shortly after the local change. In RiskOfOptions, the three basic essence tiers are displayed as White, Green and Red; existing Tier1/Tier2/Tier3 configuration keys and values are retained.

**BlockGameplayInput** has three choices and defaults to **On**:

| Mode | Players | Command Queue |
| --- | --- | --- |
| On | Gameplay blocked | Gameplay blocked |
| CommandQueueOnly | Gameplay active | Gameplay blocked |
| Off | Gameplay active | Gameplay active |

Changes and view switches apply immediately. In an unblocked view, movement and gameplay buttons stay active after clicking or hovering buttons, while typing preset names, and during the Clear all confirmation. UI actions can therefore also trigger gameplay actions. The UI keeps its normal focus and the cursor remains available for menu interaction; mouse-look follows the game's normal cursor behavior. The actual pause menu and the game's camera-control restrictions remain in effect.

Menu scale is limited by available screen space. Temporary and ethereal items each support these behaviors:

| Behavior | Effect |
| --- | --- |
| `ManualMenu` | Open the normal selection menu |
| `UseQueueWithoutConsuming` | Select the queued item without changing quantities, order or Repeat progress |
| `UseQueueNormally` | Select and consume from the queue normally |

Selected pickups retain their temporary or ethereal status. Normally, an empty queue or unavailable next item opens the normal picker.

`ExclusiveItemSelection` applies to exclusive Command essences offering **Halcyon Seed, Newly Hatched Zoea, Exposed Cerebellum, Encrypted Cerebellum, Prison Matrix or Sentry Key**. `Default` finds the item anywhere in the queue. `Automatic` also selects it without an entry, leaving the queue unchanged in that case. Existing entries follow normal consumption and Repeat rules. Pause, disabled groups and temporary/ethereal policies take priority. AutoCollectQueuedItems can collect selections using an existing queue entry; selections without an entry still drop normally. Queued exclusive items that are not offered by the current essence do not block later normal items of the same tier. Those exclusive entries stay queued for their matching essences. Offered but unavailable items still open the manual menu.

## Bug reports

For bug reports, include the mod and game versions, reproduction steps, relevant settings and `BepInEx/LogOutput.log`. Include a screenshot for interface problems. Mod log entries use the **CommandQueue Remastered** source.

## Building from source

See [the build instructions](src/README.md) for requirements, compilation commands and core checks.

## Credits

- [gabrielgad](https://thunderstore.io/c/riskofrain2/p/gabrielgad/CommandQueueExtended/) — CommandQueueExtended.
- [KubeRoot](https://thunderstore.io/c/riskofrain2/p/KubeRoot/CommandQueue/) — original CommandQueue project.
- [KennedyMindsetMovement](https://thunderstore.io/c/riskofrain2/p/KennedyMindsetMovement/ProperSave_CommandQueueExtended/) — ProperSave_CommandQueueExtended, the reference for CommandQueueExtended run queue persistence.
- [KingEnderBrine / ProperSave](https://thunderstore.io/c/riskofrain2/p/KingEnderBrine/ProperSave/) — optional run save integration.
- [Rune580 / RiskOfOptions](https://github.com/Rune580/RiskOfOptions) — optional in-game settings UI.
- [Wet-Boys / LookingGlass](https://github.com/Wet-Boys/LookingGlass/) — optional enhanced item descriptions.

## License

See [LICENSE](LICENSE) for the GNU General Public License, version 3.

## AI disclosure

Generative AI (OpenAI Codex) assisted with the development of this mod and the writing of its documentation. In-game testing and feedback were provided by the maintainer. AI assistance does not guarantee correctness; the validation limits described above apply.

