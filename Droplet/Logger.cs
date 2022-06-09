using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Droplet
{
    public class Logger
    {
        public enum LEVEL { TRACE, DEBUG, WARNING, INFO, ERROR };
        public LEVEL Level { private set; get; }

        readonly string logFilePath;
        readonly bool outputToFile;
        readonly bool outputToConsole;
        readonly string programName;

        CultureInfo culture = new CultureInfo("fr-FR");
        Dictionary<LEVEL, ConsoleColor> colors = new Dictionary<LEVEL, ConsoleColor>()
        {
            {LEVEL.TRACE, ConsoleColor.Magenta },
            {LEVEL.DEBUG, ConsoleColor.Gray },
            {LEVEL.INFO, ConsoleColor.White },
            {LEVEL.WARNING, ConsoleColor.Yellow },
            {LEVEL.ERROR, ConsoleColor.Red }
        };

        int flushEvery = 1000;


        StreamWriter logWriter = null;
        Timer flushTimer;
        Action<object> consoleLogFunction = (o) => System.Diagnostics.Debug.WriteLine(o);

        public Logger(string programName = null, bool outputToFile = false, bool outputToConsole = true, string outputFilePathFormat = @"logs/{0}{1}.log")
        {
            if (programName == null) programName = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);

            this.logFilePath = outputFilePathFormat;
            this.programName = programName;
            this.outputToFile = outputToFile;
            this.outputToConsole = outputToConsole;

            if (outputToFile) {
                var filePath = string.Format(logFilePath, this.programName, "");
                //Directory.CreateDirectory(
                //Path.GetDirectoryName(
                //        filePath
                //    )
                //);

                logWriter = null;

                int i = 0;
                while (logWriter == null) {
                    try {
                        filePath = string.Format(logFilePath, this.programName, i == 0 ? "" : i.ToString());
                        if (File.Exists(filePath)) File.Delete(filePath);

                        var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                        logWriter = new StreamWriter(fs, Encoding.UTF8);
                    }
                    catch (IOException) {
                        // File is locked - increment and retry
                        i++;
                    }

                    if (i > 10) {
                        Console.WriteLine("After " + i + " attempts, could not access file " + logFilePath + ", giving up.");
                        return;
                    }
                }

                flushTimer = new Timer(
                    e => {
                        logWriter.Flush();
                    },
                    null,
                    TimeSpan.Zero,
                    TimeSpan.FromMilliseconds(flushEvery));
            }
        }

        public void SetLevel(LEVEL level)
        {
            this.Level = level;
        }

        public void SetConsoleFunction(Action<object> function)
        {
            this.consoleLogFunction = function;
        }

        public void Trace(object msgs, [CallerFilePath] string filePath = "") { LogMessage(LEVEL.TRACE, msgs, filePath); }
        public void Debug(object msgs, [CallerFilePath] string filePath = "") { LogMessage(LEVEL.DEBUG, msgs, filePath); }
        public void Info(object msgs, [CallerFilePath] string filePath = "") { LogMessage(LEVEL.INFO, msgs, filePath); }
        public void Warn(object msgs, [CallerFilePath] string filePath = "") { LogMessage(LEVEL.WARNING, msgs, filePath); }
        public void Error(object msgs, [CallerFilePath] string filePath = "") { LogMessage(LEVEL.ERROR, msgs, filePath); }

        void LogMessage(LEVEL msgLevel, object msgs, string filePath = "")
        {
            if (msgLevel < Level) {
                return;
            }

            // Debug line formatting
            string line = "{0} [{1}] [{2}]:{3}";
            line = string.Format(line, DateTime.Now.ToString(culture.DateTimeFormat.LongTimePattern), msgLevel.ToString(), filePath, string.Join(" ", msgs));

            if (outputToConsole) {
                Console.ForegroundColor = colors[msgLevel];
                consoleLogFunction(line);
            }

            if (outputToFile) {
                logWriter.WriteLine(line);
            }
        }

        ~Logger()
        {
            if (flushTimer != null) flushTimer.Dispose();
            if (logWriter.BaseStream != null) logWriter.BaseStream.Dispose();
            if (logWriter != null) logWriter.Dispose();
        }
    }
}