using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using LoggerBase = IllusionPlugin.Logging.Logger;

namespace IllusionInjector.Logging
{
    public class UnityLogInterceptor
    {
        public static LoggerBase Unitylogger = new StandardLogger("UnityEngine");

        public static LoggerBase.Level LogTypeToLevel(LogType type)
        {
            switch (type)
            {
                case LogType.Assert:
                    return LoggerBase.Level.Debug;
                case LogType.Error:
                    return LoggerBase.Level.Error;
                case LogType.Exception:
                    return LoggerBase.Level.Critical;
                case LogType.Log:
                    return LoggerBase.Level.Info;
                case LogType.Warning:
                    return LoggerBase.Level.Warning;
                default:
                    return LoggerBase.Level.Info;
            }
        }
    }
}
