# CommandQueue Remastered

**Beta 0.3.1** — plan your Artifact of Command picks directly in the Risk of Rain 2 scoreboard.

Build item queues, repeat your preferred picks and save presets without leaving the game's interface. Remastered combines the functionality of CommandQueueExtended, CommandQueueExtendedIconFix and ProperSave_CommandQueueExtended in one mod.

## Features

- **Scoreboard integration:** switch between the original **Players** overview and **Command Queue** using the game's configured scoreboard key.
- **Item queues:** add quantities, reorder or split stacks with drag and drop, remove items and clear queues with confirmation.
- **Automatic Command selection:** selects the next queued item for the essence's actual tier. Empty queues or unavailable next items keep the normal manual selection menu.
- **Repeat modes:** rotate selected items to the end, or cycle through the planned quantities while keeping the displayed order and counts fixed.
- **Presets:** save, load, preview, rename and delete queue plans.
- **Item information:** icons, permanent inventory counters and a scrollable description panel with optional LookingGlass stats.
- **Configurable special pickups:** choose separate behavior for temporary and ethereal Command items.
- **Optional integrations:** RiskOfOptions for in-game settings, LookingGlass for descriptions and ProperSave for run queue persistence.

Groups appear in this order: **White, Green, Red, Lunar, Boss, Void, Hallowed, Hex, Pages**. Void items share one tab and show rarity color strips; automatic selection and Repeat remain separate for each actual Void tier.

## Installation

Install with r2modman or another Thunderstore-compatible mod manager. **BepInExPack must be installed.**

Disable or remove these older mods from the same profile before using Remastered:

- CommandQueueExtended
- CommandQueueExtendedIconFix
- ProperSave_CommandQueueExtended

Remastered replaces them and prevents simultaneous loading. **ProperSave itself is a separate optional mod** and should remain installed if you want to save your queue with a run.

RiskOfOptions and LookingGlass are optional. Without RiskOfOptions, use the configuration file. Without LookingGlass, descriptions use the game's normal text.

## Getting started

1. Start a run with the **Artifact of Command** enabled.
2. Open the scoreboard using your game key, normally **Tab**, and select **Command Queue**.
3. Choose an item group and click the items you want to queue.
4. Interact with a matching Command essence. Remastered selects the next eligible queued item automatically.

The **Players** button returns to the original scoreboard overview. Keyboard and mouse are the intended controls; **controller support has not been tested and controller compatibility is unverified**.

## Controls

| Action | Result |
| --- | --- |
| Left-click an available item | Add one to the queue |
| Left-click a queued item | Increase that stack by one |
| Shift + left-click | Add ten |
| Ctrl + left-click | Add one at the front of the displayed group |
| Ctrl + Shift + left-click | Add ten at the front of the displayed group |
| Right-click a queued item | Remove one |
| Shift + right-click | Remove up to ten |
| Ctrl + right-click | Remove the whole stack |
| Drag a queued item | Move the whole stack |
| Shift + drag | Move half the stack, rounded down |
| Ctrl + drag | Move one item |

Ctrl takes priority over Shift when removing or dragging. Matching neighboring items within a group merge after moves, removals and Repeat rotation. A full stack remains separate from any overflow; quantities are preserved.

Hover over an item to read its description. If the text is too long, use the mouse wheel over that item or the description panel to scroll it. Descriptions work independently of the game's **Enable Inspect** setting.

Inventory counters show **permanent owned items**, excluding temporary and ethereal stacks. Queue badges show planned quantities instead.

## Repeat and presets

Enable **Repeat** for the selected group to keep using its plan. With **RotateRepeatedItems** enabled, each selected item moves to the end and stacks with a matching neighbor. With rotation disabled, the queue cycles through every planned quantity while its displayed order and counts stay fixed. Editing the queue or restoring it starts a new cycle.

Enter a preset name and use **Save / Rename** to save your plan. Select a saved row, then use **Load**, **Preview**, **Rename** or **Delete**.

Loading replaces only groups containing items in that preset; other groups and their Repeat settings stay intact. An empty preset leaves the queue unchanged. **Preview** is read-only and shows saved quantities and order.

Presets are stored in `BepInEx/config/commandQueueRemastered.presets.json`. With ProperSave installed, run queue persistence is available for singleplayer and the multiplayer host. Multiplayer-client queue persistence is not implemented.

## Settings

With RiskOfOptions installed, open **Settings → MOD OPTIONS → CommandQueue Remastered**. Otherwise edit `BepInEx/config/local.commandqueue.remastered.cfg`.

| Section | Setting | Default |
| --- | --- | --- |
| Groups | Nine independent group toggles | All enabled |
| General | RotateRepeatedItems | Enabled |
| General | MenuScale | 1.00x, range 0.50x–2.00x |
| General | ItemScale | 1.00x, range 0.50x–2.00x |
| Special items | TemporaryItemBehavior | ManualMenu |
| Special items | EtherealItemBehavior | ManualMenu |

Menu scale affects the whole interface and is limited by the available screen space. Item scale adjusts icon size separately.

Temporary and ethereal pickups each have these choices:

| Behavior | Effect |
| --- | --- |
| ManualMenu | Open the normal selection menu |
| UseQueueWithoutConsuming | Select the queued item without changing quantities, order or Repeat progress |
| UseQueueNormally | Select and consume from the queue normally |

The selected item's temporary or ethereal status is preserved. An empty queue or unavailable next item opens the normal picker.

## Beta status and feedback

This beta targets **Risk of Rain 2 1.5.0**. Multiplayer behavior and all mod combinations have not been fully validated. Controller support is untested.

Each queue stack supports up to **999 items**. Importing saves from the older mods are not implemented.

When reporting a problem, include the mod and game versions, steps to reproduce it, relevant settings, and your `BepInEx/LogOutput.log`. A screenshot helps with interface problems.
