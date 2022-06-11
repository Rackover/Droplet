namespace Droplet
{
    using Droplet.Properties;
    using System;
    using System.Windows.Forms;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Drawing;
    using System.Net;

    public class Tray : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private Server server;
        private Scanner scanner;

        Dictionary<string, ChatWindow> netbiosChatting = new Dictionary<string, ChatWindow>();

        public Tray()
        {
            // Initialize Tray Icon
            trayIcon = new NotifyIcon() {
                Icon = Resources.droplet,
                Visible = true
            };

            server = new Server();
            server.OnClientConnected += Server_OnClientConnected;

            scanner = new Scanner();
            scanner.OnComputerListUpdated += GenerateContextMenuStrip;

            GenerateContextMenuStrip(scanner.Computers);

            trayIcon.ShowBalloonTip(100, "Droplet", "Droplet started", ToolTipIcon.Info);
        }

        private void Server_OnClientConnected(System.Net.Sockets.TcpClient arg1, string hostName)
        {
            if (arg1 != null && hostName.Length != 0) {
                lock (scanner.Computers) {
                    for (int i = 0; i < scanner.Computers.Length; i++) {
                        if (scanner.Computers[i].Netbios == hostName) {
                            return;
                        }
                    }

                    var cpu = new Scanner.Computer(hostName, "???", "x.x.x.x");

                    StaticLogger.Info($"Manual discovery of computer {cpu}");
                    scanner.OnComputerDetected(cpu);

                    //Action a = () =>
                    //{
                    //    lock (netbiosChatting) {
                    //        if (netbiosChatting.ContainsKey(hostName)) {
                    //            if (netbiosChatting[hostName].Visible) {
                    //                // nothing to do
                    //            }
                    //            else {
                    //                netbiosChatting[hostName].Show();
                    //                netbiosChatting[hostName].Server_OnMessageReceived(arg1, hostName);
                    //            }
                    //        }
                    //        else {
                    //            var window = ChatWith(cpu);
                    //            window.Server_OnMessageReceived(arg1, hostName);
                    //        }
                    //    }
                    //};

                    // then what ?
                }

            }
        }

        private void GenerateContextMenuStrip(Scanner.Computer[] computers)
        {
            // Here we could be in any thread
            // ... which I guess is OK because ApplicationContext does not have Invoke() nor InvokeRequired
            //   so if this did not work, we would be stuck

            var strip = new ContextMenuStrip();

            if (computers.Length > 0) {
                for (int i = 0; i < computers.Length; i++) {
                    Scanner.Computer computer = computers[i];
                    strip.Items.Add(new ToolStripButton(computer.ToString(), null, (a, b) =>
                    {
                        ChatWith(computer);
                    }));
                }

                strip.Items.Add(new ToolStripSeparator());
            }

            strip.Items.Add(new ToolStripButton("Exit", null, Exit));

            trayIcon.ContextMenuStrip = strip;
        }

        ChatWindow ChatWith(Scanner.Computer computer)
        {
            Color myColor = Color.Blue;
            Color otherColor = Color.Purple;

            string me = Environment.MachineName.ToUpper();
            string other = computer.ToString().ToUpper();

            if (me == "DESKTOP-0T4G12U") myColor = Color.DarkMagenta;
            if (other == "DESKTOP-0T4G12U") otherColor = Color.DarkMagenta;
            if (me == "RACKOVER-770") myColor = Color.DeepPink;
            if (other == "RACKOVER-770") otherColor = Color.DeepPink;

            ChatWindow window;

            if (netbiosChatting.TryGetValue(me, out window)) {
                lock (netbiosChatting) {
                    netbiosChatting.Remove(me);
                }
            }

            System.Diagnostics.Debug.WriteLine($"Showing window");

            var context = TaskScheduler.FromCurrentSynchronizationContext();

            Task.Factory.StartNew(() =>
                {
                    window = new ChatWindow(scanner.Me, myColor, computer, otherColor, server);
                    window.Show();
                }, 
                System.Threading.CancellationToken.None,
                TaskCreationOptions.None,
                context
            );

            //trayIcon.ContextMenuStrip.Invoke(showChatWindow);


            lock (netbiosChatting) {
                netbiosChatting.Add(computer.Netbios, window);
            }

            return window;
        }

        void Exit(object sender, EventArgs e)
        {
            // Hide tray icon, otherwise it will remain shown until user mouses over it
            trayIcon.Visible = false;
            server.OnClientConnected -= Server_OnClientConnected;

            Application.Exit();
        }
    }

}