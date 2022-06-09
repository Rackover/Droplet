using System;
using System.Windows.Forms;

namespace Droplet
{
    internal static class Program
    {
        public const ushort PORT = 2999;

        public const byte VERSION = 3;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            StaticLogger.Initialize(new Logger($"Droplet.{Environment.MachineName}", outputToFile: true, outputToConsole: true, outputFilePathFormat: "{0}{1}.log"));

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.Run(new Tray());
        }
    }
}
