# CommandQueue Remastered

**Beta 0.4.1** — plan Artifact of Command item queues directly in the Risk of Rain 2 scoreboard.

Development build for branch testing. Gameplay and multiplayer testing is incomplete; some essences can remain on the ground while moving.

Build item queues, repeat your preferred picks and save presets without leaving the game's interface.

## Features

- **Scoreboard integration:** switch between the original **Players** overview and **Command Queue** using the game's configured scoreboard key.
- **Optional gameplay controls:** choose `On`, `CommandQueueOnly` or `Off` for `BlockGameplayInput` to control which scoreboard views block movement and gameplay buttons.
- **Item queues:** add quantities, reorder or split stacks with drag and drop, remove items and clear queues with confirmation.
- **Automatic Command selection:** selects the next queued item for the essence's actual tier. Empty queues or unavailable next items normally open the manual selection menu. Temporary/ethereal essences skip entries unavailable in their pool; exclusive-item behavior is configurable below.
- **Queue and Limiter:** switch views per item group. The third button shows Repeat in Queue view or the independent Limiter On/Off state in Limiter view. Pause remains a separate group control.
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

Interact with a Command essence to select the next eligible item from its tier's queue. An empty queue or unavailable next item normally opens the manual picker; temporary and ethereal essences skip entries unavailable in their special pool. Other choice windows retain their normal behavior.

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

**Clear group** and **Clear all** affect the selected Queue or Limiter view. **Clear all** requires confirmation, with **Cancel** selected initially.

## Limiter

Switch between **Queue** and **Limiter** above the item grid. The Limiter is a cap, not a second automatic selection list: only items from the queue are selected. Set, for example, syringe limit 3 and glasses limit 2, then keep syringes, glasses and repeating crowbars in the queue. At the cap, syringe/glasses entries are skipped and grayed out without being consumed. They become eligible again after scrapping, trading or otherwise losing counted stacks. If every queued choice is capped, the normal picker opens.

Each item has exactly one Limiter stack, in the order first added. Left-click adds one, Shift-left adds ten; right-click removes one, Shift-right removes up to ten and Ctrl-right removes the target. Ctrl-left does not move targets to the front. Dragging always moves the whole Limiter stack, including with Shift/Ctrl held. **Sort by ID** replaces the drag shortcut text and sorts the current group in the same catalog ID order as the selection grid. Queue drag/split controls remain available in Queue view.

The selection grid shows **owned/limit**, for example **5/9**. General / **LimiterItemCount** defaults to **PermanentOnly**; choose **IncludeTemporary**, **IncludeEthereal** or **IncludeBoth** to count those stacks too, including with UseQueueNormally. Disabling the Limiter for a group ignores its caps and dims only the limit number. Queue-view items with an active cap have a thin silver-gray strip along the bottom edge.

Targets and Limiter On/Off states are included in presets and supported ProperSave payloads; preview always combines both lists. Within each tier, queued items appear first, followed by limiter-only items, before the next tier. Repeated queue entries are summed per item, badges show saved queue quantity/limit (— means no limit), and limiter-only items are gray with quantity zero. No items are automatically added to the queue from the Limiter. The Limiter applies to automatic Command choices, not manual pickups or unrelated sources.

## Item Distribution Rules

These host settings have their own RiskOfOptions tab. Defaults: distribution enabled, Bazaar Between Time and Computational Exchange excluded, chest/exchange/teleporter sources enabled, DrifterSalvage set to SkillUser, Hex blocks disabled.

| Setting | Effect |
| --- | --- |
| Enabled | Master distribution switch; Off keeps original drop locations |
| ExcludedStages | Open the scrollable stage checklist; names are grouped by stage from the installed game catalog. Defaults exclude Bazaar Between Time and Computational Exchange |
| Chests | Standard/option chests and money shop terminals |
| ExchangePools | Lunar-item exchange pool terminals, cauldron terminals and Hallowed cauldron drops |
| TeleporterBossRewards | Teleporter boss rewards; other boss groups retain their per-tier mode |
| DrifterSalvage | Original keeps the skill location; SkillUser (default) sends every drop to its own casting Drifter; ItemDistribution uses the per-tier mode |
| HexGroupsOfSeven | Complete seven-item blocks across all permanent Hex items, including outstanding assignments |

Source/stage exclusions leave the original location and retain normal queue selection/collection settings. Original per-tier mode keeps the original location except for the explicit Drifter SkillUser target, which overrides per-tier modes. Master/stage/source exclusions, personal Ignore and optional paused-group exclusions still apply. With no eligible caster, keep the original skill location; do not redirect to a different Drifter. Source recognition covers the native hooked paths; mods implementing their own sources may bypass these contexts.

Hex blocks override the selected Hex recipient algorithm except Original. With no existing Hex items, give one player seven, then the next. Complete an unfinished block first; with several partial blocks, start with the one closest to completion and finish that player's block. Once complete, rotate by stable player ID. All eligible living players participate, with the existing optional paused-group exclusion. Different Hex items count together; temporary/ethereal stacks are excluded.

LimiterItemCount appears directly above BlockGameplayInput in General. DrifterSalvage uses one dropdown; previous checkbox/target settings are migrated automatically.

RiskOfOptions shows three categories per page for this mod: Groups, General and Special Items on page one; Item Distribution Rules followed by Item Distribution on page two. Other mods retain their native paging. ExcludedStages descriptions list environment names. In the selector, checked means excluded; Apply saves and Cancel discards changes. Defaults selects the two default realms; None allows every stage. Variants sharing a display name are selected together. Existing additional/custom scene exclusions are preserved. On the first startup of this correction, both default realms are added once to the existing exclusions; later changes, including an empty selection, are retained. Advanced comma-separated scene names remain supported in the config file.

## Repeat and presets

**Pause** is the first button of the right-hand action group, followed by **Clear Group** and **Clear All**. It pauses automatic selection for the current item group while preserving quantities and Repeat progress; orange indicates paused. The left-hand group contains **Queue**, **Limiter** and either **Repeat: On/Off** or **Limiter: On/Off**. All six visible buttons have equal width, with a gap between the two groups. Limiter and Repeat use green for On. Pause and Limiter remain independent; pause resets each run while Limiter state is saved with targets.

Drag a preset row to reorder the list. A cursor label and insertion line show the destination; dragging near the list edges scrolls it. The order is saved immediately and survives restarting the game. Drop outside the list or press Escape to cancel.

Enable **Repeat** for a group to keep using its plan. With `RotateRepeatedItems` enabled, each selected item moves to the end of its tier's queue. With rotation disabled, an internal cursor cycles through the planned quantities while displayed order and counts stay fixed. Editing the queue, changing Repeat settings or mode, or restoring a queue starts a new cycle. Cycle progress is not persisted.

Presets support saving, loading, renaming, deletion and read-only **Preview**. Select a saved row to use its action buttons. Queue and Limiter load independently: queued entries replace only Queue groups represented in the preset, while limits replace only represented Limiter groups. A limiter-only preset preserves the entire Queue and its Repeat progress; a queue-only or older preset preserves all limits. Unrepresented groups stay intact. An empty preset leaves the current plan unchanged.

Presets are stored in `BepInEx/config/commandQueueRemastered.presets.json`. Invalid preset data is logged, and preset writes are disabled for that session to protect the file.

Remastered automatically imports `BepInEx/config/commandQueueSaves.json` from the same profile once, adding legacy CommandQueue / CommandQueueExtended presets without changing the source or existing presets. Import completion is stored with the presets, so deleted imports stay deleted. Names, order and quantities are retained; Repeat starts off because the legacy format contains no Repeat settings. A missing source can be added before a later startup. Invalid source data aborts the entire import and is logged for retry on the next startup.

With ProperSave installed, run queue persistence is available for singleplayer and the multiplayer host. Multiplayer-client queue persistence is not supported.

## Item information and settings

Item counters show permanent inventory stacks unless an item has a configured limit, in which case LimiterItemCount controls its owned/limit count. Queue badges show planned quantities.

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
| `LimiterItemCount` | `PermanentOnly` | Optionally include temporary, ethereal or both kinds of stacks in limits |
| `ExcludePausedPlayers` | Disabled | Host: skip players whose matching queue group is paused |
| `FewestItemsCount` | `AllItems` | Count permanent stacks across all items, or only `SameTier` |
| Item Distribution: one mode per actual tier | `Original` | `Original`, `RoundRobin`, `Killer` or `FewestItems` |

Essence drop modes apply to Command item droplets from all sources; equipment and non-Command drops retain their original locations. Settings use actual tier names (`Tier1` = White, `Tier2` = Green, `Tier3` = Red, `Cursed` = Hallowed); all four Void tiers have independent choices. Round robin uses living player characters, ordered by stable network ID, and resets each run. Killer includes owned minions; unavailable/dead killers and sources without a kill context fall back to round robin. Delayed boss rewards without a live kill context use this fallback. With no living players, the original location is retained. Essences remain shared and unreserved; relocation does not automatically open them.

FewestItems counts permanent stacks plus outstanding permanent essence assignments. Choose AllItems or SameTier; ties rotate independently per actual tier. Assignments follow the native droplet, essence, selected ground item and transfer orb, and are released on consumption, orb arrival, destruction or run reset. Relocation does not reserve ownership: another player can still collect the essence.

`AutoCollectQueuedItems` defaults off. Only a successful automatic selection with an existing queue entry is collected. Use AutoPickupCommandEssence to also open matching nearby essences automatically. Manual choices, empty/unavailable queues, paused groups and exclusive Automatic choices without an entry keep their normal behavior. Collection preserves temporary/ethereal status and the existing consumption/Repeat policies, including selection without consumption.

Preset files retain the versioned container and legacy import status, with one compact preset per line for easier manual editing.

Assigned drops wait as complete pickup data until delivery. The host leaves at least 1/30 second between deliveries (at most 30 per second), never creates more than one per rendered frame, and does not catch up with a burst after a frame hitch, using the recipient's current position and the native Command conversion. Waiting drops do not depend on live droplet objects. If the recipient is gone, the drop uses its original position and normal physics. Original-location drops keep their normal flight.

AutoPickup registers live essences when they appear and removes them when disabled. Each local player checks at most 32 candidate distances and four nearby choices per processing step, beginning at most one interaction. New essences receive priority; older candidates are visited in rotation. Full-scene essence/prompt searches and global distance sorting are avoided. Queue, pause, normal range, line of sight and next-frame selection validation remain active. Actual frame-time impact and fast movement in multiplayer still require in-game testing.

RiskOfOptions categories include **Item Distribution**, **Item Distribution Rules** and **Special Items**, with **VoidWhite**, **VoidGreen** and **VoidRed** matching the basic rarity names. Descriptions separate behavior choices with blank lines. Existing configuration sections/keys retain their names and values.

The three collection/distribution switches are under **General**. `AutoCollectQueuedItems` automatically collects the item selected from your queue; otherwise it drops on the ground. In multiplayer, enable it on both host and selecting client. Existing values from the former Item collection category are retained.

`AutoPickupCommandEssence` opens a nearby essence using normal interaction range, only if an active queue has an available matching entry. It leaves empty, paused, disabled or unavailable queues alone, including exclusive Automatic selections without a queue entry. It works while viewing the scoreboard; other menus, dialogs, text input, dragging, another interaction and obstructing world geometry prevent automatic interaction. Combine it with `AutoCollectQueuedItems` to also collect the selected item.

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
| `UseQueueWithoutConsuming` | Select the next available queued item of the same tier without changing quantities, order or Repeat progress |
| `UseQueueNormally` | Select and consume from the queue normally |

**IgnoreTemporaryItems** and **IgnoreEtherealItems** are independent personal True/False settings under **Special Items**, both off by default. Enabling one excludes only your own player from distribution of that pickup type, even when ExcludePausedPlayers is off. It also disables proximity auto-opening for that type; manual interaction retains your chosen Behaviour. The host's personal settings do not override another player's preferences. Updated clients report only their own choices to an updated host; unknown or stale remote states remain eligible. If every recipient is excluded, the drop keeps its original location. Manually collecting items remains possible.

For temporary items from other sources to route to Drifter, enable a per-tier distribution mode and let other players enable IgnoreTemporaryItems while Drifter leaves it off. For Salvage, select DrifterSalvage = SkillUser (default): Drifter A's drops target A and Drifter B's drops target B. A caster who personally opts out with Ignore is not assigned those drops.

Selected pickups retain their temporary or ethereal status. For temporary and ethereal essences, skip queued items absent or unavailable in that essence and select the next eligible item of the same actual tier. Skipped entries remain queued; consumption and Repeat apply only to the selected entry. If no eligible queued choice exists, open the normal picker. Permanent essences retain their usual head rule.

`ExclusiveItemSelection` applies to exclusive Command essences offering **Halcyon Seed, Newly Hatched Zoea, Exposed Cerebellum, Encrypted Cerebellum, Prison Matrix or Sentry Key**. `Default` finds the item anywhere in the queue. `Automatic` also selects it without an entry, leaving the queue unchanged in that case. Automatic selects after you interact with the essence; it does not open an essence by itself. AutoPickupCommandEssence opens it only when an active queue has a matching entry. Existing entries follow normal consumption and Repeat rules. Pause, disabled groups and temporary/ethereal policies take priority. AutoCollectQueuedItems can collect selections using an existing queue entry; selections without an entry still drop normally. Queued exclusive items that are not offered by the current essence do not block later normal items of the same tier. Those exclusive entries stay queued for their matching essences. Offered but unavailable items still open the manual menu.

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

