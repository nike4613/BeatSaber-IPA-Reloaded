using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Doorstop
{
    internal static class Entrypoint
    {
        public static void Start()
        {
            try
            {
                var injectorFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "IPA.Injector.dll");

                if (!File.Exists(injectorFilePath))
                {
                    throw new FileNotFoundException("Could not find Injector. BSIPA installation is corrupted.");
                }

                Assembly.LoadFrom(injectorFilePath).GetType("IPA.Injector.Injector")!.GetMethod("Inject", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null);
            }
            catch (Exception ex)
            {
                MessageBox(IntPtr.Zero, ex.ToString(), "BSIPA", MbIconError | MbSetForeground | MbSystemModal);
            }
        }

        private const uint MbIconError = 0x10;
        private const uint MbSetForeground = 0x10000;
        private const uint MbSystemModal = 0x1000;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
    }
}
