using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using IPA.Logging;

namespace IPA.Utilities
{
    /// <summary>
    /// Provides utilities for managing various critical sections.
    /// </summary>
    public static class CriticalSection
    {

        internal static void Configure()
        {
            Logger.log.Debug("Configuring exit handlers");

            ResetExitHandlers();
        }

        #region Execute section

        private static readonly Win32.ConsoleCtrlDelegate registeredHandler = HandleExit;
        internal static void ResetExitHandlers()
        {
            Win32.SetConsoleCtrlHandler(registeredHandler, false);
            Win32.SetConsoleCtrlHandler(registeredHandler, true);
            WinHttp.SetPeekMessageHook(PeekMessageHook);
        }

        private static class WinHttp
        {
            public delegate bool PeekMessageHook(
                bool isW,
                uint result,
                [MarshalAs(UnmanagedType.LPStruct)]
                in Win32.MSG message,
                IntPtr hwnd,
                uint filterMin,
                uint filterMax,
                ref Win32.PeekMessageParams removeMsg);

            [DllImport("bsipa-doorstop")]
            public static extern void SetPeekMessageHook(
                [MarshalAs(UnmanagedType.FunctionPtr)]
                PeekMessageHook hook);
        }

        private static Win32.ConsoleCtrlDelegate _handler = null;
        private static volatile bool isInExecuteSection = false;

        // returns true to continue looping and calling PeekMessage
        private static bool PeekMessageHook(
                bool isW,
                uint result,
                [MarshalAs(UnmanagedType.LPStruct)]
                in Win32.MSG message,
                IntPtr hwnd,
                uint filterMin,
                uint filterMax,
                ref Win32.PeekMessageParams removeMsg)
        {
            if (isInExecuteSection)
            {
                if (result == 0) return false;

                switch (message.message)
                {
                    case Win32.WM.CLOSE:
                        if (removeMsg != Win32.PeekMessageParams.PM_REMOVE)
                        {
                            removeMsg = Win32.PeekMessageParams.PM_REMOVE;
                            exitRecieved = true;
                            return true;
                        }
                        else
                        {
                            removeMsg = Win32.PeekMessageParams.PM_NOREMOVE;
                            return true;
                        }

                    default:
                        return false;
                }
            }

            return false;
        }

        private static bool HandleExit(Win32.CtrlTypes type)
        {
            if (_handler != null)
                return _handler(type);

            return false;
        } 

        private static volatile bool exitRecieved = false;

        /// <summary>
        /// Enters a critical execution section. Does not nest.
        /// </summary>
        /// <note>
        /// During a critical execution section, the program must execute until the end of the section before
        /// exiting. If an exit signal is recieved during the section, it will be canceled, and the process
        /// will terminate at the end of the section.
        /// </note>
        public static void EnterExecuteSection()
        {
            ResetExitHandlers();

            exitRecieved = false;
            _handler = sig => exitRecieved = true;
            isInExecuteSection = true;
        }

        /// <summary>
        /// Exits a critical execution section. Does not nest.
        /// </summary>
        /// <note>
        /// During a critical execution section, the program must execute until the end of the section before
        /// exiting. If an exit signal is recieved during the section, it will be canceled, and the process
        /// will terminate at the end of the section.
        /// </note>
        public static void ExitExecuteSection()
        {
            _handler = null;
            isInExecuteSection = false;

            if (exitRecieved)
                Environment.Exit(1);
        }

        #endregion

        #region GC section

        // i wish i could reference GC_enable and GC_disable directly
        [DllImport("mono-2.0-bdwgc")]
        private static extern void mono_unity_gc_enable();
        [DllImport("mono-2.0-bdwgc")]
        private static extern void mono_unity_gc_disable();

        /// <summary>
        /// Enters a GC critical section. Each call to this must be paired with a call to <see cref="ExitGCSection"/>.
        /// </summary>
        /// <note>
        /// During a GC critical section, no GCs will occur. 
        /// 
        /// This may throw an <see cref="EntryPointNotFoundException"/> if the build of Mono the game is running on does
        /// not have `mono_unity_gc_disable` exported. Use with caution.
        /// </note>
        public static void EnterGCSection()
        {
            mono_unity_gc_disable();
        }


        /// <summary>
        /// Exits a GC critical section. Each call to this must have a preceding call to <see cref="EnterGCSection"/>.
        /// </summary>
        /// <note>
        /// During a GC critical section, no GCs will occur. 
        /// 
        /// This may throw an <see cref="EntryPointNotFoundException"/> if the build of Mono the game is running on does
        /// not have `mono_unity_gc_enable` exported. Use with caution.
        /// </note>
        public static void ExitGCSection()
        {
            mono_unity_gc_enable();
        }

        #endregion

    }
}
