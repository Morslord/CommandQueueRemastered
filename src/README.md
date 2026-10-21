# Building CommandQueue Remastered 0.4.1

The mod targets .NET Standard 2.1 and references the installed Risk of Rain 2 1.5.0 assemblies.

## Build requirements

- .NET SDK 10.
- A local Risk of Rain 2 installation with its managed assemblies.
- A mod manager profile containing BepInEx and Harmony under `BepInEx/core/`.
- `../build/icon.png` for the embedded mod icon. Preserve this path when copying the source tree.

ProperSave, LookingGlass and RiskOfOptions are optional runtime integrations and are not required to compile the mod.

## Build and checks

Open `CommandQueueRemastered.slnx` in Visual Studio, or run these commands from this directory. Replace the example paths with your game installation and mod manager profile directories.

```powershell
dotnet clean
dotnet build -c Release `
  "-p:GameDir=C:\Path\To\Risk of Rain 2" `
  "-p:ProfileDir=C:\Path\To\ModProfile"
```

You can also supply the paths through the `ROR2_GAME_DIR` and `ROR2_PROFILE_DIR` environment variables.

The compiled plugin is written to `bin/Release/netstandard2.1/CommandQueueRemastered.dll`. Release builds also copy it to `../build/CommandQueueRemastered.dll`. Building does not install the plugin into your game or mod manager profile.

Run the core checks separately:

```powershell
dotnet run --project tests/CoreChecks -c Release
```

These checks exercise queue behavior, selection and menu-input policies, description formatting, legacy preset import, preset formatting, essence distribution and single-use collection tickets without launching the game. They do not validate Unity rendering, runtime hooks, item-transfer orb delivery or multiplayer behavior.

Additional checks cover pause-based distribution filtering, remote pause state ownership/expiry and queue-only eligibility for automatic essence interaction. Automatic interaction uses the game's Interactor; optional host-requested pause reporting uses a versioned UNet message with a protocol marker and preserves foreign message handlers on channel conflicts. These runtime/network paths still need in-game validation.

Bounded-work regressions exercise an 18-drop burst in 4/4/4/4/2 batches, FIFO draining, candidate priority among 10,000 registered entries, removal/re-enabling and cancellation during callbacks. They verify scheduling rules without measuring Unity frame time or proving native spawn/physics/network behavior.

## Required source files

Keep these files and directories in their repository locations:

- `CommandQueueRemastered.csproj` and `CommandQueueRemastered.slnx`.
- `RemasteredPlugin.cs` and all C# files under `Core/`, `Game/` and `Integrations/`.
- `../build/icon.png` for the embedded icon.
- `tests/CoreChecks/CoreChecks.csproj` and `tests/CoreChecks/Program.cs` to run the core checks.
- `tests/CoreChecks/Fixtures/commandQueueSaves.json` as the legacy import fixture for the core checks.

The `tools/` directory is optional and is not required to compile the mod. Generated `bin/`, `obj/` and `.vs/` directories are not needed to rebuild it.


0.4.1 checks additionally cover unique limiter stacks, cap-aware selection and Repeat, version 1/2 document migration, group-scoped limiter presets, distribution exclusions, pending assignment accounting and Hex blocks of seven. Native source-context and reservation-lifecycle hooks require in-game host/client validation.

Special-pool regressions cover absent/unavailable heads, tier/limiter/pause guards, no-consumption and both Repeat modes, changed pools and exclusive choices. Drifter Salvage uses a single Original/SkillUser/ItemDistribution dropdown with SkillUser default; its native source-context hook still needs in-game validation.

Personal temporary/ethereal Ignore flags use additive message kind 3 on the existing signed/versioned status channel, with a two-bit mask. Clients report only their own NetworkUser, host checks connection ownership; states expire after five seconds with one-second heartbeats. Unknown/old clients remain eligible. Status queries run while distribution is enabled independently of ExcludePausedPlayers. Drifter caster identity is captured in the native source scope and stored per deferred recipient; separate simultaneous casters do not share a target. New network/source paths require in-game validation.
