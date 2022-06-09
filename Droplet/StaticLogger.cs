using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Droplet
{
    public static class StaticLogger
    {
        public static Logger SingleLogger { private set; get; }

        public static void Initialize(Logger logger)
        {
            SingleLogger = logger;
        }
        public static void Trace(object msgs, [CallerFilePath] string filePath = "") { SingleLogger.Trace(msgs, filePath); }
        public static void Debug(object msgs, [CallerFilePath] string filePath = "") { SingleLogger.Debug(msgs, filePath); }
        public static void Info(object msgs, [CallerFilePath] string filePath = "") { SingleLogger.Info(msgs, filePath); }
        public static void Warn(object msgs, [CallerFilePath] string filePath = "") { SingleLogger.Warn(msgs, filePath); }
        public static void Error(object msgs, [CallerFilePath] string filePath = "") { SingleLogger.Error(msgs, filePath); }
    }
}
