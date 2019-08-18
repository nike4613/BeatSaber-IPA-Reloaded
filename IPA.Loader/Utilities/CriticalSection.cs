using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using IPA.Logging;

namespace IPA.Utilities
{
    public static class CriticalSection
    {

        internal static void Configure()
        {
            Logger.log.Debug("Configuring exit handlers");

            ResetExitHandlers();
        }

        #region Execute section

        private static readonly EventHandler registeredHandler = HandleExit;
        internal static void ResetExitHandlers()
        {
            SetConsoleCtrlHandler(registeredHandler, false);
            SetConsoleCtrlHandler(registeredHandler, true);
        }

        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

        private delegate bool EventHandler(CtrlType sig);
        private static EventHandler _handler = null;

        private static bool HandleExit(CtrlType type)
        {
            if (_handler != null)
                return _handler(type);

            return false;
        } 

        enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        private static volatile bool exitRecieved = false;

        public static void EnterExecuteSection()
        {
            ResetExitHandlers();

            exitRecieved = false;
            _handler = sig => exitRecieved = true;
        }

        public static void ExitExecuteSection()
        {
            _handler = null;

            if (exitRecieved)
                Environment.Exit(1);
        }

        #endregion

    }
}
