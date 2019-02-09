
# BSIPA [![Build status](https://ci.appveyor.com/api/projects/status/fql702mky0d5bcky?svg=true)](https://ci.appveyor.com/project/nike4613/beatsaber-ipa-reloaded)

Beat Saber IPA - The mod injector tailored for Beat Saber

## How To Install

1. Download a release (https://github.com/nike4613/BeatSaber-IPA-Reloaded/releases)
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

See [Developing](https://github.com/nike4613/BeatSaber-IPA-Reloaded/wiki/Developing) for more information.

## How To Keep The Game Patched

BSIPA will automatically repatch the game when it updates, as long as `winhttp.dll` is present in the install directory.

## Building

### Prerequisites 

- Microsoft Visual Studio 2017 or later
- Tools for C/C++ (MSVC)
- .NET 4.6 SDK and .NET 4.7.1 SDK

### Building

1. Clone with `git clone https://github.com/nike4613/BeatSaber-IPA-Reloaded.git --recursive`
2. Create a file, `bsinstalldir.txt` in the solution root. Do NOT create this in Visual Studio; VS adds a BOM at the begginning of the file that the tools used cannot read. It should contain the path to your Beat Saber installation, using forward slashes with a trailing slash. e.g. 
```
C:/Program Files (x86)/Steam/steamapps/common/Beat Saber/
```
3. Open `BSIPA.sln` in Visual Studio.
4. Choose the configuration `x64`
5. Rebuild all. Any time you make a change, ALWAYS Rebuild All.

When building a Debug build, all referenced assemblies from Beat Saber will be copied from the install directory provided in `bsinstalldir.txt` into `Refs/`. Any new references should reference the copy in there. When building for Release, it just uses the files already in `Refs/`