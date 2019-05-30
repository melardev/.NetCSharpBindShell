using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace DotNetCsharpReverseShellServer
{
    class TcpSyncNetService : INetService
    {
        private class ClientData
        {
            public NetworkStream NetworkStream { get; set; }
            public StreamReader Reader { get; set; }
            public int ClientId { get; set; }
            public StreamWriter Writer { get; set; }
            public Socket ClientSocket { get; set; }
        }

        public class OutputDataReceivedArgs
        {
            public int ClientId { get; set; }
            public string Line { get; set; }
        };

        private Socket _serverSocket;
        public event LineReceivedHandler OutputDataReceived;
        public event ClientAcceptedHandler ClientAccepted;
        public event DisconnectionHandler ClientDisconnected;

        private Dictionary<int, ClientData> Clients { get; set; } = new Dictionary<int, ClientData>();

        public delegate void LineReceivedHandler(object sender, OutputDataReceivedArgs args);

        public delegate void ClientAcceptedHandler(object sender, int clientId);

        public delegate void DisconnectionHandler(object sender, int clientId);


        public void Start(IPAddress iPAddress, int port)
        {
            IPEndPoint ipEndPoint = new IPEndPoint(iPAddress, 3002);

            _serverSocket = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp);
            _serverSocket.Bind(ipEndPoint);
            _serverSocket.Listen(0);
        }

        public void WriteLine(int clientId, string line)
        {
            if (!line.EndsWith("\n"))
                line += "\n";

            Write(clientId, line);
        }

        public void Write(int clientId, string output)
        {
            ClientData clientData = Clients[clientId];
            try
            {
                clientData.Writer.Write(output);
                clientData.Writer.Flush();
            }
            catch (IOException exception)
            {
                CloseAndNotify(clientData);
            }
        }

        public void AcceptOnClient()
        {
            Socket clientSocket = _serverSocket.Accept();
            ClientData clientData = new ClientData
            {
                ClientId = (int) clientSocket.Handle,
                ClientSocket = clientSocket,
                NetworkStream = new NetworkStream(clientSocket, FileAccess.ReadWrite)
            };


            Clients.Add((int) clientSocket.Handle, clientData);

            clientData.Reader = new StreamReader(clientData.NetworkStream);
            clientData.Writer = new StreamWriter(clientData.NetworkStream);
            ClientAccepted?.Invoke(this, (int) clientSocket.Handle);
        }

        public void ReadSync(int clientId)
        {
            ClientData clientData = Clients[clientId];
            try
            {
                while (true)
                {
                    string line = clientData.Reader.ReadLine();
                    OutputDataReceived?.Invoke(this, new OutputDataReceivedArgs
                    {
                        ClientId = (int) clientData.ClientSocket.Handle,
                        Line = line
                    });
                }
            }
            catch (IOException exception)
            {
                CloseAndNotify(clientData);
            }
        }

        public void InteractAsync(int clientId)
        {
            new Thread(() => { ReadSync(clientId); }).Start();
        }

        private void CloseAndNotify(ClientData clientData)
        {
            Close(clientData);
            ClientDisconnected?.Invoke(this, clientData.ClientId);
        }

        private void Close(ClientData clientData)
        {
            clientData.Writer.Close();
            clientData.Reader.Close();
            clientData.NetworkStream.Close();
        }

        public void Shutdown()
        {
            _serverSocket.Close();
            foreach (KeyValuePair<int, ClientData> clientData in Clients)
            {
                Close(clientData.Value);
            }
        }
    }
}