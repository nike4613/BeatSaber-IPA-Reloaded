using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace IPA.Logging
{
    internal class UnityLogInterceptor
    {
        public static Logger UnityLogger = new StandardLogger("UnityEngine");

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
