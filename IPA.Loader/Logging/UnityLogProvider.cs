#nullable enable
using UnityEngine;

namespace IPA.Logging
{
    internal static class UnityLogProvider
    {
        private static readonly StandardLogger UnityLogger = new("UnityEngine");

        public static void RedirectUnityLogging()
        {
            StdoutInterceptor.RedirectConsole();
            Application.logMessageReceivedThreaded += delegate(string logString, string stackTrace, LogType type)
            {
                var level = UnityLogRedirector.LogTypeToLevel(type);
                var message = logString;

                if (!string.IsNullOrEmpty(stackTrace))
                {
                    message += $"\n{stackTrace}";
                }

                UnityLogger.Log(level, message);
            };
        }
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
