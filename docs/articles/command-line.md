---
uid: articles.command_line
---

# The Command Line

BSIPA supports a number of useful command-line arguments.

Here's a quick list of what they are and what they do.

- `--verbose`

  > Makes a console appear with log information at startup.
  >
  > Optionally, an explicit process ID can be specified to start the game with an external console. This allows it to be
  > launched via Steam without being interrupted by its "Allow game launch?" if launched directly from the `.exe`.
  > 
  > Example for Beat Saber using PowerShell:
  >
  >   ```
  >   .\steam.exe -applaunch 620980 --verbose $PID
  >   ```
  >
  > Do note that this isn't going to work from an elevated terminal.

- `--debug`

  > Enables the loading of debug information in Mono. The debugging information must be in the portable PDB format,
  > in the same location as the DLL that it's for.
  >
  > This option also forces BSIPA to show all debug messages in the console, as well as where they were called.
  >
  > This overrides the config settings `Debug.ShowDebug` and `Debug.ShowCallSource`.

- `--trace`

  > Enables trace level messages. By default, they do not ever enter the message queue, and thus cost almost nothing.
  > When this or the config option is used, they are added and logged with the same rules as Debug messages.
  >
  > This overrides the config setting `Debug.ShowTrace`.

- `--no-yeet`

  > Disables mod yeeting.
  >
  > By default, whenever BSIPA detects that the game is now running a newer version than previous runs, it will move all
  > mods to another folder and not load them. (They still get checked for updates though.) When this is enabled, that
  > behaviour is disabled.
  >
  > Overrides the config setting `YeetMods`.

- `--condense-logs`

  > Reduces the number of log files BSIPA will output for a given session.
  >
  > By default, BSIPA will create a subfolder in the `Logs` folder for each mod sublog, as well as each mod. This disables
  > that behaviour, and restricts it to only create a global log and mod logs.
  >
  > Overrides the config setting `Debug.CondenseModLogs`.

- `--plugin-logs`

  > Causes each plugins' log messages to be written to files in their own folder for ease of debugging.
  >
  > This was the default through 4.1.6, however is now disabled by default.
  >
  > Overrides the config setting `Debug.CreateModLogs`.

- `-vrmode`

  > Allows changing the OpenXR runtime. Value must be a substring of the runtime filename. 
  > 
  > Set to `none` to disable VR by forcing an invalid runtime.

***
