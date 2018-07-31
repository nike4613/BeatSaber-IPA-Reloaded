using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IllusionPlugin.Logging
{
    public abstract class LogPrinter
    {
        public abstract Logger.LogLevel Filter { get; set; }
        public abstract void Print(Logger.Level level, DateTime time, string logName, string message);
        public virtual void StartPrint() { }
        public virtual void EndPrint() { }
    }
}
