BSIPA
=====

BSIPA is a mod loader based off of [Illusion Plugin Architecture](https://github.com/russianGecko/IPA-Reloaded), designed for Beat Saber.
While it retains backwards compatability, the recommended interface has been completely redesigned for a more consistent and stable installation.

Taking some inspiration from mod injectors like [BepInEx](https://github.com/BepInEx/BepInEx), it uses a fork of [Unity Doorstop](https://github.com/NeighTools/UnityDoorstop)
to actually inject itself into the game.

***

The particular method of injection that BSIPA uses lets its user experience be far nicer than most others, not requiring a repatch for every game update.

With updating the game being seamless, it also makes sense to make updating the mods themselves seamless. BSIPA's internal updater will automatically ask [BeatMods](https://beatmods.com/)
for newer versions of your installed mods. Of course, this is configurable either in the config at `UserData\Beat Saber IPA.json` or with the Mod List, which lets you control the process
to a greater extend than even the configs.