# Build from source

Needs Windows, the .NET 10 SDK, Visual Studio Build Tools 2022 with the C++ workload, and Melvor Idle 2 installed through Steam. If the game is somewhere else, add `-p:MelvorGame=<the game's folder>` to each command. Run the commands in the repository's root.

The mod, into `bin/Release/`:

```
dotnet build -c Release
```

The Loader that starts it, into `loader/Loader/bin/Release/`, `loader/Loader/Host/bin/Release/` and `loader/WinmmProxy/bin/` (the last command from a Developer Command Prompt):

```
dotnet build loader/Loader/Loader.csproj -c Release
dotnet build loader/Loader/Host/Host.csproj -c Release
msbuild loader/WinmmProxy/WinmmProxy.vcxproj -p:Configuration=Release -p:Platform=x64
```

The mod's tests, where it has a `tests/` folder (they need no game; `<Mod>` is the project's name):

```
dotnet build tests/<Mod>.Tests.csproj
dotnet "%LOCALAPPDATA%\MelvorMods\tests\Debug\MelvorMods.<Mod>.Tests.dll"
```
