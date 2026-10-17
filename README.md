# CommandQueue Remastered

**Beta 0.3.4** — plan Artifact of Command item queues directly in the Risk of Rain 2 scoreboard.

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

## Installation and use

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

`ExclusiveItemSelection` applies to exclusive Command essences offering **Halcyon Seed, Newly Hatched Zoea, Exposed Cerebellum, Encrypted Cerebellum, Prison Matrix or Sentry Key**. `Default` finds the item anywhere in the queue. `Automatic` also selects it without an entry, leaving the queue unchanged in that case. Existing entries follow normal consumption and Repeat rules. Pause, disabled groups and temporary/ethereal policies take priority. Items still drop normally; there is no automatic collection. Queued exclusive items that are not offered by the current essence do not block later normal items of the same tier. Those exclusive entries stay queued for their matching essences. Offered but unavailable items still open the manual menu.

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

