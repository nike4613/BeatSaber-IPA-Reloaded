---
title: BSIPA - Home
uid: home
---

# ![BSIPA](images/banner_dark.svg)

[![Build status](https://ci.appveyor.com/api/projects/status/1ruhnnfeudrrd097?svg=true)](https://ci.appveyor.com/project/nike4613/beatsaber-ipa-reloaded-9smsb)

BSIPA - The Unity mod injector for the new age (pending confirmation).

Assuming, that is, that Unity 2017 is "new age".

## How To Install

1. [Download a release](https://github.com/beat-saber-modding-group/BeatSaber-IPA-Reloaded/releases)
2. Extract the contents into the game folder
3. Run **IPA.exe**
4. Start the game as usual

## How To Uninstall

1. Drag & drop the game exe onto **IPA.exe** while holding <kbd>Alt</kbd>
    - Or run `ipa -rn` in a command window

## Arguments

See <xref:articles.command_line>.

## How To Develop

1. Create a new **Class Library** C# project (.NET 4.6)
2. Download a release and add **IPA.Loader.dll** to your references
3. Implement `IPA.IPlugin` or `IPA.IEnhancedPlugin`
4. Build the project and copy the DLL into the Plugins folder of the game.

See [Developing](xref:articles.start.dev) for more information.

## How To Keep The Game Patched

BSIPA will automatically repatch the game when it updates, as long as `winhttp.dll` is present in the install directory.
