using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Droplet
{
    public class Server
    {
        private const int BUFFER_SIZE = 8 * 1024;
        public event Action<TcpClient, string> OnMessageReceived;
        public event Action<TcpClient, System.Exception> OnClientLost;
        public event Action<TcpClient, string> OnClientConnected;

        private Dictionary<string, TcpClient> clientsPerName = new Dictionary<string, TcpClient>();

        TcpListener listener;

        public Server() : base()
        {
            listener = new TcpListener(System.Net.IPAddress.Any, Program.PORT);

            Task.Run(Listen);
        }

        public TcpClient GetOtherClient(string machineName)
        {
            return clientsPerName.ContainsKey(machineName) ? clientsPerName[machineName] : null;
        }

        private string GetMachineName(TcpClient client)
        {
            IPEndPoint endPoint = (IPEndPoint)client.Client.RemoteEndPoint;
            // .. or LocalEndPoint - depending on which end you want to identify

            IPAddress ipAddress = endPoint.Address;

            // get the hostname
            IPHostEntry hostEntry = Dns.GetHostEntry(ipAddress);

            return hostEntry?.HostName?.Replace(".local", string.Empty);
        }

        private void Listen()
        {
            try {
                listener.Start();
            }
            catch (Exception ex) {
                Console.WriteLine(ex);
                Application.Exit();
                return;
            }

            while (true) {
                try {
                    var client = listener.AcceptTcpClient();
                    StaticLogger.Debug($"Accepted a new TCP client!");

                    var name = GetMachineName(client);
                    StaticLogger.Debug($"Resolved name {name} for TCP client!");
                    clientsPerName[name] = client;

                    OnClientConnected?.Invoke(client, name);

                    Task.Run(() => HandleClientMessages(client));
                }
                catch (Exception e) {
                    Console.WriteLine(e);
                }
            }
        }

        private void HandleClientMessages(TcpClient client)
        {
            List<byte> bytes = new List<byte>();
            while (true) {
                try {
                    var stream = client.GetStream();
                    var buff = new byte[BUFFER_SIZE];
                    int bytesRead = stream.Read(buff, 0, BUFFER_SIZE);

                    if (bytesRead != BUFFER_SIZE) {
                        byte[] finalBuff = new byte[bytesRead];
                        Array.Copy(buff, finalBuff, bytesRead);

                        bytes.AddRange(finalBuff);

                        OnMessageReceived?.Invoke(client, Encoding.UTF8.GetString(bytes.ToArray()));
                        bytes.Clear();
                    }
                    else {
                        bytes.AddRange(buff);
                    }
                }
                catch (Exception ex) {
                    string name = null;
                    lock (clientsPerName) {
                        foreach (var kv in clientsPerName) {
                            if (kv.Value == client) {
                                name = kv.Key;
                                break;
                            }
                        }
                    }

                    if (name != null) {
                        lock (clientsPerName) {
                            clientsPerName.Remove(name);
                        }
                    }

                    Task.Run(() => OnClientLost?.Invoke(client, ex));
                }
            }
        }
    }
}
