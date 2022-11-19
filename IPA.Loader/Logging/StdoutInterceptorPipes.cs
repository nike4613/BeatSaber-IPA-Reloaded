using System;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;

namespace IPA.Logging
{
    internal static class StdoutInterceptorPipes
    {
        // Used to ensure the server starts first, as Mono struggles with this simple task.
        // Otherwise it would throw a ERROR_PIPE_CONNECTED Win32Exception.
        private static readonly ManualResetEvent manualResetEvent = new(false);

        public static bool ShouldRedirectStdHandles;

        public static void Initialize()
        {
            InitializePipe(STD_OUTPUT_HANDLE);
            InitializePipe(STD_ERROR_HANDLE);
        }

        private static void InitializePipe(int stdHandle)
        {
            var pipeName = stdHandle == STD_OUTPUT_HANDLE ? "STD_OUT_PIPE" : "STD_ERR_PIPE";
            var serverThread = InstantiateServerThread(pipeName, stdHandle);
            serverThread.Start();
            var clientThread = InstantiateClientThread(pipeName, stdHandle);
            clientThread.Start();
        }

        private static Thread InstantiateServerThread(string pipeName, int stdHandle)
        {
            return new Thread(() =>
            {
                NamedPipeServerStream pipeServer = new(pipeName, PipeDirection.In);

                try
                {
                    // If the client starts first, releases the client thread.
                    manualResetEvent.Set();
                    pipeServer.WaitForConnection();
                    var buffer = new byte[1024];
                    while (pipeServer.IsConnected)
                    {
                        if (ShouldRedirectStdHandles)
                        {
                            // Separate method to avoid a BadImageFormatException when accessing StdoutInterceptor early.
                            // This happens because the Harmony DLL is not loaded at this point.
                            Redirect(pipeServer, buffer, stdHandle);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    pipeServer.Close();
                }
            });
        }

        private static Thread InstantiateClientThread(string pipeName, int stdHandle)
        {
            return new Thread(() =>
            {
                NamedPipeClientStream pipeClient = new(".", pipeName, PipeDirection.Out);

                try
                {
                    // If the client starts first, blocks the client thread.
                    manualResetEvent.WaitOne();
                    pipeClient.Connect();
                    SetStdHandle(stdHandle, pipeClient.SafePipeHandle.DangerousGetHandle());
                    while (pipeClient.IsConnected)
                    {
                        // Keeps the thread alive.
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    pipeClient.Close();
                }
            });
        }

        private static void Redirect(NamedPipeServerStream server, byte[] buffer, int stdHandle)
        {
            var charsRead = server.Read(buffer, 0, buffer.Length);
            var interceptor = stdHandle == STD_OUTPUT_HANDLE ? StdoutInterceptor.Stdout : StdoutInterceptor.Stderr;
            interceptor!.Write(Encoding.UTF8.GetString(buffer, 0, charsRead));
        }

        [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [ResourceExposure(ResourceScope.Process)]
        private static extern bool SetStdHandle(int nStdHandle, IntPtr hHandle);

        private const int STD_OUTPUT_HANDLE = -11;
        private const int STD_ERROR_HANDLE = -12;
    }
}