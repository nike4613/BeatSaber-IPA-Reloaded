namespace IPA.Config
{
    public static class ConfigWatchersHelper
    {
        public static void ToggleWatchers()
        {
            foreach (var watcher in ConfigRuntime.GetWatchers())
            {
                watcher.EnableRaisingEvents = !watcher.EnableRaisingEvents;
            }
        }
    }
}
