using System;

namespace IPA.Logging
{
    /// <summary>
    /// The log printer's base class.
    /// </summary>
    public abstract class LogPrinter
    {
        /// <summary>
        /// Provides a filter for which log levels to allow through.
        /// </summary>
        /// <value>the level to filter to</value>
        public abstract Logger.LogLevel Filter { get; set; }

        /// <summary>
        /// Prints a provided message from a given log at the specified time.
        /// </summary>
        /// <param name="level">the log level</param>
        /// <param name="time">the time the message was composed</param>
        /// <param name="logName">the name of the log that created this message</param>
        /// <param name="message">the message</param>
        public abstract void Print(Logger.Level level, DateTime time, string logName, string message);

        /// <summary>
        /// Called before the first print in a group. May be called multiple times.
        /// Use this to create file handles and the like.
        /// </summary>
        public virtual void StartPrint() { }

        /// <summary>
        /// Called after the last print in a group. May be called multiple times.
        /// Use this to dispose file handles and the like.
        /// </summary>
        public virtual void EndPrint() { }

        internal DateTime LastUse { get; set; }
    }
}