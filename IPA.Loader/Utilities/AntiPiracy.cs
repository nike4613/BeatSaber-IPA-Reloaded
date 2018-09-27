using System;
using System.IO;
using UnityEngine;
using Steamworks;

namespace IPA.Utilities.AntiPiracy
{
    /// <summary>
    /// Provides checks for whether or not the game is pirated.
    /// </summary>
    public static class PiracyChecks
    {
        /// <summary>
        /// Runs through a list of checks to detect whether a game is pirated
        /// </summary>
        /// <returns></returns>
        public static bool IsPirated
        {
            get
            {
                // Check for spoofed Steam Client
                if (BeatSaber.ReleaseType == BeatSaber.Release.Steam && IsSpoofedSteam())
                    return true;

                // Check for the presence of known pirated files
                if (HasKnownFiles())
                    return true;

                // If we get here, probably not a pirate
                return false;
            }
        }

        /// <summary>
        /// Check common Steam Emulator values for red flags
        /// </summary>
        /// <returns></returns>
        static bool IsSpoofedSteam()
        {
            // Always resolves to "IGGGAMES"
            string userName = SteamFriends.GetFriendPersonaName(SteamUser.GetSteamID());

            // Always resolves to "SteamFriends"
            string friendName = SteamFriends.GetFriendPersonaName(new CSteamID(76561198042581607));

            // Return if they both resolve to known spoofed values
            return userName == "IGGGAMES" && friendName == "SteamFriends";
        }

        /// <summary>
        /// Check for files that are present in pirated copies
        /// </summary>
        /// <returns></returns>
        static bool HasKnownFiles()
        {
            // All known files
            string[] paths = new string[]
            {
                Path.Combine(Application.dataPath, "Plugins", "valve.ini"),
                Path.Combine(Application.dataPath, "Plugins", "steam.ini"),
                Path.Combine(Application.dataPath, "Plugins", "huhuvr.ini"),
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "SmartSteamEmu.ini")),
            };

            // Check for the existence of each file
            foreach (string path in paths)
            {
                // If one is found, probably pirated
                if (File.Exists(path))
                    return true;
            }

            return false;
        }
    }
}