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

        private static void Reset(object sender, EventArgs e)
        {
            Win32.SetConsoleCtrlHandler(registeredHandler, false);
            WinHttp.SetPeekMessageHook(null);
        }

        #region Execute section

        private static readonly Win32.ConsoleCtrlDelegate registeredHandler = HandleExit;
        internal static void ResetExitHandlers()
        {
            Win32.SetConsoleCtrlHandler(registeredHandler, false);
            Win32.SetConsoleCtrlHandler(registeredHandler, true);
            WinHttp.SetPeekMessageHook(PeekMessageHook);

            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }

        private static void OnProcessExit(object sender, EventArgs args)
        {
            WinHttp.SetIgnoreUnhandledExceptions(true);
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

            [DllImport("bsipa-doorstop")]
            public static extern void SetIgnoreUnhandledExceptions(
                [MarshalAs(UnmanagedType.Bool)] bool ignore);
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
        /// A struct that allows <c>using</c> blocks to manage an execute section.
        /// </summary>
        public struct AutoExecuteSection : IDisposable
        {
            private readonly bool constructed;
            internal AutoExecuteSection(bool val) 
            {
                constructed = val && !isInExecuteSection;
                if (constructed)
                    EnterExecuteSection();
            }

            void IDisposable.Dispose()
            {
                if (constructed)
                    ExitExecuteSection();
            }
        }

        /// <summary>
        /// Creates an <see cref="AutoExecuteSection"/> for automated management of an execute section.
        /// </summary>
        /// <returns>the new <see cref="AutoExecuteSection"/> that manages the section</returns>
        public static AutoExecuteSection ExecuteSection() => new AutoExecuteSection(true);

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

            Reset(null, null);

            if (exitRecieved)
                Environment.Exit(1);
        }

        #endregion
    }
}
