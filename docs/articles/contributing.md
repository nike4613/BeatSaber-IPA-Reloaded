---
uid: articles.contributing
---

# Contributing

## Prerequisites

- Microsoft Visual Studio 2019 or later (2017 may work, no guarantees)
- Tools for C/C++ (MSVC) v141
- .NET 4.6.1 SDK and .NET 4.7.2 SDK
- Beat Saber (if developing for .NET 4.5+)
- Muse Dash (if developing for .NET 3.5)

## Building

1. Clone with `git clone https://github.com/beat-saber-modding-group/BeatSaber-IPA-Reloaded.git --recursive`
2. Create a file, `bsinstalldir.txt` in the solution root. Do NOT create this in Visual Studio; VS adds a BOM at the begginning of the file that the tools used cannot read.
   It should contain the path to your Beat Saber installation, using forward slashes with a trailing slash. e.g.

   ```
   C:/Program Files (x86)/Steam/steamapps/common/Beat Saber/
   ```

   If you intend to be doing .NET 3.5 centric development, you must put your Muse Dash installation folder in a file named `mdinstalldir.txt` that is otherwise identical to
   `bsinstalldir.txt`.

3. Open `BSIPA.sln` in Visual Studio.
4. Choose the configuration that you intend to target during development.
5. Rebuild all.

   When you make a change somewhere in BSIPA itself, right click on `BSIPA-Meta` and click `Build` or `Rebuild`. This sets up the output in `path/to/solution/BSIPA-Meta/bin/<Configuration>` to be what
   should be copied to the game directory.

   When making a change to Mod List, you only need to build Mod List itself. Install by copying everything in `path/to/solution/BSIPA-ModList/bin/<Configuration>` to your game
   directory.

When building a Debug build, all referenced assemblies from Beat Saber will be copied from the install directory provided in `bsinstalldir.txt` into `Refs/`. Any new references
should reference the copy in there. When building for Release, it just uses the files already in `Refs/`.
