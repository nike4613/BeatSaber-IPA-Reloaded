using System;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace IPA.Logging
{
    internal static class StdoutInterceptorPipes
    {
        // Used to ensure the server starts first, as Mono struggles with this simple task.
        // Otherwise it would throw a ERROR_PIPE_CONNECTED Win32Exception.
        private static readonly ManualResetEventSlim manualResetEvent = new(false);

        public static bool ShouldRedirectStdHandles;

        public static void Initialize()
        {
            InitializePipe(StdOutputHandle);
            InitializePipe(StdErrorHandle);
        }

        private static void InitializePipe(int stdHandle)
        {
            // Makes sure that we won't get a ERROR_PIPE_BUSY Win32Exception
            // if the pipe wasn't closed fast enough when restarting the game.
            var pipeName = Guid.NewGuid().ToString();
            var serverThread = InstantiateServerThread(pipeName, stdHandle);
            serverThread.Start();
            var clientThread = InstantiateClientThread(pipeName, stdHandle);
            clientThread.Start();
        }

        private static Thread InstantiateServerThread(string pipeName, int stdHandle)
        {
            return new Thread(() =>
            {
                var pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.In);

                try
                {
                    // If the client starts first, releases the client thread.
                    manualResetEvent.Set();
                    pipeServer.WaitForConnection();
                    var buffer = new byte[1024];
                    while (pipeServer.IsConnected)
                    {
                        // TODO: Figure out why this line is absolutely needed
                        // to avoid blocking the main thread when ShouldRedirectStdHandles is false.
                        var length = pipeServer.Read(buffer, 0, buffer.Length);
                        if (ShouldRedirectStdHandles)
                        {
                            // Separate method to avoid a BadImageFormatException when accessing StdoutInterceptor early.
                            // This happens because the Harmony DLL is not loaded at this point.
                            WriteToInterceptor(length , buffer, stdHandle);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }

                pipeServer.Close();
                manualResetEvent.Dispose();
            });
        }

        private static Thread InstantiateClientThread(string pipeName, int stdHandle)
        {
            return new Thread(() =>
            {
                var pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);

                try
                {
                    // If the client starts first, blocks the client thread.
                    manualResetEvent.Wait();
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
                }

                pipeClient.Close();
                manualResetEvent.Dispose();
            });
        }

        private static void WriteToInterceptor(int length, byte[] buffer, int stdHandle)
        {
            var interceptor = stdHandle == StdOutputHandle ? StdoutInterceptor.Stdout : StdoutInterceptor.Stderr;
            interceptor!.Write(Encoding.UTF8.GetString(buffer, 0, length));
        }

        [DllImport("kernel32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern bool SetStdHandle(int nStdHandle, IntPtr hHandle);

        private const int StdOutputHandle = -11;
        private const int StdErrorHandle = -12;
    }
}