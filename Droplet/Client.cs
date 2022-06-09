using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Droplet
{
    internal class Client : IDisposable
    {
        TcpClient client;

        public Client(string address) : base()
        {
            client = new TcpClient(address, Program.PORT);
        }

        public void Dispose()
        {
            ((IDisposable)client).Dispose();
        }

        public void Send(string message)
        {
            if (client.Client != null) {
                client.Client.Send(Encoding.UTF8.GetBytes(message));
            }
        }
    }
}
