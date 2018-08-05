# IPA
Illusion Plugin Architecture (Reloaded) - let's you inject code into Unity projects.

## How To Install

1. Download a release (https://github.com/nike4613/IPA-Reloaded/releases)
2. Extract the contents into the game folder
3. Drag & drop the game exe onto **IPA.exe**
4. Start the game as usual (the generated shortcut is optional)

**Optional:** 
To verify it worked, start the game with the `--verbose` flag. If a new console window opens with debug info, then you're good.

## How To Uninstall

1. Drag & drop the game exe onto **IPA.exe** while holding <kbd>Alt</kbd>
2. Done

## How To Develop

1. Create a new **Class Library** C# project (.NET 4.6)
2. Download a release and add **IllusionPlugin.dll** to your references
3. Implement `IBeatSaberPlugin` or `IEnhancedBeatSaberPlugin`
4. Build the project and copy the DLL into the Plugins folder of the gam

See [Developing](https://github.com/nike4613/IPA-Reloaded/wiki/Developing) for more information.

## How To Keep The Game Patched

When patching, IPA automatically creates a shortcut that keeps everything up-to-date. This simply makes sure that your DLLs remain patched even after an update.

## Arguments

`IPA.exe file-to-patch [arguments]` 

- `--launch`: Launch the game after patching
- `--revert`: Revert changes made by IPA (= unpatch the game)
- `--nowait`: Never keep the console open

Unconsumed arguments will be passed on to the game in case of `--launch`.
