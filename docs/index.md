# Beat Saber IPA Reloaded [![Build status](https://ci.appveyor.com/api/projects/status/1ruhnnfeudrrd097?svg=true)](https://ci.appveyor.com/project/nike4613/beatsaber-ipa-reloaded-9smsb)

Beat Saber IPA - The mod injector tailored for Beat Saber

## How To Install

1. [Download a release](https://github.com/beat-saber-modding-group/BeatSaber-IPA-Reloaded/releases)
2. Extract the contents into the game folder
3. Run **IPA.exe**
4. Start the game as usual

A console window should open before the game starts if the installation was successful.

To disable this console window, pass `--no-console` to the game.

## How To Uninstall

1. Drag & drop the game exe onto **IPA.exe** while holding <kbd>Alt</kbd>
    - Or run `ipa -rn` in a command window

## Arguments

`IPA.exe file-to-patch [arguments]`

- `--launch`: Launch the game after patching
- `--revert`: Revert changes made by IPA (= unpatch the game)
- `--nowait`: Never keep the console open
- See `-h` or `--help` for more options.

Unconsumed arguments will be passed on to the game in case of `--launch`.

## How To Develop

1. Create a new **Class Library** C# project (.NET 4.6)
2. Download a release and add **IPA.Loader.dll** to your references
3. Implement `IBeatSaberPlugin` or `IEnhancedBeatSaberPlugin`
4. Build the project and copy the DLL into the Plugins folder of the game.

See [Developing](https://github.com/beat-saber-modding-group/BeatSaber-IPA-Reloaded/wiki/Developing) for more information.

## How To Keep The Game Patched

BSIPA will automatically repatch the game when it updates, as long as `winhttp.dll` is present in the install directory.

## Notes for running under Wine

For some reason, by default, Wine does not load DLLs in quite the same way that Windows does, causing issues with the injection.
To make the injection work with Wine, `winhttp` has to have a DLL override set to `native,builtin`. This can be set either through
Protontricks, or with the following `.reg` file.

```reg
REGEDIT4
[HKEY_CURRENT_USER\Software\Wine\DllOverrides]
"winhttp"="native,builtin"
```

For Steam there's a per-game Wine prefix under `compatdata`. In this case `SteamLibrary/steamapps/compatdata/620980/pfx/user.reg`.
Changes to this file will likely be ovewritten when the game updates or if local files are validated through Steam.

## Developing BSIPA itself

### Prerequisites

- Microsoft Visual Studio 2019 or later (2017 may work, no guarantees)
- Tools for C/C++ (MSVC) v141
- .NET 4.6.1 SDK and .NET 4.7.2 SDK

### Building

1. Clone with `git clone https://github.com/beat-saber-modding-group/BeatSaber-IPA-Reloaded.git --recursive`
2. Create a file, `bsinstalldir.txt` in the solution root. Do NOT create this in Visual Studio; VS adds a BOM at the begginning of the file that the tools used cannot read.
   It should contain the path to your Beat Saber installation, using forward slashes with a trailing slash. e.g.

   ```
   C:/Program Files (x86)/Steam/steamapps/common/Beat Saber/
   ```

3. Open `BSIPA.sln` in Visual Studio.
4. Choose the configuration `x64`
5. Rebuild all.

   When you make a change somewhere in BSIPA itself, right click on `IPA` and click `Build`. This sets up the output in `path/to/solution/IPA/bin/<Configuration>` to be what
   should be copied to the game directory.

   When making a change to the Mod List, you only need to build the mod list. Install by copying everything in `path/to/solution/BSIPA-ModList/bin/<Configuration>` to your game
   directory.

When building a Debug build, all referenced assemblies from Beat Saber will be copied from the install directory provided in `bsinstalldir.txt` into `Refs/`. Any new references
should reference the copy in there. When building for Release, it just uses the files already in `Refs/`
