using UnityEngine;

namespace IPA.Logging
{
    internal static class UnityLogProvider
    {
        internal static Logger Logger;
        public static Logger UnityLogger => Logger ?? (Logger = new StandardLogger("UnityEngine"));
    }

    internal static class UnityLogRedirector
    {
        public static Logger.Level LogTypeToLevel(LogType type)
        {
            switch (type)
            {
                case LogType.Assert:
                    return Logger.Level.Debug;
                case LogType.Error:
                    return Logger.Level.Error;
                case LogType.Exception:
                    return Logger.Level.Critical;
                case LogType.Log:
                    return Logger.Level.Info;
                case LogType.Warning:
                    return Logger.Level.Warning;
                default:
                    return Logger.Level.Info;
            }
        }
    }
}
