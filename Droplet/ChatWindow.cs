namespace Droplet
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Data;
    using System.Drawing;
    using System.Linq;
    using System.Media;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using System.IO;
    using System.Net.Sockets;

    public partial class ChatWindow : Form
    {
        TcpClient otherClient;
        Client client;
        Server server;
        SoundPlayer player = new SoundPlayer(Properties.Resources.snd_message);
        bool isWizzing = false;

        readonly Color myColor;
        readonly Color otherColor;
        readonly Scanner.Computer other;
        readonly Scanner.Computer me;

        public ChatWindow(Scanner.Computer me, Color myColor, Scanner.Computer other, Color otherColor, Server server)
        {

            this.myColor = myColor;
            this.me = me;
            this.other = other;
            this.otherColor = otherColor;
            this.server = server;

            InitializeComponent();

            inputBox.Enabled = false;
            chatLogWindow.LinkClicked += ChatLogWindow_LinkClicked;

            Text = $"{other} - droplet v{Program.VERSION}";

            Task.Run(InitializeChat);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Control | Keys.V:

                    if (Clipboard.ContainsImage())
                    {
                        string final = string.Empty;
                        var image = Clipboard.GetImage();
                        using (MemoryStream ms = new MemoryStream())
                        {
                            image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);

                            final = ByteEncodings.ByteEncoding.Base64Safe.GetString(ms.ToArray());
                            AppendImage(ms.ToArray(), local:true);
                        }

                        client?.Send($"/image {final}");
                    }

                    return true;

                default:
                    return base.ProcessCmdKey(ref msg, keyData);
            }
        }

        private void ChatLogWindow_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(e.LinkText);
        }

        private async Task InitializeChat()
        {
            server.OnMessageReceived += Server_OnMessageReceived;
            server.OnClientLost += Server_OnClientLost;
            inputBox.KeyPress += InputBox_KeyPress;

            await AttemptConnect();
        }

        private async Task AttemptConnect()
        {
            client = null;
            otherClient = null;

            while (client == null) {
                try {
                    AppendLine($"Connecting to {other}...");
                    client = new Client(other.Netbios);
                }
                catch (Exception e) {
                    AppendLine($"Could not connect to {other}: {e.Message}");
                }
            }

            while (otherClient == null) {
                otherClient = server.GetOtherClient(other.Netbios);

                if (otherClient == null) {
                    AppendLine($"Awaiting connection from {other}...");
                    await Task.Delay(1000);
                }
            }

            Action action = () =>
            {
                inputBox.Enabled = true;

                AppendLine($"Connected to {other}!");
            };

            if (this.InvokeRequired) {
                this.Invoke(action);
            }
            else {
                action();
            }
        }

        private void Server_OnClientLost(System.Net.Sockets.TcpClient client, Exception e)
        {
            if (client == otherClient) {
                // Should close the window maybe?
                AppendLine($"Client {other} is dead: {e.Message}. Attempting reconnection...");
                _ = AttemptConnect();
            }
        }

        private void InputBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return)
            {
                string txt = inputBox.Text.Trim();
                if (txt.Length > 0)
                {
                    client?.Send(txt);
                    if (txt[0] != '/')
                    {
                        AppendLine($"<{me}> {txt}", myColor);
                    }

                    inputBox.Clear();
                }

                e.Handled = true;
            }
        }

        public void Server_OnMessageReceived(System.Net.Sockets.TcpClient client, string message)
        {
            if (client != otherClient) return;
            if (!Visible) return;
            if (message.Length == 0) return;

            if (message[0] == '/')
            {
                var components = message.Substring(1).Split(' ');
                var cmd = components[0];

                switch (cmd.ToLower())
                {
                    case "image":
                        if (components.Length > 1)
                        {
                            string b64 = components[1].Trim();

                            byte[] arr = new byte[b64.Length / 4 + 1];

                            ByteEncodings.ByteEncoding.Base64Safe.ToBytes(b64, arr);
                            AppendImage(arr);
                            player.Play();
                        }

                        break;

                    case "wizz":
                        AppendLine($"{other} sent you a wizz!", otherColor);
                        Wizz();
                        player.Play();
                        break;
                }

                return;
            }

            player.Play();

            AppendLine($"<{other}> {message.Trim()}", otherColor);
        }

        private void AppendImage(byte[] data, bool local=false)
        {
            Image image = null;

            try
            {
                using (System.IO.MemoryStream ms = new System.IO.MemoryStream(data))
                {
                    image = Image.FromStream(ms);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
            }

            if (image != null)
            {
                Action setImage = () =>
                {
                    Clipboard.SetImage(image);

                    if (Clipboard.ContainsImage())
                    {
                        DataFormats.Format format = DataFormats.GetFormat(DataFormats.Bitmap);

                        chatLogWindow.ReadOnly = false;
                        if (chatLogWindow.CanPaste(format))
                        {
                            if (local)
                            {
                                AppendLine($"<{me}>", myColor);
                            }
                            else
                            {
                                AppendLine($"<{other}>", otherColor);
                            }

                            chatLogWindow.Paste(format);
                            chatLogWindow.ReadOnly = true;

                            chatLogWindow.AppendText(Environment.NewLine);
                        }
                    }

                    image.Dispose();
                };

                this?.Invoke(setImage);
            }
        }
        private void AppendLine(string line)
        {
            AppendLine(line, Color.Black);
        }

        private void AppendLine(string line, Color color)
        {
            Action write = () =>
            {
                chatLogWindow.SelectionColor = color;
                chatLogWindow.AppendText($"{line}");
                chatLogWindow.AppendText(Environment.NewLine);
                chatLogWindow.SelectionColor = Color.Black;

                // Scroll to end
                chatLogWindow.ScrollToCaret();
            };

            if (this.InvokeRequired)
            {
                this.Invoke(write);
            }
            else
            {
                write();
            }
        }

        private void Wizz()
        {
            if (isWizzing) return;

            isWizzing = true;

            TimeSpan wizzDuration = TimeSpan.FromSeconds(1);
            Task.Run(async () =>
            {
                var wizzStart = DateTime.Now;

                Point originalLocation = Location;
                Random random = new Random();

                while (wizzStart + wizzDuration > DateTime.Now)
                {
                    Action a = () =>
                    {
                        Location = new Point(originalLocation.X + random.Next(10) - 5, originalLocation.Y + random.Next(10) - 5);
                    };

                    this?.Invoke(a);

                    await Task.Delay(10);
                }

                isWizzing = false;
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            server.OnMessageReceived -= Server_OnMessageReceived;
            client?.Dispose();
            base.OnClosed(e);
        }
    }
}
