using System.Net;

namespace DotNetCsharpReverseShellServer
{
    interface INetService
    {
        void Start(IPAddress ipAddress, int port);
        void WriteLine(int clientId, string output);
        void AcceptOnClient();
        void InteractAsync(int clientId);
        void ReadSync(int clientId);
        void Write(int clientId, string output);
        void Shutdown();
    }
}