namespace Droplet
{
    using System;
    using System.Collections.Generic;
    using System.DirectoryServices;
    using System.Threading.Tasks;
    using System.Runtime.InteropServices;
    using System.Security;
    using System.Collections;
    using System.Windows.Forms;
    using System.Net;
    using System.Net.Sockets;
    using System.Diagnostics.CodeAnalysis;
    using System.Text;

    public class Scanner
    {
        public struct Computer
        {
            public string Netbios;
            public string Username;
            public string Address;
            public DateTime Time;

            public void RefreshTime()
            {
                Time = DateTime.Now;
            }

            public override string ToString()
            {
                return $"{Username}@{Netbios}";
            }

            public override bool Equals([NotNullWhen(true)] object obj)
            {
                return obj is Computer computer && computer.Netbios == Netbios/* && computer.Username == Username && computer.Address == Address*/;
            }

            public override int GetHashCode()
            {
                return Netbios.GetHashCode() /*^ Username.GetHashCode() ^ Address.GetHashCode()*/;
            }

            public Computer(string netbios, string username, string address)
            {
                Netbios = netbios;
                Username = username;
                Address = address;
                Time = DateTime.Now;
            }
        }

        private const int TIMEOUT_SECONDS = 20;
        private const int SEND_BROADCAST_EVERY_MS = 2000;

        public event Action<Computer[]> OnComputerListUpdated;

        public Computer[] Computers => detectedComputers.ToArray();

        public Computer Me => me;

        private List<Computer> detectedComputers = new List<Computer>();

        private Computer me = new Computer(Environment.MachineName, Environment.UserName, "x.x.x.x");

        public Scanner()
        {
            var endpoint = new IPEndPoint(IPAddress.Any, Program.PORT);
            
            Task.Run(async () =>
            {
                UdpClient receiveClient;

                try {
                    receiveClient = new UdpClient(endpoint);
                }
                catch (Exception e) {
                    StaticLogger.Debug(e);
                    Application.Exit();
                    return;
                }

                while (true) {
                    try {
                        var result = await receiveClient.ReceiveAsync();

                        var recvBuffer = System.Text.Encoding.UTF8.GetString(result.Buffer);

                        StaticLogger.Debug($"Received {recvBuffer} at {DateTime.Now.ToLongTimeString()}");

                        string[] elements = recvBuffer.Trim().Split(' ');

                        if (elements.Length == 3) {
                            OnComputerDetected(new Computer(elements[0], elements[1], elements[2]));
                        }
                    }
                    catch(Exception e) {
                        StaticLogger.Debug(e);
                    }
                }
            });


            Task.Run(async () =>
            {
                UdpClient sendClient = new UdpClient();

                while (true) {
                    try {
                        Cleanup();

                        string bcast = IPAddress.Broadcast.ToString();

                        string me = $"{Environment.MachineName} {Environment.UserName.Replace(" ", string.Empty)} x.x.x.x";
                        var data = Encoding.UTF8.GetBytes(me);

                        StaticLogger.Debug($"Sending {me} at {DateTime.Now.ToLongTimeString()}");

                        sendClient.Send(data, data.Length, bcast, Program.PORT);
                    }
                    catch (Exception ex) {
                        StaticLogger.Debug(ex);
                    }

                    await Task.Delay(SEND_BROADCAST_EVERY_MS);
                }
            });
        }

        private void Cleanup(bool noUpdate=false)
        {
            bool anyChange = false;

            lock (detectedComputers) {
                anyChange = 0 != detectedComputers.RemoveAll(o => o.Time + TimeSpan.FromSeconds(TIMEOUT_SECONDS) < DateTime.Now);
            }


            if (anyChange && !noUpdate) {
                StaticLogger.Debug($"The list of clients was cleaned up and changed, firing up update");

                // v something about this makes it crash
                Task.Run(() => OnComputerListUpdated?.Invoke(detectedComputers.ToArray()));
            }
        }

        public void OnComputerDetected(Computer cpu)
        {
#if !DEBUG
            // It's me!
            if (cpu.Equals(me)) return;
#endif

            lock (detectedComputers) {
                bool added = false;

                for (int i = 0; i < detectedComputers.Count; i++) {
                    if (detectedComputers[i].Equals(cpu)) {
                        detectedComputers[i].RefreshTime(); // Refresh time
                        added = true;
                        break;
                    }
                }

                if (!added) {
                    detectedComputers.Add(cpu);
                }
            }

            Cleanup(noUpdate:true);

            Task.Run(() => OnComputerListUpdated?.Invoke(detectedComputers.ToArray()));
        }
    }
}