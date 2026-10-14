# Building CommandQueue Remastered 0.3.1

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

These checks exercise queue behavior, selection policies and description formatting without launching the game. They do not validate Unity rendering, runtime hooks or multiplayer behavior.

## Required source files

Keep these files and directories in their repository locations:

- `CommandQueueRemastered.csproj` and `CommandQueueRemastered.slnx`.
- `RemasteredPlugin.cs` and all C# files under `Core/`, `Game/` and `Integrations/`.
- `../build/icon.png` for the embedded icon.
- `tests/CoreChecks/CoreChecks.csproj` and `tests/CoreChecks/Program.cs` to run the core checks.

The `tools/` directory is optional and is not required to compile the mod. Generated `bin/`, `obj/` and `.vs/` directories are not needed to rebuild it.

